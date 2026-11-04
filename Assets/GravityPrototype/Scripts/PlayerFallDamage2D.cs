using UnityEngine;

namespace GravityPrototype
{
    /// <summary>
    /// Measures airborne drop along the active gravity direction, resetting the axis on gravity changes.
    /// PlayerController2D samples this before consuming a buffered jump, so landings cannot be skipped.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController2D), typeof(PlayerHealth))]
    public sealed class PlayerFallDamage2D : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("安全落差（世界单位/米）；小于等于此高度不扣血。")]
        private float safeFallHeight = 3f;
        [SerializeField, Min(0f), Tooltip("超过安全高度的部分，每米造成的伤害。")]
        private float damagePerMeter = 20f;

        private PlayerHealth health;
        private Rigidbody2D body;
        private bool trackingFall;
        private float peakHeight;
        private Vector2 previousPosition;
        private Vector2 trackedUp;
        private bool hasGravityAxis;

        public float LastFallHeight { get; private set; }
        public float LastDamage { get; private set; }
        public float LastDamageFallHeight { get; private set; }
        public float LastDamageTime { get; private set; } = float.NegativeInfinity;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
            body = GetComponent<Rigidbody2D>();
            ResetTracking();
        }

        public float CalculateDamage(float fallHeight)
        {
            return Mathf.Max(0f, fallHeight - safeFallHeight) * damagePerMeter;
        }

        internal void SampleGroundState(bool grounded, Vector2 position, Vector2 gravity)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (gravity.sqrMagnitude <= 0.000001f)
            {
                // No gravity means no supporting surface and no accumulated fall distance.
                hasGravityAxis = false;
                trackingFall = false;
                previousPosition = position;
                return;
            }

            Vector2 up = -gravity.normalized;
            float height = Vector2.Dot(position, up);
            if (!hasGravityAxis || Vector2.Dot(trackedUp, up) < 0.9999f)
            {
                // A reversal/rotation starts a new fall segment, never reproject old heights.
                trackedUp = up;
                hasGravityAxis = true;
                peakHeight = height;
                previousPosition = position;
                trackingFall = !grounded;
            }

            if (!grounded)
            {
                if (!trackingFall)
                {
                    // Include the previous grounded height when walking off a ledge.
                    peakHeight = Mathf.Max(Vector2.Dot(previousPosition, up), height);
                    trackingFall = true;
                }
                else
                {
                    peakHeight = Mathf.Max(peakHeight, height);
                }
            }
            else if (trackingFall)
            {
                LastFallHeight = Mathf.Max(0f, peakHeight - height);
                float damage = health.TakeFallDamage(CalculateDamage(LastFallHeight));
                if (damage > 0f)
                {
                    // Harmless contact jitter must not immediately erase the injury feedback.
                    LastDamage = damage;
                    LastDamageFallHeight = LastFallHeight;
                    LastDamageTime = Time.time;
                }

                // Ground contact can persist for many steps and involve several colliders.
                // Only the airborne -> grounded transition settles damage.
                trackingFall = false;
            }

            previousPosition = position;
        }

        public void ResetTracking()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            peakHeight = 0f;
            previousPosition = body.position;
            hasGravityAxis = false;
            trackingFall = true; // First sample establishes a spawn in mid-air's gravity axis.
            LastFallHeight = 0f;
            LastDamage = 0f;
            LastDamageFallHeight = 0f;
            LastDamageTime = float.NegativeInfinity;
        }

        private void OnValidate()
        {
            safeFallHeight = Mathf.Max(0f, safeFallHeight);
            damagePerMeter = Mathf.Max(0f, damagePerMeter);
        }
    }
}
