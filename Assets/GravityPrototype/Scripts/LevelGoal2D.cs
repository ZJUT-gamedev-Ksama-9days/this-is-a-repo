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
        [SerializeField, Tooltip("设计文档 2.3.6.1：目标门由小球到达才打开；关闭时角色进入即算到达。")]
        private bool opensWithBall;
        [SerializeField, Tooltip("小球到达时要求电路已接通（2.3.6.3）。")]
        private bool requiresConnectedCircuit = true;

        private LevelController controller;

        public event Action<LevelGoal2D> Reached;

        public bool HasBeenReached { get; private set; }

        /// <summary>小球已经把门打开；未开放时角色进入不算到达。</summary>
        public bool IsOpen { get; private set; }

        /// <summary>目标门是否由小球到达才打开；第 1 关、原型关卡为常开门。</summary>
        public bool OpensWithBall => opensWithBall;

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
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (opensWithBall && other.attachedRigidbody != null
                && other.attachedRigidbody.GetComponent<BallMaterial2D>() != null)
            {
                OpenWithBall();
                return;
            }

            if (other.GetComponentInParent<PlayerController2D>() == null
                || (other.GetComponentInParent<PlayerHealth>() is PlayerHealth health && health.IsDead))
            {
                return;
            }

            // 门还没被小球打开时，角色进门不算到达。
            if (opensWithBall && !IsOpen)
            {
                return;
            }

            MarkReached();
        }

        /// <summary>小球到达目标门：电路接通后开门。公开给脚本关卡和测试调用。</summary>
        public void OpenWithBall()
        {
            if (IsOpen || !opensWithBall || !CircuitConnected())
            {
                return;
            }

            IsOpen = true;
        }

        private bool CircuitConnected()
        {
            if (!requiresConnectedCircuit)
            {
                return true;
            }

            if (controller == null)
            {
                foreach (var candidate in FindObjectsByType<LevelController>(FindObjectsSortMode.None))
                {
                    if (candidate.gameObject.scene != gameObject.scene) continue;
                    controller = candidate;
                    break;
                }
            }

            return controller == null || controller.IsCircuitConnected;
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
            IsOpen = false;
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
