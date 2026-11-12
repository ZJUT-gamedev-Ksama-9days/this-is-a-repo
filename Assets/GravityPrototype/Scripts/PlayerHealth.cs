using UnityEngine;

namespace GravityPrototype
{
    public enum PlayerDamageSource { None, Other, Fall, BallImpact, Spike, Void }

    /// <summary>Single-life state; legacy health values remain only for serialized/API compatibility.</summary>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, HideInInspector] private float maxHealth = 100f; // Legacy serialized/API backing; gameplay is alive/dead.
        [SerializeField, Min(0.001f), Tooltip("旧砸头伤害曲线达到此等效值时死亡；以下不累计伤害。")]
        private float lethalBallImpactEquivalent = 6.5f;
        [SerializeField, HideInInspector] // Legacy serialized value; falling no longer causes damage.
        private float lethalFallEquivalent = 15f;

        public event System.Action<PlayerHealth> Died;
        public event System.Action<PlayerHealth, float> HealthChanged;

        public float LethalBallImpactEquivalent => lethalBallImpactEquivalent;
        public float LethalFallEquivalent => lethalFallEquivalent;
        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public float Fraction => Mathf.Clamp01(CurrentHealth / maxHealth);
        public bool IsDead => CurrentHealth <= 0f;
        public float LastDamage { get; private set; }
        public float LastDamageTime { get; private set; } = float.NegativeInfinity;
        public PlayerDamageSource LastDamageSource { get; private set; }
        public float LastBallImpulse { get; private set; }
        public float LastBallAverageForce { get; private set; }
        public float LastBallContactDuration { get; private set; }

        public Vector2 PositionBeforeSimulation { get; private set; }
        public Vector2 VelocityBeforeSimulation { get; private set; }

        private Rigidbody2D body;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            CapturePreSimulationState();
            ResetHealth();
        }

        private void FixedUpdate()
        {
            CapturePreSimulationState();
        }

        private void CapturePreSimulationState()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (body == null) return;
            PositionBeforeSimulation = body.position;
            VelocityBeforeSimulation = body.linearVelocity;
        }

        /// <returns>The health actually removed, capped at remaining health.</returns>
        public float TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            {
                return 0f;
            }

            return Kill(PlayerDamageSource.Other);
        }

        /// <summary>One life only: finite valid hazards either kill or do nothing; no health depletion.</summary>
        public float Kill(PlayerDamageSource source)
        {
            if (IsDead) return 0f;
            LastDamage = CurrentHealth;
            CurrentHealth = 0f;
            LastDamageTime = Time.time;
            LastDamageSource = source;
            HealthChanged?.Invoke(this, -LastDamage);
            Died?.Invoke(this);
            return LastDamage;
        }

        /// <summary>Compatibility entry point: landing from any height is non-lethal.</summary>
        public float TakeFallDamage(float amount) => 0f;

        public float TakeBallImpactDamage(float amount, float cooldown, float normalImpulse = 0f,
            float averageForce = 0f, float contactDuration = 0f)
        {
            if (IsDead || float.IsNaN(amount) || float.IsInfinity(amount)
                || amount < lethalBallImpactEquivalent) return 0f;
            LastBallImpulse = Mathf.Max(0f, normalImpulse);
            LastBallAverageForce = Mathf.Max(0f, averageForce);
            LastBallContactDuration = Mathf.Max(0f, contactDuration);
            return Kill(PlayerDamageSource.BallImpact);
        }

        // Preserve the existing float-returning damage API used by fall/head-impact code.
        bool IDamageable.TakeDamage(float amount) => TakeDamage(amount) > 0f;

        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
            LastDamage = 0f;
            LastDamageTime = float.NegativeInfinity;
            LastDamageSource = PlayerDamageSource.None;
            LastBallImpulse = 0f;
            LastBallAverageForce = 0f;
            LastBallContactDuration = 0f;
            CapturePreSimulationState();
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            lethalBallImpactEquivalent = Mathf.Max(0.001f, lethalBallImpactEquivalent);
            lethalFallEquivalent = Mathf.Max(0.001f, lethalFallEquivalent);
        }
    }
}
