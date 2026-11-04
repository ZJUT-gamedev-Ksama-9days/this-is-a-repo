using System;
using UnityEngine;

namespace GravityPrototype
{
    public enum SpikeDamageMode
    {
        /// <summary>Damage grows smoothly with contact time.</summary>
        Linear,

        /// <summary>Damage is applied in fixed ticks while the player stays on the spikes.</summary>
        Stepped,

        /// <summary>No damage during a safe window, then a fast drain.</summary>
        DelayedBurst
    }

    /// <summary>
    /// Configurable single-contact damage function y = f(t), where t is the continuous contact time.
    /// The profile is a plain serializable class so every spike can author its own curve.
    /// </summary>
    [Serializable]
    public sealed class SpikeDamageProfile
    {
        [SerializeField, Tooltip("扣血曲线：线性 / 阶梯 / 延迟后爆发。")]
        private SpikeDamageMode mode = SpikeDamageMode.Linear;
        [SerializeField, Min(0f), Tooltip("线性模式：每秒扣血量。")]
        private float damagePerSecond = 25f;
        [SerializeField, Min(0.01f), Tooltip("阶梯模式：每级间隔，秒。")]
        private float stepInterval = 0.4f;
        [SerializeField, Min(0f), Tooltip("阶梯模式：每级扣血量。")]
        private float damagePerStep = 10f;
        [SerializeField, Min(0f), Tooltip("延迟爆发模式：开始扣血前的安全时间，秒。")]
        private float delaySeconds = 0.6f;
        [SerializeField, Min(0f), Tooltip("延迟爆发模式：安全时间结束后的每秒扣血量。")]
        private float burstDamagePerSecond = 80f;
        [SerializeField, Min(0f), Tooltip("单次接触的扣血上限；0 表示不限制。")]
        private float maxDamagePerContact;

        public SpikeDamageMode Mode => mode;
        public float MaxDamagePerContact => maxDamagePerContact;

        /// <summary>Total damage accumulated by one uninterrupted contact of the given length.</summary>
        public float EvaluateTotalDamage(float contactSeconds)
        {
            if (contactSeconds <= 0f)
            {
                return 0f;
            }

            float damage;
            switch (mode)
            {
                case SpikeDamageMode.Stepped:
                    damage = Mathf.Floor(contactSeconds / Mathf.Max(0.0001f, stepInterval)) * damagePerStep;
                    break;
                case SpikeDamageMode.DelayedBurst:
                    damage = Mathf.Max(0f, contactSeconds - delaySeconds) * burstDamagePerSecond;
                    break;
                default:
                    damage = contactSeconds * damagePerSecond;
                    break;
            }

            return maxDamagePerContact > 0f ? Mathf.Min(damage, maxDamagePerContact) : damage;
        }
    }
}
