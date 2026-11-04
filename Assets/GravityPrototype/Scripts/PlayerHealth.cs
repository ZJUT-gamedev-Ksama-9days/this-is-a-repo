using UnityEngine;

namespace GravityPrototype
{
    public enum PlayerDamageSource { None, Other, Fall, BallImpact }

    /// <summary>Player health state; respawning restores health without reloading the scene.</summary>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f), Tooltip("最大生命值；出生和按 R 重置时回满。")]
        private float maxHealth = 100f;

        public event System.Action<PlayerHealth> Died;
        public event System.Action<PlayerHealth, float> HealthChanged;

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
        private float nextBallImpactTime = float.NegativeInfinity;

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

            float previous = CurrentHealth;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            LastDamage = previous - CurrentHealth;
            LastDamageTime = Time.time;
            LastDamageSource = PlayerDamageSource.Other;
            HealthChanged?.Invoke(this, -LastDamage);
            if (IsDead) Died?.Invoke(this);
            return LastDamage;
        }

        public float TakeFallDamage(float amount)
        {
            float applied = TakeDamage(amount);
            if (applied > 0f) LastDamageSource = PlayerDamageSource.Fall;
            return applied;
        }

        public float TakeBallImpactDamage(float amount, float cooldown, float normalImpulse = 0f,
            float averageForce = 0f, float contactDuration = 0f)
        {
            if (Time.time < nextBallImpactTime) return 0f;
            float applied = TakeDamage(amount);
            if (applied > 0f)
            {
                LastDamageSource = PlayerDamageSource.BallImpact;
                LastBallImpulse = Mathf.Max(0f, normalImpulse);
                LastBallAverageForce = Mathf.Max(0f, averageForce);
                LastBallContactDuration = Mathf.Max(0f, contactDuration);
                nextBallImpactTime = Time.time + Mathf.Max(0f, cooldown);
            }
            return applied;
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
            nextBallImpactTime = float.NegativeInfinity;
            CapturePreSimulationState();
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
        }
    }
}
