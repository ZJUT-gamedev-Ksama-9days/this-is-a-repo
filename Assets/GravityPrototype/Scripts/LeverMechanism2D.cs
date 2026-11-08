using System;
using UnityEngine;

namespace GravityPrototype
{
    /// <summary>
    /// A lever the player triggers by walking into its zone. Every accepted trigger turns the linked
    /// platform by one fixed step around that platform's own origin: triggers 1, 3, 5 turn
    /// counter-clockwise (+Z) and triggers 2, 4, 6 turn clockwise (-Z), so the platform alternates
    /// between two poses. The step is smoothed in FixedUpdate and, when the platform owns a kinematic
    /// Rigidbody2D, applied through MoveRotation so Physics2D keeps resolving contacts during the turn.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class LeverMechanism2D : MonoBehaviour
    {
        [SerializeField, Tooltip("参与触发的层；默认全部层，靠 PlayerController2D 过滤。")]
        private LayerMask triggerLayers = ~0;
        [SerializeField, Tooltip("被旋转的平台；留空时旋转本对象。平台原点即旋转轴心。")]
        private Transform platform;
        [SerializeField, Tooltip("平台刚体；留空时自动查找。只有 Kinematic 刚体才走 MoveRotation。")]
        private Rigidbody2D platformBody;
        [SerializeField, Range(1f, 180f), Tooltip("单次旋转角度，度。默认 30。")]
        private float stepDegrees = 30f;
        [SerializeField, Tooltip("按平台朝向自动取角（设计文档 2.3.3）：横平竖直 90°，倾斜 45°。")]
        private bool autoStepFromPlatform = true;
        [SerializeField, Min(0.01f), Tooltip("一次旋转的平滑过渡时长，秒。")]
        private float rotateSeconds = 0.35f;

        private float homeAngle;
        private float fromAngle;
        private float toAngle;
        private float angle;
        private float progress;
        private bool rotating;
        private int playerOverlaps;

        public event Action<LeverMechanism2D, int> Rotated;

        public Transform Platform => platform;
        public float StepDegrees => stepDegrees;
        public bool IsRotating => rotating;

        /// <summary>Number of accepted triggers since the last reset; odd turns are counter-clockwise.</summary>
        public int TriggerCount { get; private set; }

        private void Awake()
        {
            Collider2D leverCollider = GetComponent<Collider2D>();
            if (!leverCollider.isTrigger)
            {
                leverCollider.isTrigger = true;
            }

            if (platform == null)
            {
                platform = transform;
            }

            if (platformBody == null)
            {
                platformBody = platform.GetComponent<Rigidbody2D>();
            }

            // A kinematic body owns its rotation; a platform without a body is driven through its Transform.
            homeAngle = UsesBodyRotation ? platformBody.rotation : platform.eulerAngles.z;
            if (autoStepFromPlatform)
            {
                stepDegrees = ResolveStepDegrees(homeAngle);
            }

            fromAngle = homeAngle;
            toAngle = homeAngle;
            angle = homeAngle;
        }

        private bool UsesBodyRotation =>
            platformBody != null && platformBody.bodyType == RigidbodyType2D.Kinematic;

        /// <summary>横平竖直的平台一次转 90°，倾斜的平台一次转 45°。</summary>
        private static float ResolveStepDegrees(float homeAngle)
        {
            float offset = Mathf.Abs(Mathf.DeltaAngle(homeAngle, Mathf.Round(homeAngle / 90f) * 90f));
            return offset < 1f ? 90f : 45f;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController2D>();
            if (player == null || player.InputBlocked
                || (player.GetComponent<PlayerHealth>() is PlayerHealth health && health.IsDead)
                || (triggerLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            // One trigger per visit: the player has to leave the zone before it counts again.
            playerOverlaps++;
            if (playerOverlaps == 1)
            {
                Trigger();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController2D>() == null
                || (triggerLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            playerOverlaps = Mathf.Max(0, playerOverlaps - 1);
        }

        /// <summary>
        /// Turns the platform once. Public so scripted levels, tests and other triggers reuse the same
        /// step math instead of driving the Transform themselves.
        /// </summary>
        public void Trigger()
        {
            if (platform == null || !isActiveAndEnabled || Time.timeScale <= 0f)
            {
                return;
            }

            TriggerCount++;
            bool counterClockwise = TriggerCount % 2 == 1;
            // Retarget from the displayed pose to an authored endpoint, never accumulate mid-turn drift.
            fromAngle = angle;
            toAngle = homeAngle + (counterClockwise ? stepDegrees : 0f);
            progress = 0f;
            rotating = true;
            Rotated?.Invoke(this, TriggerCount);
        }

        private void FixedUpdate()
        {
            if (!rotating)
            {
                return;
            }

            progress = Mathf.Min(1f, progress + Time.fixedDeltaTime / rotateSeconds);
            angle = Mathf.LerpAngle(fromAngle, toAngle, Mathf.SmoothStep(0f, 1f, progress));
            if (progress >= 1f)
            {
                angle = Mathf.Repeat(toAngle, 360f);
                rotating = false;
            }

            ApplyAngle(angle);
        }

        private void ApplyAngle(float degrees)
        {
            if (platform == null)
            {
                return;
            }

            if (UsesBodyRotation)
            {
                // MoveRotation keeps the platform a real physics body, so a ball on it is carried by contacts.
                platformBody.MoveRotation(degrees);
                return;
            }

            // No kinematic body: the collider follows the Transform, which Physics2D still collides against.
            platform.rotation = Quaternion.Euler(0f, 0f, degrees);
        }

        /// <summary>Restores the authored angle and clears the trigger count, used by in-place level resets.</summary>
        public void ResetLever()
        {
            TriggerCount = 0;
            playerOverlaps = 0;
            rotating = false;
            progress = 0f;
            angle = homeAngle;
            fromAngle = homeAngle;
            toAngle = homeAngle;
            if (UsesBodyRotation) platformBody.rotation = homeAngle;
            else ApplyAngle(homeAngle);
        }

        private void OnDisable()
        {
            playerOverlaps = 0;
        }

        private void OnValidate()
        {
            stepDegrees = Mathf.Clamp(stepDegrees, 1f, 180f);
            rotateSeconds = Mathf.Max(0.01f, rotateSeconds);
        }

        private void OnDrawGizmosSelected()
        {
            Collider2D leverCollider = GetComponent<Collider2D>();
            if (leverCollider != null)
            {
                Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.85f);
                Bounds bounds = leverCollider.bounds;
                Gizmos.DrawWireCube(bounds.center, new Vector3(bounds.size.x, bounds.size.y, 0f));
            }

            Transform target = platform != null ? platform : transform;
            Gizmos.color = new Color(0.5f, 0.9f, 1f, 0.9f);
            Gizmos.DrawRay(target.position, Quaternion.Euler(0f, 0f, stepDegrees) * target.up);
            Gizmos.DrawRay(target.position, Quaternion.Euler(0f, 0f, -stepDegrees) * target.up);
        }
    }
}
