using System;
using UnityEngine;

namespace GravityPrototype
{
    /// <summary>Run health for the protagonist: full at spawn, drained by hazards, reset by the level controller.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f), Tooltip("满血值；关卡开始时恢复到该值。")]
        private float maxHealth = 100f;
        [SerializeField, Min(0f), Tooltip("受伤后的短暂无敌时间，秒；尖刺持续伤害建议保持 0。")]
        private float invulnerabilityAfterHit;

        private float currentHealth;
        private float invulnerableUntil;

        /// <summary>Raised with the signed health delta after every accepted change.</summary>
        public event Action<PlayerHealth, float> HealthChanged;
        public event Action<PlayerHealth> Died;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float NormalizedHealth => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        public bool IsDead { get; private set; }

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        public void ResetHealth()
        {
            IsDead = false;
            invulnerableUntil = 0f;
            currentHealth = maxHealth;
            HealthChanged?.Invoke(this, 0f);
        }

        public bool TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f || Time.time < invulnerableUntil)
            {
                return false;
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            invulnerableUntil = invulnerabilityAfterHit > 0f ? Time.time + invulnerabilityAfterHit : 0f;
            HealthChanged?.Invoke(this, -amount);
            if (currentHealth > 0f)
            {
                return true;
            }

            // Latch death so repeated hazard ticks cannot restart the level twice.
            IsDead = true;
            Died?.Invoke(this);
            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f || currentHealth >= maxHealth)
            {
                return;
            }

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            HealthChanged?.Invoke(this, amount);
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            invulnerabilityAfterHit = Mathf.Max(0f, invulnerabilityAfterHit);
        }
    }
}
