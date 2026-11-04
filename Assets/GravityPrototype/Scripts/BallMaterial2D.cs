using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityPrototype
{
    /// <summary>Applies an authored material profile once on spawn or explicit material selection.</summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
    public sealed class BallMaterial2D : MonoBehaviour
    {
        [SerializeField] private BallPhysicsProfile profile;
        private Rigidbody2D body;
        private GravityBody2D gravityBody;

        public BallPhysicsProfile Profile => profile;
        public float ImpactDamageMultiplier => profile != null ? profile.ImpactDamageMultiplier : 1f;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            gravityBody = GetComponent<GravityBody2D>();
            if (profile != null) ApplyProfile(profile);
        }

        public void ApplyProfile(BallPhysicsProfile nextProfile)
        {
            if (nextProfile == null) return;
            profile = nextProfile;
            if (body == null) body = GetComponent<Rigidbody2D>();
            body.useAutoMass = false;
            body.mass = profile.Mass;
            body.gravityScale = profile.GravityScale;
            body.linearDamping = profile.LinearDamping;
            body.angularDamping = profile.AngularDamping;
            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            collider.sharedMaterial = profile.ContactMaterial;
            if (profile.UseReferencePhysics)
            {
                // Circle collider and physical sphere have the same diameter in world metres.
                float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), 0.0001f);
                collider.radius = profile.RadiusMeters / scale;
                body.inertia = profile.MomentOfInertia;
                body.centerOfMass = Vector2.zero;
            }
            GetComponent<SpriteRenderer>().color = profile.Color;
            if (Application.isPlaying) body.WakeUp();
        }

        private void FixedUpdate()
        {
            ApplyAirForces();
        }

        // Uses Unity's normal force integration, not Transform motion or a velocity cap.
        internal void ApplyAirForces()
        {
            if (profile == null || !profile.UseReferencePhysics || !body.simulated
                || body.bodyType != RigidbodyType2D.Dynamic || body.IsSleeping()) return;
            Vector2 localGravity = Physics2D.gravity * body.gravityScale;
            GravityRegion2D region = GravityRegion2D.FindAtPoint(body.worldCenterOfMass,
                gameObject.scene.GetPhysicsScene2D());
            if (gravityBody != null && gravityBody.isActiveAndEnabled && region != null)
                localGravity = Vector2.Lerp(localGravity, region.Acceleration * body.gravityScale, region.Influence);
            Vector2 force = profile.CalculateAirForce(body.linearVelocity, localGravity);
            if (force.sqrMagnitude > 0f) body.AddForce(force, ForceMode2D.Force);
        }

        [ContextMenu("Apply Material Profile")]
        private void ApplyCurrentProfile()
        {
            ApplyProfile(profile);
        }
    }
}
