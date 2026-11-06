using UnityEngine;

namespace GravityPrototype
{
    /// <summary>A gravity-relative platformer body that pushes rigidbodies through contacts.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    [RequireComponent(typeof(GravityBody2D))]
    public sealed class PlayerController2D : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("水平最大行走速度，米/秒。")]
        private float moveSpeed = 5.5f;
        [SerializeField, Min(0f), Tooltip("地面行走加速度，米/秒²；也限制推球时的驱动力。")]
        private float groundAcceleration = 35f;
        [SerializeField, Min(0f)] private float groundDeceleration = 45f;
        [SerializeField, Min(0f)] private float airAcceleration = 20f;
        [SerializeField, Min(0f), Tooltip("起跳速度，米/秒；4.6 在 70% 地球重力下理论跳高约 1.54 米。")]
        private float jumpSpeed = 4.6f;
        [SerializeField, Range(0f, 0.3f)] private float coyoteTime = 0.1f;
        [SerializeField, Range(0f, 0.3f)] private float jumpBufferTime = 0.12f;
        [SerializeField, Range(0f, 80f)] private float maxGroundAngle = 50f;
        [SerializeField] private bool readKeyboardInput = true;
        [SerializeField, Min(0f), Tooltip("身体顶住可动刚体时的驱动力上限，牛顿；不用机械臂时仅允许极轻微推挤。")]
        private float bodyPushForce = 0.5f;
        [SerializeField] private Transform visuals;

        private readonly ContactPoint2D[] contacts = new ContactPoint2D[16];
        private readonly RaycastHit2D[] supportHits = new RaycastHit2D[8];
        private Vector2 supportNormal;
        private Rigidbody2D body;
        private CapsuleCollider2D capsule;
        private GravityBody2D gravityBody;
        private Vector2 upAxis = Vector2.up;
        private Vector2 moveAxis = Vector2.right;
        private bool hasGravity = true;
        private PlayerHealth health;
        private PlayerFallDamage2D fallDamage;
        private ContactFilter2D groundFilter;
        private Vector2 spawnPosition;
        private float spawnRotation;
        private float horizontalInput;
        private float jumpBufferRemaining;
        private float coyoteRemaining;
        private bool awaitingJumpSeparation;

        public bool InputBlocked { get; set; }
        public bool IsGrounded { get; private set; }
        public bool HasGravity => hasGravity;
        public Vector2 UpAxis => upAxis;
        public float HorizontalInput => horizontalInput;
        /// <summary>Position and linear velocity at the start of the latest physics step.</summary>
        public Vector2 PositionBeforeSimulation { get; private set; }
        public Vector2 VelocityBeforeSimulation { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            capsule = GetComponent<CapsuleCollider2D>();
            gravityBody = GetComponent<GravityBody2D>();
            health = GetComponent<PlayerHealth>();
            fallDamage = GetComponent<PlayerFallDamage2D>();
            groundFilter = new ContactFilter2D { useTriggers = false };
            spawnPosition = body.position;
            spawnRotation = body.rotation;
        }

        private void Update()
        {
            if (!readKeyboardInput || InputBlocked || (health != null && health.IsDead))
            {
                return;
            }

            float horizontal = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
            bool jump = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W)
                || Input.GetKeyDown(KeyCode.UpArrow);
            SetInput(horizontal, jump);
        }

        /// <summary>Accepts keyboard, replay or scripted input; jumpPressed means the press edge, not held.</summary>
        public void SetInput(float horizontal, bool jumpPressed)
        {
            if (InputBlocked)
            {
                horizontalInput = 0f;
                jumpBufferRemaining = 0f;
                return;
            }
            if (health != null && health.IsDead)
            {
                return;
            }

            horizontalInput = Mathf.Clamp(horizontal, -1f, 1f);
            if (jumpPressed)
            {
                jumpBufferRemaining = Mathf.Max(jumpBufferTime, Time.fixedDeltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (!body.simulated || body.bodyType != RigidbodyType2D.Dynamic)
            {
                return;
            }

            VelocityBeforeSimulation = body.linearVelocity;
            PositionBeforeSimulation = body.position;
            // GravityBody2D runs first so a moved frame takes effect in this same physics step.
            Vector2 gravity = gravityBody != null && gravityBody.isActiveAndEnabled
                ? gravityBody.CurrentAcceleration : Physics2D.gravity * body.gravityScale;
            hasGravity = gravity.sqrMagnitude > 0.000001f;
            if (hasGravity)
            {
                Vector2 nextUp = -gravity.normalized;
                if (Vector2.Dot(nextUp, upAxis) < 0.9999f)
                {
                    coyoteRemaining = 0f;
                    awaitingJumpSeparation = false;
                }

                upAxis = nextUp;
                moveAxis = new Vector2(upAxis.y, -upAxis.x);
                // A/D retain screen-left/right under vertical gravity; on side walls D moves up.
                if (moveAxis.x < -0.0001f || (Mathf.Abs(moveAxis.x) <= 0.0001f && moveAxis.y < 0f))
                    moveAxis = -moveAxis;
                float angle = Mathf.Atan2(upAxis.y, upAxis.x) * Mathf.Rad2Deg - 90f;
                if (Mathf.Abs(Mathf.DeltaAngle(body.rotation, angle)) > 0.01f) body.rotation = angle;
            }
            else
            {
                coyoteRemaining = 0f;
            }

            bool hasGroundContact = hasGravity && HasGroundContact();
            if (!hasGroundContact)
            {
                awaitingJumpSeparation = false;
            }

            // Ascending a slope is still supported. Only a jump suppresses old support contacts.
            IsGrounded = hasGroundContact && !awaitingJumpSeparation;
            // Settle a landing before jumping again; a buffered jump must not bypass fall damage.
            if (fallDamage != null)
            {
                fallDamage.SampleGroundState(IsGrounded, body.position, gravity);
            }

            if (health != null && health.IsDead)
            {
                horizontalInput = 0f;
                jumpBufferRemaining = 0f;
                coyoteRemaining = 0f;
                body.linearVelocity -= moveAxis * Vector2.Dot(body.linearVelocity, moveAxis);
                return;
            }

            coyoteRemaining = IsGrounded ? coyoteTime
                : Mathf.Max(0f, coyoteRemaining - Time.fixedDeltaTime);

            float targetSpeed = horizontalInput * moveSpeed;
            Vector2 driveAxis = moveAxis;
            if (IsGrounded)
            {
                driveAxis = new Vector2(supportNormal.y, -supportNormal.x);
                if (Vector2.Dot(driveAxis, moveAxis) < 0f) driveAxis = -driveAxis;
                // Keep the authored speed along the screen movement axis without driving into the slope.
                targetSpeed /= Mathf.Max(0.5f, Vector2.Dot(driveAxis, moveAxis));
            }
            float acceleration = IsGrounded
                ? (Mathf.Abs(horizontalInput) > 0.01f ? groundAcceleration : groundDeceleration)
                : airAcceleration;
            float desiredAcceleration = (targetSpeed - Vector2.Dot(body.linearVelocity, driveAxis)) / Time.fixedDeltaTime;
            float drive = Mathf.Clamp(desiredAcceleration, -acceleration, acceleration);
            if (Mathf.Abs(drive) > 0.001f)
            {
                Vector2 driveForce = driveAxis * (drive * body.mass);
                int count = capsule.GetContacts(groundFilter, contacts);
                for (int i = 0; i < count; i++)
                {
                    ContactPoint2D contact = contacts[i];
                    Rigidbody2D other = contact.rigidbody == body ? contact.otherRigidbody : contact.rigidbody;
                    if (other == null || other == body || other.bodyType != RigidbodyType2D.Dynamic) continue;
                    if (Vector2.Dot(contact.normal, driveForce.normalized) < -0.2f)
                    {
                        driveForce = Vector2.ClampMagnitude(driveForce, bodyPushForce);
                        break;
                    }
                }
                body.AddForce(driveForce, ForceMode2D.Force);
            }

            if (hasGravity && jumpBufferRemaining > 0f && (IsGrounded || coyoteRemaining > 0f))
            {
                body.linearVelocity += upAxis * (jumpSpeed - Vector2.Dot(body.linearVelocity, upAxis));
                jumpBufferRemaining = 0f;
                coyoteRemaining = 0f;
                awaitingJumpSeparation = true;
                IsGrounded = false;
            }
            else
            {
                jumpBufferRemaining = Mathf.Max(0f, jumpBufferRemaining - Time.fixedDeltaTime);
            }

            if (visuals != null && Mathf.Abs(horizontalInput) > 0.01f)
            {
                Vector3 scale = visuals.localScale;
                Vector2 localRight = new Vector2(upAxis.y, -upAxis.x);
                scale.x = Mathf.Abs(scale.x) * Mathf.Sign(horizontalInput * Vector2.Dot(moveAxis, localRight));
                visuals.localScale = scale;
            }
        }

        private bool HasGroundContact()
        {
            float minimumNormalY = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
            int count = capsule.GetContacts(groundFilter, contacts);
            for (int i = 0; i < count; i++)
            {
                if (Vector2.Dot(contacts[i].normal, upAxis) >= minimumNormalY
                    && Vector2.Dot(contacts[i].point - body.worldCenterOfMass, upAxis) < 0f)
                {
                    supportNormal = contacts[i].normal;
                    return true;
                }
            }

            // Contact manifolds can briefly disappear at low gravity. Check only a 2cm support skin,
            // never snap/teleport the body and never treat a rising jump as grounded.
            if (Vector2.Dot(body.linearVelocity, upAxis) > 0.15f) return false;
            count = capsule.Cast(-upAxis, groundFilter, supportHits, 0.02f);
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = supportHits[i];
                if (hit.collider != null && hit.distance > 0f && Vector2.Dot(hit.normal, upAxis) >= minimumNormalY)
                {
                    supportNormal = hit.normal;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Launch pads set velocity once; they do not alter gravity or enable a buffered extra jump.</summary>
        public void ApplyLaunch(Vector2 velocity)
        {
            if (!isActiveAndEnabled || body == null || !body.simulated
                || body.bodyType != RigidbodyType2D.Dynamic || (health != null && health.IsDead)) return;
            body.linearVelocity = velocity;
            jumpBufferRemaining = 0f;
            coyoteRemaining = 0f;
            awaitingJumpSeparation = true;
            IsGrounded = false;
        }

        public void ResetToSpawn()
        {
            ClearInput();
            body.position = spawnPosition;
            body.rotation = spawnRotation;
            upAxis = Vector2.up;
            moveAxis = Vector2.right;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.transform.SetPositionAndRotation(spawnPosition, Quaternion.Euler(0f, 0f, spawnRotation));
            body.WakeUp();
            if (health != null)
            {
                health.ResetHealth();
            }

            if (fallDamage != null)
            {
                fallDamage.ResetTracking();
            }

            if (visuals != null)
            {
                Vector3 scale = visuals.localScale;
                scale.x = Mathf.Abs(scale.x);
                visuals.localScale = scale;
            }
        }

        private void ClearInput()
        {
            horizontalInput = 0f;
            jumpBufferRemaining = 0f;
            coyoteRemaining = 0f;
            awaitingJumpSeparation = false;
            IsGrounded = false;
        }

        private void OnDisable()
        {
            ClearInput();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ClearInput();
            }
        }
    }
}
