using UnityEngine;

namespace GravityPrototype
{
    /// <summary>Converts solver normal impulse into equivalent average contact force and gameplay damage.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [DefaultExecutionOrder(100)]
    public sealed class BallImpactDamage2D : MonoBehaviour
    {
        // Preserve legacy fields already serialized on prefabs; damage is now impulse based.
        [SerializeField, HideInInspector] private float safeImpactSpeed = 6f;
        [SerializeField, HideInInspector] private float damagePerSpeed = 8f;
        [SerializeField, Min(0.01f)] private float impactDurationSeconds = 0.1f;
        [SerializeField, HideInInspector] private float safeAverageForce = 50f;
        [SerializeField, HideInInspector] private float damagePerNewton = 0.1f;
        [SerializeField, Min(0f), Tooltip("头部接触冲量不超过此值免伤，单位 N·s。")]
        private float safeHeadImpulse = 5f;
        [SerializeField, Min(0.001f), Tooltip("超过阈值后，指数翻倍所需的冲量增量，单位 N·s。")]
        private float impulseDoublingInterval = 10f;
        [SerializeField, Min(0f), Tooltip("指数曲线 HP 系数：D = scale * (2^((J-threshold)/interval)-1)。")]
        private float exponentialDamageScale = 5f;
        [SerializeField, Min(0f), Tooltip("单次砸击最高伤害 HP。")]
        private float maxDamagePerHit = 100f;
        [SerializeField, Min(0f), Tooltip("同一玩家受到球头部砸击后的冷却，秒。摔落伤害不受影响。")]
        private float hitCooldown = 0.4f;
        [SerializeField, Min(0f)] private float minimumIncomingBallSpeed = 0.25f;
        [SerializeField, Range(0.1f, 0.9f), Tooltip("从胶囊头部端向内的区域比例，随角色重力朝向旋转。")]
        private float headRegionFraction = 0.35f;
        [SerializeField, Range(0f, 1f), Tooltip("接触表面法线朝角色头部方向的最小点积。")]
        private float minimumHeadNormalAlignment = 0.55f;

        private Rigidbody2D body;
        private Vector2 velocityBeforeSimulation;

        public float ImpactDurationSeconds => Mathf.Max(0.01f, impactDurationSeconds);
        public float LastNormalImpulse { get; private set; }
        public float LastBallImpulseShare { get; private set; }
        public float LastAverageForce { get; private set; }
        public float LastIncomingBallSpeed { get; private set; }
        public int LastContactCount { get; private set; }
        public float LastHeadImpactImpulse { get; private set; }

        private void FixedUpdate()
        {
            CapturePreSimulationState();
        }

        private void CapturePreSimulationState()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            velocityBeforeSimulation = body.linearVelocity;
        }

        public float CalculateAverageForce(float normalImpulse)
        {
            if (normalImpulse <= 0f || float.IsNaN(normalImpulse) || float.IsInfinity(normalImpulse)) return 0f;
            return normalImpulse / ImpactDurationSeconds;
        }

        public float CalculateDamageFromImpulse(float normalImpulse)
        {
            if (float.IsNaN(normalImpulse) || normalImpulse <= safeHeadImpulse
                || exponentialDamageScale <= 0f || maxDamagePerHit <= 0f) return 0f;
            if (float.IsPositiveInfinity(normalImpulse)) return maxDamagePerHit;
            double exponent = ((double)normalImpulse - safeHeadImpulse) / Mathf.Max(0.001f, impulseDoublingInterval);
            // Clamp before exponentiation, avoiding overflow even for extreme solver impulses.
            double capExponent = System.Math.Log(1d + (double)maxDamagePerHit / exponentialDamageScale, 2d);
            if (exponent >= capExponent) return maxDamagePerHit;
            return (float)(exponentialDamageScale * (System.Math.Pow(2d, exponent) - 1d));
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!isActiveAndEnabled || body == null || !body.simulated
                || body.bodyType != RigidbodyType2D.Dynamic) return;
            Rigidbody2D callbackBody = collision.rigidbody;
            Rigidbody2D callbackOther = collision.otherRigidbody;
            Rigidbody2D targetBody = null;
            if (callbackBody != null && callbackBody != body
                && callbackBody.GetComponent<PlayerHealth>() != null)
                targetBody = callbackBody;
            else if (callbackOther != null && callbackOther != body
                && callbackOther.GetComponent<PlayerHealth>() != null)
                targetBody = callbackOther;
            if (targetBody == null || targetBody == body) return;
            PlayerHealth health = targetBody.GetComponent<PlayerHealth>();
            PlayerController2D targetPlayer = targetBody.GetComponent<PlayerController2D>();
            CapsuleCollider2D capsule = targetBody.GetComponent<CapsuleCollider2D>();
            if (health == null || capsule == null || health.IsDead) return;
            LastContactCount = collision.contactCount;

            Vector2 up = targetPlayer != null ? targetPlayer.UpAxis.normalized : (Vector2)targetBody.transform.up;
            if (up.sqrMagnitude <= 0.000001f) return;
            float capsuleHeight = capsule.size.y * Mathf.Abs(targetBody.transform.lossyScale.y);
            float headDepth = Mathf.Max(0.08f, capsuleHeight * headRegionFraction);
            Vector2 playerCenter = targetBody.transform.TransformPoint(capsule.offset);
            float ballSpeedTowardHead = Vector2.Dot(velocityBeforeSimulation, -up);
            if (ballSpeedTowardHead < minimumIncomingBallSpeed) return;

            float headImpulse = 0f;
            float incomingHeadSpeed = 0f;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint2D contact = collision.GetContact(i);
                float heightAlongUp = Vector2.Dot(contact.point - playerCenter, up);
                if (heightAlongUp < capsuleHeight * 0.5f - headDepth) continue;

                Vector2 ballToContact = contact.point - body.worldCenterOfMass;
                if (ballToContact.sqrMagnitude <= 0.000001f) continue;
                Vector2 radial = ballToContact.normalized;
                float headAlignment = Vector2.Dot(-radial, up);
                if (headAlignment < minimumHeadNormalAlignment) continue;

                float incoming = Vector2.Dot(velocityBeforeSimulation, radial);
                if (incoming < minimumIncomingBallSpeed) continue;

                float contactImpulse = Mathf.Abs(contact.normalImpulse);
                headImpulse += contactImpulse;
                incomingHeadSpeed = Mathf.Max(incomingHeadSpeed, incoming);
            }

            LastHeadImpactImpulse = headImpulse;
            LastIncomingBallSpeed = incomingHeadSpeed;
            LastNormalImpulse = headImpulse;
            LastBallImpulseShare = headImpulse;
            if (headImpulse <= 0f) return;
            LastAverageForce = CalculateAverageForce(headImpulse);
            health.TakeBallImpactDamage(CalculateDamageFromImpulse(headImpulse), hitCooldown,
                headImpulse, LastAverageForce, ImpactDurationSeconds);
        }

        private void OnValidate()
        {
            impactDurationSeconds = Mathf.Max(0.01f, impactDurationSeconds);
            safeHeadImpulse = Mathf.Max(0f, safeHeadImpulse);
            impulseDoublingInterval = Mathf.Max(0.001f, impulseDoublingInterval);
            exponentialDamageScale = Mathf.Max(0f, exponentialDamageScale);
            maxDamagePerHit = Mathf.Max(0f, maxDamagePerHit);
            hitCooldown = Mathf.Max(0f, hitCooldown);
            minimumIncomingBallSpeed = Mathf.Max(0f, minimumIncomingBallSpeed);
            headRegionFraction = Mathf.Clamp(headRegionFraction, 0.1f, 0.9f);
            minimumHeadNormalAlignment = Mathf.Clamp01(minimumHeadNormalAlignment);
        }
    }
}
