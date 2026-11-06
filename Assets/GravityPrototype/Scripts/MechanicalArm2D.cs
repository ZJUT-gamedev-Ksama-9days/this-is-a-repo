using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityPrototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(AstronautToolController))]
    public sealed class MechanicalArm2D : MonoBehaviour
    {
        [SerializeField] private Camera inputCamera;
        [SerializeField, Min(0.1f)] private float reach = 2.5f;
        [SerializeField, Min(0f)] private float pushForce = 55f;
        [SerializeField, Min(0f)] private float grabSpring = 120f;
        [SerializeField, Min(0f)] private float grabDamping = 18f;
        [SerializeField, Min(0f)] private float maxGrabForce = 90f;
        [SerializeField] private LineRenderer beam;
        [SerializeField] private bool readMouseInput = true;

        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
        private AstronautToolController tools;
        private Rigidbody2D ownerBody;
        private Vector2 aimPoint;
        private bool pushing;
        private bool hasAim;
        private bool hasFocus = true;
        private Vector2 grabLocalPoint;

        public Rigidbody2D GrabbedBody { get; private set; }
        public bool CanUse => isActiveAndEnabled && hasFocus && tools != null
            && tools.IsOperational && !tools.InputBlocked && tools.Equipped == AstronautTool.MechanicalArm;
        public Vector2 Origin => ownerBody != null ? ownerBody.worldCenterOfMass : (Vector2)transform.position;

        private void Awake()
        {
            tools = GetComponent<AstronautToolController>();
            ownerBody = GetComponent<Rigidbody2D>();
            if (inputCamera == null) inputCamera = Camera.main;
            aimPoint = Origin + Vector2.right;
        }

        private void Update()
        {
            if (!CanUse) { Release(); return; }
            if (!readMouseInput || inputCamera == null) return;
            Ray ray = inputCamera.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(Vector3.forward, transform.position);
            if (plane.Raycast(ray, out float distance)) SetAim(ray.GetPoint(distance));
            if (Input.GetMouseButtonDown(0)) TryGrab();
            if (!Input.GetMouseButton(0)) ReleaseGrab();
            SetPushing(Input.GetMouseButton(1));
        }

        public void SetAim(Vector2 worldPoint)
        {
            aimPoint = worldPoint;
            hasAim = true;
        }

        public void SetPushing(bool value)
        {
            pushing = value && CanUse;
            if (pushing) ReleaseGrab();
        }

        public bool TryGrab()
        {
            if (!CanUse || !hasAim || !TryFindTarget(out RaycastHit2D hit)) return false;
            Rigidbody2D target = hit.rigidbody;
            if (!IsMovable(target) || !IsInGravityField(target)) return false;
            GrabbedBody = target;
            grabLocalPoint = target.transform.InverseTransformPoint(hit.point);
            pushing = false;
            return true;
        }

        private bool IsInGravityField(Rigidbody2D target)
        {
            return target != null && GravityRegion2D.FindAtPoint(target.worldCenterOfMass,
                target.gameObject.scene.GetPhysicsScene2D()) != null;
        }

        private bool IsMovable(Rigidbody2D target)
        {
            return target != null && target != ownerBody && target.simulated
                && target.bodyType == RigidbodyType2D.Dynamic && target.GetComponent<PlayerController2D>() == null;
        }

        private bool TryFindTarget(out RaycastHit2D result)
        {
            result = default;
            Vector2 delta = aimPoint - Origin;
            if (delta.sqrMagnitude < 0.0001f) return false;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false, useLayerMask = false };
            int count = gameObject.scene.GetPhysicsScene2D().Raycast(Origin, delta.normalized,
                Mathf.Min(reach, delta.magnitude + 0.15f), filter, hits);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].rigidbody == ownerBody || hits[i].collider == null) continue;
                if (hits[i].distance < nearest) { result = hits[i]; nearest = result.distance; }
            }
            return nearest < float.PositiveInfinity;
        }

        private bool HasLineOfSight(Vector2 point, Rigidbody2D target)
        {
            Vector2 delta = point - Origin;
            if (delta.sqrMagnitude < 0.0001f) return true;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false, useLayerMask = false };
            int count = gameObject.scene.GetPhysicsScene2D().Raycast(Origin, delta.normalized,
                delta.magnitude, filter, hits);
            for (int i = 0; i < count; i++)
                if (hits[i].collider != null && hits[i].rigidbody != ownerBody && hits[i].rigidbody != target)
                    return false;
            return true;
        }

        private void FixedUpdate()
        {
            if (!CanUse) { Release(); return; }
            if (GrabbedBody != null)
            {
                if (!IsMovable(GrabbedBody) || !IsInGravityField(GrabbedBody)) { ReleaseGrab(); return; }
                Vector2 anchor = GrabbedBody.transform.TransformPoint(grabLocalPoint);
                if (Vector2.Distance(Origin, anchor) > reach + 0.4f || !HasLineOfSight(anchor, GrabbedBody))
                { ReleaseGrab(); return; }
                Vector2 desired = Origin + Vector2.ClampMagnitude(aimPoint - Origin, reach);
                if (!HasLineOfSight(desired, GrabbedBody)) { ReleaseGrab(); return; }
                Vector2 relativeVelocity = GrabbedBody.GetPointVelocity(anchor) - ownerBody.linearVelocity;
                // Implicit spring integration includes the surface anchor's rotational response.
                // Fixed damping alone can explode on a light balloon at the normal 0.02 s step.
                Vector2 lever = anchor - GrabbedBody.worldCenterOfMass;
                float inverseMass = 1f / GrabbedBody.mass;
                if ((GrabbedBody.constraints & RigidbodyConstraints2D.FreezeRotation) == 0)
                    inverseMass += lever.sqrMagnitude / Mathf.Max(GrabbedBody.inertia, 0.000001f);
                if (ownerBody.constraints != RigidbodyConstraints2D.FreezeAll) inverseMass += 1f / ownerBody.mass;
                float dt = Time.fixedDeltaTime;
                float denominator = 1f + (grabDamping * dt + grabSpring * dt * dt) * inverseMass;
                Vector2 force = (grabSpring * (desired - anchor - relativeVelocity * dt)
                    - grabDamping * relativeVelocity) / denominator;
                ApplyForce(GrabbedBody, anchor, Vector2.ClampMagnitude(force, maxGrabForce));
            }
            else if (pushing && hasAim && TryFindTarget(out RaycastHit2D hit) && IsMovable(hit.rigidbody))
            {
                ApplyForce(hit.rigidbody, hit.point, (aimPoint - Origin).normalized * pushForce);
            }
        }

        private void ApplyForce(Rigidbody2D target, Vector2 point, Vector2 force)
        {
            target.AddForceAtPosition(force, point, ForceMode2D.Force);
            ownerBody.AddForce(-force, ForceMode2D.Force);
        }

        private void LateUpdate()
        {
            if (beam == null) return;
            beam.enabled = CanUse;
            if (!CanUse) return;
            Vector2 end = GrabbedBody != null
                ? (Vector2)GrabbedBody.transform.TransformPoint(grabLocalPoint)
                : Origin + Vector2.ClampMagnitude(aimPoint - Origin, reach);
            beam.SetPosition(0, Origin);
            beam.SetPosition(1, end);
            Color color = GrabbedBody != null ? new Color(1f, 0.8f, 0.2f) : new Color(0.4f, 1f, 0.7f, 0.5f);
            beam.startColor = beam.endColor = color;
        }

        public void ReleaseGrab() => GrabbedBody = null;
        public void Release() { ReleaseGrab(); pushing = false; }
        private void OnDisable() => Release();
        private void OnApplicationFocus(bool focus)
        {
            hasFocus = focus;
            if (!focus) Release();
        }
    }
}
