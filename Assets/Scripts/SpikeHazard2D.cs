using System;
using System.Collections.Generic;
using UnityEngine;

namespace GravityPrototype
{
    /// <summary>
    /// Tracks continuous contact with damageable targets and converts contact time into damage
    /// through its configurable <see cref="SpikeDamageProfile"/>. The collider is forced to a trigger
    /// so spikes never block movement, they only drain health.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class SpikeHazard2D : MonoBehaviour
    {
        [SerializeField] private SpikeDamageProfile damageProfile = new SpikeDamageProfile();
        [SerializeField, Tooltip("参与伤害的层；默认全部层，靠 IDamageable 过滤。")]
        private LayerMask damageableLayers = ~0;

        private readonly Dictionary<IDamageable, Contact> contacts = new Dictionary<IDamageable, Contact>(4);
        private readonly List<IDamageable> released = new List<IDamageable>(4);

        private sealed class Contact
        {
            public int OverlappingColliders;
            public float Seconds;
            public float AppliedDamage;
        }

        public event Action<SpikeHazard2D, IDamageable, float> DamageDealt;

        public SpikeDamageProfile DamageProfile => damageProfile;

        private void Awake()
        {
            Collider2D hazardCollider = GetComponent<Collider2D>();
            if (!hazardCollider.isTrigger)
            {
                hazardCollider.isTrigger = true;
            }
        }

        private void FixedUpdate()
        {
            if (contacts.Count == 0)
            {
                return;
            }

            float step = Time.fixedDeltaTime;
            released.Clear();
            foreach (KeyValuePair<IDamageable, Contact> pair in contacts)
            {
                IDamageable target = pair.Key;
                Contact contact = pair.Value;
                if (!IsAlive(target) || target.IsDead)
                {
                    released.Add(target);
                    continue;
                }

                contact.Seconds += step;
                float totalDamage = damageProfile.EvaluateTotalDamage(contact.Seconds);
                float delta = totalDamage - contact.AppliedDamage;
                if (delta <= 0f)
                {
                    continue;
                }

                // Ignored hits (invulnerability, already dead) are retried on the next physics step.
                if (!target.TakeDamage(delta))
                {
                    continue;
                }

                contact.AppliedDamage = totalDamage;
                DamageDealt?.Invoke(this, target, delta);
            }

            for (int i = 0; i < released.Count; i++)
            {
                contacts.Remove(released[i]);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            IDamageable target = FindTarget(other);
            if (target == null)
            {
                return;
            }

            // A target can own several colliders; count them so one exit does not end the contact.
            if (contacts.TryGetValue(target, out Contact contact))
            {
                contact.OverlappingColliders++;
                return;
            }

            contacts.Add(target, new Contact { OverlappingColliders = 1 });
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            IDamageable target = FindTarget(other);
            if (target == null || !contacts.TryGetValue(target, out Contact contact))
            {
                return;
            }

            contact.OverlappingColliders--;
            if (contact.OverlappingColliders <= 0)
            {
                contacts.Remove(target);
            }
        }

        private IDamageable FindTarget(Collider2D other)
        {
            if ((damageableLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return null;
            }

            return other.GetComponentInParent<IDamageable>();
        }

        private static bool IsAlive(IDamageable target)
        {
            return !(target is UnityEngine.Object unityObject) || unityObject != null;
        }

        /// <summary>Current contact time of one target; useful for debug UI and tests.</summary>
        public float GetContactSeconds(IDamageable target)
        {
            return target != null && contacts.TryGetValue(target, out Contact contact) ? contact.Seconds : 0f;
        }

        public void ClearContacts()
        {
            contacts.Clear();
        }

        private void OnDisable()
        {
            contacts.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.3f, 0.85f);
            Collider2D hazardCollider = GetComponent<Collider2D>();
            if (hazardCollider == null)
            {
                return;
            }

            Bounds bounds = hazardCollider.bounds;
            Gizmos.DrawWireCube(bounds.center, new Vector3(bounds.size.x, bounds.size.y, 0f));
        }
    }
}
