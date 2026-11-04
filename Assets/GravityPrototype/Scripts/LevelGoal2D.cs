using System;
using UnityEngine;

namespace GravityPrototype
{
    /// <summary>The finish trigger of a level. It reports the first player arrival and the controller stops the clock.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class LevelGoal2D : MonoBehaviour
    {
        [SerializeField, Tooltip("到达后隐藏终点标记的子对象；留空则不处理。")]
        private GameObject visualOnReached;

        public event Action<LevelGoal2D> Reached;

        public bool HasBeenReached { get; private set; }

        private void Awake()
        {
            Collider2D goalCollider = GetComponent<Collider2D>();
            if (!goalCollider.isTrigger)
            {
                goalCollider.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController2D>() == null
                || (other.GetComponentInParent<PlayerHealth>() is PlayerHealth health && health.IsDead))
            {
                return;
            }

            MarkReached();
        }

        /// <summary>Completes the goal once. Public so scripted levels and tests can skip the trigger.</summary>
        public void MarkReached()
        {
            if (HasBeenReached)
            {
                return;
            }

            HasBeenReached = true;
            if (visualOnReached != null)
            {
                visualOnReached.SetActive(false);
            }

            Reached?.Invoke(this);
        }

        public void ResetGoal()
        {
            HasBeenReached = false;
            if (visualOnReached != null)
            {
                visualOnReached.SetActive(true);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Collider2D goalCollider = GetComponent<Collider2D>();
            if (goalCollider == null)
            {
                return;
            }

            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.85f);
            Bounds bounds = goalCollider.bounds;
            Gizmos.DrawWireCube(bounds.center, new Vector3(bounds.size.x, bounds.size.y, 0f));
        }
    }
}
