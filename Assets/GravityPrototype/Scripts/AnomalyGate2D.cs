using System;
using UnityEngine;

namespace GravityPrototype
{
    /// <summary>
    /// An anomaly gate: when the ball enters the zone the door leaf stops blocking it, the gate pulls
    /// the ball into its hole and the ball is respawned at its start. The gate never touches
    /// PlayerHealth or LevelController, so a swallowed ball is not a failure; the run keeps going.
    /// Suction uses forces while the ball is dynamic and switches to a kinematic MovePosition slurp at
    /// the event horizon, which keeps contacts valid and rules out high-speed tunnelling.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class AnomalyGate2D : MonoBehaviour
    {
        private enum GateState
        {
            Closed,
            Opening,
            Sucking,
            Swallowing
        }

        [SerializeField, Tooltip("被吸入的小球；留空时自动使用场景中的 GravityBody2D。")]
        private Rigidbody2D ball;
        [SerializeField, Tooltip("小球重生点；留空时使用小球在 Awake 时的位置。")]
        private Transform ballSpawn;
        [SerializeField, Tooltip("黑洞中心；留空时使用本对象位置。")]
        private Transform hole;
        [SerializeField, Tooltip("门关闭时的表现对象；打开时隐藏。留空则不处理。")]
        private GameObject closedVisual;
        [SerializeField, Tooltip("门打开时的表现对象；关闭时隐藏。留空则不处理。")]
        private GameObject openVisual;
        [SerializeField, Tooltip("关闭时挡住小球的碰撞体（门扇）；打开时只对小球忽略碰撞，不做启停。")]
        private Collider2D[] barrierColliders;
        [SerializeField, Min(0f), Tooltip("开门到开始吸入的延迟，秒。")]
        private float openDelay = 0.12f;
        [SerializeField, Min(0f), Tooltip("吸入加速度，米/秒²。")]
        private float suctionAcceleration = 30f;
        [SerializeField, Min(0.1f), Tooltip("吸入时的最高速度，米/秒；防止小球高速穿模。")]
        private float maxSuctionSpeed = 12f;
        [SerializeField, Min(0.05f), Tooltip("吸入口半径，米；小球进入该半径即被吞没。")]
        private float captureRadius = 0.28f;
        [SerializeField, Min(0.01f), Tooltip("吞没过渡时长，秒。")]
        private float swallowSeconds = 0.2f;
        [SerializeField, Min(0f), Tooltip("小球重生后到重新布防的延迟，秒。")]
        private float rearmSeconds = 0.4f;

        private Collider2D[] ballColliders;
        private RigidbodyType2D ballBodyType;
        private Vector2 spawnPosition;
        private float spawnRotation;
        private Vector2 swallowStart;
        private GateState state = GateState.Closed;
        private float stateTimer;
        private float rearmUntil;
        private bool ballInside;

        public event Action<AnomalyGate2D, Rigidbody2D> BallCaptured;
        public event Action<AnomalyGate2D, Rigidbody2D> BallRespawned;

        public Rigidbody2D Ball => ball;
        public bool IsOpen => state != GateState.Closed;

        private Vector2 HolePosition => hole != null ? (Vector2)hole.position : (Vector2)transform.position;

        private void Awake()
        {
            Collider2D gateCollider = GetComponent<Collider2D>();
            if (!gateCollider.isTrigger)
            {
                gateCollider.isTrigger = true;
            }

            if (ball == null)
            {
                BallMaterial2D[] candidates = FindObjectsByType<BallMaterial2D>(FindObjectsSortMode.None);
                foreach (var candidate in candidates)
                {
                    if (candidate.gameObject.scene != gameObject.scene) continue;
                    if (ball != null) { ball = null; Debug.LogWarning("Multiple balls: assign AnomalyGate2D.ball explicitly.", this); break; }
                    ball = candidate.GetComponent<Rigidbody2D>();
                }
            }

            if (ball != null && ball.GetComponent<PlayerController2D>() != null) ball = null;
            if (ball != null)
            {
                ballColliders = ball.GetComponentsInChildren<Collider2D>();
                ballBodyType = ball.bodyType;
                spawnPosition = ballSpawn != null ? (Vector2)ballSpawn.position : ball.position;
                spawnRotation = ballSpawn != null ? ballSpawn.eulerAngles.z : ball.rotation;
            }

            ApplyBarrierPassThrough(false);
            SetVisuals(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isActiveAndEnabled || !IsBall(other))
            {
                return;
            }

            ballInside = true;
            if (state != GateState.Closed || Time.time < rearmUntil)
            {
                return;
            }

            BeginOpening();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!isActiveAndEnabled || !IsBall(other))
            {
                return;
            }

            ballInside = false;
            // Nothing was swallowed yet: drop the attempt instead of pulling a ball that left the zone.
            if (state == GateState.Opening || state == GateState.Sucking)
            {
                CloseGate();
            }
        }

        private bool IsBall(Collider2D other)
        {
            return ball != null && other.attachedRigidbody == ball;
        }

        private void BeginOpening()
        {
            state = GateState.Opening;
            stateTimer = 0f;
            SetVisuals(true);
            ApplyBarrierPassThrough(true);
        }

        private void FixedUpdate()
        {
            if (ball == null)
            {
                return;
            }

            if (state == GateState.Closed && ballInside && Time.time >= rearmUntil) BeginOpening();
            switch (state)
            {
                case GateState.Opening:
                    stateTimer += Time.fixedDeltaTime;
                    if (stateTimer >= openDelay)
                    {
                        state = GateState.Sucking;
                        stateTimer = 0f;
                    }

                    break;

                case GateState.Sucking:
                    Suck();
                    break;

                case GateState.Swallowing:
                    Swallow();
                    break;
            }
        }

        private void Suck()
        {
            if (!ballInside)
            {
                // The exit callback normally handles this; the check also covers a collider switched off.
                CloseGate();
                return;
            }

            Vector2 offset = HolePosition - ball.worldCenterOfMass;
            if (offset.magnitude <= captureRadius)
            {
                BeginSwallow();
                return;
            }

            // Force pull keeps the ball dynamic, so it still collides instead of being teleported inward.
            ball.AddForce(offset.normalized * (suctionAcceleration * ball.mass), ForceMode2D.Force);
            float speed = ball.linearVelocity.magnitude;
            if (speed > maxSuctionSpeed)
            {
                // Only the safety cap rewrites velocity: it is what stops a fast ball from tunnelling.
                ball.linearVelocity *= maxSuctionSpeed / speed;
            }

            ball.WakeUp();
        }

        private void BeginSwallow()
        {
            var arm = FindFirstObjectByType<MechanicalArm2D>();
            if (arm != null && arm.GrabbedBody == ball) arm.Release();
            state = GateState.Swallowing;
            stateTimer = 0f;
            swallowStart = ball.position;
            // Kinematic on the last stretch: no solver impulse, no tunnelling, and GravityBody2D stops pulling.
            ball.bodyType = RigidbodyType2D.Kinematic;
            // Unity 6 renamed Rigidbody2D.velocity to linearVelocity.
            ball.linearVelocity = Vector2.zero;
            ball.angularVelocity = 0f;
            ball.WakeUp();
            BallCaptured?.Invoke(this, ball);
        }

        private void Swallow()
        {
            stateTimer += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(stateTimer / swallowSeconds);
            Vector2 target = Vector2.Lerp(swallowStart, HolePosition, Mathf.SmoothStep(0f, 1f, progress));
            ball.MovePosition(target);
            if (progress >= 1f)
            {
                RespawnBall();
                CloseGate();
            }
        }

        /// <summary>Puts the ball back on its start pose, used by the gate and by in-place level resets.</summary>
        public void RespawnBall()
        {
            if (ball == null)
            {
                return;
            }

            ball.bodyType = ballBodyType;
            ball.position = spawnPosition;
            ball.rotation = spawnRotation;
            // Unity 6 renamed Rigidbody2D.velocity to linearVelocity.
            ball.linearVelocity = Vector2.zero;
            ball.angularVelocity = 0f;
            // Clear the authored transform too, so interpolated rendering shows the respawn immediately.
            ball.transform.SetPositionAndRotation(spawnPosition, Quaternion.Euler(0f, 0f, spawnRotation));
            ball.WakeUp();
            ballInside = false;
            BallRespawned?.Invoke(this, ball);
        }

        private void CloseGate()
        {
            state = GateState.Closed;
            stateTimer = 0f;
            rearmUntil = Time.time + Mathf.Max(0f, rearmSeconds);
            SetVisuals(false);
            ApplyBarrierPassThrough(false);
        }

        private void SetVisuals(bool open)
        {
            if (closedVisual != null)
            {
                closedVisual.SetActive(!open);
            }

            if (openVisual != null)
            {
                openVisual.SetActive(open);
            }
        }

        private void ApplyBarrierPassThrough(bool passThrough)
        {
            if (ballColliders == null || barrierColliders == null)
            {
                return;
            }

            for (int i = 0; i < barrierColliders.Length; i++)
            {
                Collider2D barrier = barrierColliders[i];
                if (barrier == null)
                {
                    continue;
                }

                // Ignoring collisions instead of disabling the leaf keeps the ball and door from popping apart.
                for (int j = 0; j < ballColliders.Length; j++)
                {
                    if (ballColliders[j] != null)
                    {
                        Physics2D.IgnoreCollision(ballColliders[j], barrier, passThrough);
                    }
                }
            }
        }

        private void OnDisable()
        {
            ApplyBarrierPassThrough(false);
            if (state == GateState.Swallowing && ball != null)
            {
                // A gate switched off mid-swallow must never leave the ball kinematic.
                ball.bodyType = ballBodyType;
                // Unity 6 renamed Rigidbody2D.velocity to linearVelocity.
                ball.linearVelocity = Vector2.zero;
                ball.WakeUp();
            }

            ballInside = false;
            state = GateState.Closed;
        }

        private void OnValidate()
        {
            openDelay = Mathf.Max(0f, openDelay);
            suctionAcceleration = Mathf.Max(0f, suctionAcceleration);
            maxSuctionSpeed = Mathf.Max(0.1f, maxSuctionSpeed);
            captureRadius = Mathf.Max(0.05f, captureRadius);
            swallowSeconds = Mathf.Max(0.01f, swallowSeconds);
            rearmSeconds = Mathf.Max(0f, rearmSeconds);
        }

        private void OnDrawGizmosSelected()
        {
            Collider2D gateCollider = GetComponent<Collider2D>();
            if (gateCollider != null)
            {
                Gizmos.color = new Color(0.75f, 0.6f, 1f, 0.85f);
                Bounds bounds = gateCollider.bounds;
                Gizmos.DrawWireCube(bounds.center, new Vector3(bounds.size.x, bounds.size.y, 0f));
            }

            Gizmos.color = new Color(0.55f, 0.25f, 1f, 0.9f);
            Gizmos.DrawWireSphere(HolePosition, captureRadius);
        }
    }
}
