using UnityEngine;

namespace GravityPrototype
{
    /// <summary>A normal-gravity platformer body that pushes other rigidbodies through contacts.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class PlayerController2D : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("水平最大行走速度，米/秒。")]
        private float moveSpeed = 5.5f;
        [SerializeField, Min(0f), Tooltip("地面行走加速度，米/秒²；也限制推球时的驱动力。")]
        private float groundAcceleration = 35f;
        [SerializeField, Min(0f)] private float groundDeceleration = 45f;
        [SerializeField, Min(0f)] private float airAcceleration = 20f;
        [SerializeField, Min(0f), Tooltip("起跳竖直速度，米/秒。")]
        private float jumpSpeed = 8.5f;
        [SerializeField, Range(0f, 0.3f)] private float coyoteTime = 0.1f;
        [SerializeField, Range(0f, 0.3f)] private float jumpBufferTime = 0.12f;
        [SerializeField, Range(0f, 80f)] private float maxGroundAngle = 50f;
        [SerializeField] private bool readKeyboardInput = true;
        [SerializeField] private Transform visuals;

        private readonly ContactPoint2D[] contacts = new ContactPoint2D[16];
        private Rigidbody2D body;
        private CapsuleCollider2D capsule;
        private ContactFilter2D groundFilter;
        private Vector2 spawnPosition;
        private float spawnRotation;
        private float horizontalInput;
        private float jumpBufferRemaining;
        private float coyoteRemaining;
        private bool awaitingJumpSeparation;

        public bool IsGrounded { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            capsule = GetComponent<CapsuleCollider2D>();
            groundFilter = new ContactFilter2D { useTriggers = false };
            spawnPosition = body.position;
            spawnRotation = body.rotation;
        }

        private void Update()
        {
            if (!readKeyboardInput)
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

            bool hasGroundContact = HasGroundContact();
            if (!hasGroundContact)
            {
                awaitingJumpSeparation = false;
            }

            // Upward motion on a slope is still grounded. Ignore old contacts only after a jump,
            // until the capsule has actually separated, rather than rejecting all positive Y speed.
            IsGrounded = hasGroundContact && !awaitingJumpSeparation;
            coyoteRemaining = IsGrounded ? coyoteTime
                : Mathf.Max(0f, coyoteRemaining - Time.fixedDeltaTime);

            float targetSpeed = horizontalInput * moveSpeed;
            float acceleration = IsGrounded
                ? (Mathf.Abs(horizontalInput) > 0.01f ? groundAcceleration : groundDeceleration)
                : airAcceleration;
            float desiredAcceleration = (targetSpeed - body.linearVelocity.x) / Time.fixedDeltaTime;
            float drive = Mathf.Clamp(desiredAcceleration, -acceleration, acceleration);
            if (Mathf.Abs(drive) > 0.001f)
            {
                // Bound the drive force instead of teleporting: contacts can naturally push the ball.
                body.AddForce(Vector2.right * (drive * body.mass), ForceMode2D.Force);
            }

            if (jumpBufferRemaining > 0f && (IsGrounded || coyoteRemaining > 0f))
            {
                Vector2 velocity = body.linearVelocity;
                velocity.y = jumpSpeed;
                body.linearVelocity = velocity;
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
                scale.x = Mathf.Abs(scale.x) * Mathf.Sign(horizontalInput);
                visuals.localScale = scale;
            }
        }

        private bool HasGroundContact()
        {
            float minimumNormalY = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
            int count = capsule.GetContacts(groundFilter, contacts);
            for (int i = 0; i < count; i++)
            {
                if (contacts[i].normal.y >= minimumNormalY
                    && contacts[i].point.y < body.worldCenterOfMass.y)
                {
                    return true;
                }
            }

            return false;
        }

        public void ResetToSpawn()
        {
            ClearInput();
            body.position = spawnPosition;
            body.rotation = spawnRotation;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.transform.SetPositionAndRotation(spawnPosition, Quaternion.Euler(0f, 0f, spawnRotation));
            body.WakeUp();
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
