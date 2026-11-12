using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityPrototype
{
    /// <summary>A movable area that replaces world gravity for participating bodies.</summary>
    [DisallowMultipleComponent]
    public sealed class GravityRegion2D : MonoBehaviour
    {
        private static readonly List<GravityRegion2D> ActiveRegions = new List<GravityRegion2D>(4);

        [SerializeField, Tooltip("方框的局部空间尺寸。以刚体质心是否在框内判定。")]
        private Vector2 size = new Vector2(4f, 4f);

        [SerializeField, Tooltip("世界空间重力方向；零向量表示失重。")]
        private Vector2 direction = Vector2.up;

        [SerializeField, Min(0f), Tooltip("重力加速度，单位：米/秒²。")]
        private float acceleration = PhysicsEnvironment.Gravity;

        [SerializeField, Tooltip("重叠区域中数值大的优先；相同时后启用的优先。")]
        private int priority;

        public Vector2 Size => size;
        public Vector2 Acceleration => direction.normalized * acceleration;
        public int Priority => priority;
        public float Influence { get; private set; } = 1f;
        public Vector2 Direction => direction;

        /// <summary>设置世界空间重力方向；零向量表示失重。重力框右键切换用。</summary>
        public void SetDirection(Vector2 next)
        {
            direction = next.sqrMagnitude > 1e-6f ? next.normalized : Vector2.zero;
        }

        public void SetInfluence(float value)
        {
            Influence = Mathf.Clamp01(value);
        }

        public Vector2 WorldHalfExtents
        {
            get
            {
                Vector3 horizontal = transform.TransformVector(Vector3.right * size.x * 0.5f);
                Vector3 vertical = transform.TransformVector(Vector3.up * size.y * 0.5f);
                return new Vector2(
                    Mathf.Abs(horizontal.x) + Mathf.Abs(vertical.x),
                    Mathf.Abs(horizontal.y) + Mathf.Abs(vertical.y));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearRegistry()
        {
            ActiveRegions.Clear();
        }

        private void OnEnable()
        {
            if (!ActiveRegions.Contains(this))
            {
                ActiveRegions.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveRegions.Remove(this);
        }

        public bool Contains(Vector2 worldPoint)
        {
            Vector3 localPoint = transform.InverseTransformPoint(
                new Vector3(worldPoint.x, worldPoint.y, transform.position.z));
            return Mathf.Abs(localPoint.x) <= size.x * 0.5f
                && Mathf.Abs(localPoint.y) <= size.y * 0.5f;
        }

        internal static GravityRegion2D FindAtPoint(Vector2 worldPoint, PhysicsScene2D physicsScene)
        {
            GravityRegion2D selected = null;
            for (int i = 0; i < ActiveRegions.Count; i++)
            {
                GravityRegion2D candidate = ActiveRegions[i];
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.Influence <= 0f
                    || candidate.gameObject.scene.GetPhysicsScene2D() != physicsScene
                    || !candidate.Contains(worldPoint))
                {
                    continue;
                }

                if (selected == null || candidate.priority >= selected.priority)
                {
                    selected = candidate;
                }
            }

            return selected;
        }

        private void OnValidate()
        {
            size.x = Mathf.Max(0.1f, size.x);
            size.y = Mathf.Max(0.1f, size.y);
            acceleration = Mathf.Max(0f, acceleration);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.9f);
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0f));
            Gizmos.matrix = previousMatrix;
            Gizmos.DrawRay(transform.position, (Vector3)direction.normalized);
        }
    }
}
