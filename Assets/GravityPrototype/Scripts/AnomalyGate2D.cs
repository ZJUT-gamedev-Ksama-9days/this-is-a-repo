using System;
using System.Collections.Generic;
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

        [SerializeField, Tooltip("主小球（留空时取场景中第一个小球）；异常门会吞下任何落入的小球。")]
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

        private sealed class BallRecord
        {
            public Rigidbody2D body;
            public Collider2D[] colliders;
            public RigidbodyType2D bodyType;
            public Vector2 spawn;
            public float spawnRotation;
        }

        private BallRecord[] records = new BallRecord[0];
        private BallRecord primary;
        private BallRecord inside;
        private BallRecord active;
        private Vector2 swallowStart;
        private GateState state = GateState.Closed;
        private float stateTimer;
        private float rearmUntil;

        public event Action<AnomalyGate2D, Rigidbody2D> BallCaptured;
        public event Action<AnomalyGate2D, Rigidbody2D> BallRespawned;

        public Rigidbody2D Ball => active != null ? active.body : primary != null ? primary.body : ball;
        public bool IsOpen => state != GateState.Closed;

        private Vector2 HolePosition => hole != null ? (Vector2)hole.position : (Vector2)transform.position;

        private void Awake()
        {
            Collider2D gateCollider = GetComponent<Collider2D>();
            if (!gateCollider.isTrigger)
            {
                gateCollider.isTrigger = true;
            }

            // 任何小球落入异常门都可以被吞：取最先进入的那一个。
            var found = new List<BallRecord>();
            foreach (var candidate in FindObjectsByType<BallMaterial2D>(FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene != gameObject.scene) continue;
                Rigidbody2D body = candidate.GetComponent<Rigidbody2D>();
                if (body == null || body.GetComponent<PlayerController2D>() != null) continue;
                found.Add(Capture(body));
            }

            if (ball != null && ball.GetComponent<PlayerController2D>() != null) ball = null;
            if (ball != null && !found.Exists(r => r.body == ball)) found.Add(Capture(ball));
            if (ball == null && found.Count > 0) ball = found[0].body;
            records = found.ToArray();
            primary = FindRecord(ball);

            SetVisuals(false);
        }

        private BallRecord Capture(Rigidbody2D body)
        {
            bool useAuthorSpawn = ballSpawn != null && body == ball;
            return new BallRecord
            {
                body = body,
                colliders = body.GetComponentsInChildren<Collider2D>(),
                bodyType = body.bodyType,
                spawn = useAuthorSpawn ? (Vector2)ballSpawn.position : body.position,
                spawnRotation = useAuthorSpawn ? ballSpawn.eulerAngles.z : body.rotation
            };
        }

        private BallRecord FindRecord(Rigidbody2D body)
        {
            if (body == null) return null;
            for (int i = 0; i < records.Length; i++)
            {
                if (records[i].body == body) return records[i];
            }

            return null;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isActiveAndEnabled) return;
            BallRecord record = FindRecord(other.attachedRigidbody);
            if (record == null) return;
            inside = record;
            if (state != GateState.Closed || Time.time < rearmUntil)
            {
                return;
            }

            BeginOpening(record);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!isActiveAndEnabled) return;
            BallRecord record = FindRecord(other.attachedRigidbody);
            if (record == null || record != inside) return;
            inside = null;
            // Nothing was swallowed yet: drop the attempt instead of pulling a ball that left the zone.
            if (state == GateState.Opening || state == GateState.Sucking)
            {
                CloseGate();
            }
        }


        private void BeginOpening(BallRecord record)
        {
            active = record;
            state = GateState.Opening;
            stateTimer = 0f;
            SetVisuals(true);
            ApplyBarrierPassThrough(record, true);
        }

        private void FixedUpdate()
        {
            if (records.Length == 0)
            {
                return;
            }

            if (state == GateState.Closed && inside != null && Time.time >= rearmUntil) BeginOpening(inside);
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
            Rigidbody2D body = active != null ? active.body : null;
            if (inside == null || body == null)
            {
                // The exit callback normally handles this; the check also covers a collider switched off.
                CloseGate();
                return;
            }

            Vector2 offset = HolePosition - body.worldCenterOfMass;
            if (offset.magnitude <= captureRadius)
            {
                BeginSwallow();
                return;
            }

            // Force pull keeps the ball dynamic, so it still collides instead of being teleported inward.
            body.AddForce(offset.normalized * (suctionAcceleration * body.mass), ForceMode2D.Force);
            float speed = body.linearVelocity.magnitude;
            if (speed > maxSuctionSpeed)
            {
                // Only the safety cap rewrites velocity: it is what stops a fast ball from tunnelling.
                body.linearVelocity *= maxSuctionSpeed / speed;
            }

            body.WakeUp();
        }

        private void BeginSwallow()
        {
            Rigidbody2D body = active != null ? active.body : null;
            if (body == null)
            {
                CloseGate();
                return;
            }

            var arm = FindFirstObjectByType<MechanicalArm2D>();
            if (arm != null && arm.GrabbedBody == body) arm.Release();
            state = GateState.Swallowing;
            stateTimer = 0f;
            swallowStart = body.position;
            // Kinematic on the last stretch: no solver impulse, no tunnelling, and GravityBody2D stops pulling.
            body.bodyType = RigidbodyType2D.Kinematic;
            // Unity 6 renamed Rigidbody2D.velocity to linearVelocity.
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.WakeUp();
            BallCaptured?.Invoke(this, body);
        }

        private void Swallow()
        {
            stateTimer += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(stateTimer / swallowSeconds);
            Rigidbody2D body = active != null ? active.body : null;
            if (body == null)
            {
                CloseGate();
                return;
            }

            Vector2 target = Vector2.Lerp(swallowStart, HolePosition, Mathf.SmoothStep(0f, 1f, progress));
            body.MovePosition(target);
            if (progress >= 1f)
            {
                RespawnBall();
                CloseGate();
            }
        }

        /// <summary>Puts the swallowed ball back on its own start pose, used by the gate and by in-place level resets.</summary>
        public void RespawnBall()
        {
            BallRecord record = active ?? primary;
            if (record == null || record.body == null)
            {
                return;
            }

            Rigidbody2D body = record.body;
            body.bodyType = record.bodyType;
            body.position = record.spawn;
            body.rotation = record.spawnRotation;
            // Unity 6 renamed Rigidbody2D.velocity to linearVelocity.
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            // Clear the authored transform too, so interpolated rendering shows the respawn immediately.
            body.transform.SetPositionAndRotation(record.spawn, Quaternion.Euler(0f, 0f, record.spawnRotation));
            body.WakeUp();
            if (inside == record) inside = null;
            BallRespawned?.Invoke(this, body);
        }

        private void CloseGate()
        {
            state = GateState.Closed;
            stateTimer = 0f;
            rearmUntil = Time.time + Mathf.Max(0f, rearmSeconds);
            SetVisuals(false);
            ApplyBarrierPassThrough(active, false);
            active = null;
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

        private void ApplyBarrierPassThrough(BallRecord record, bool passThrough)
        {
            if (record == null || record.colliders == null || barrierColliders == null)
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
                for (int j = 0; j < record.colliders.Length; j++)
                {
                    if (record.colliders[j] != null)
                    {
                        Physics2D.IgnoreCollision(record.colliders[j], barrier, passThrough);
                    }
                }
            }
        }

        private void OnDisable()
        {
            ApplyBarrierPassThrough(active, false);
            if (state == GateState.Swallowing && active != null && active.body != null)
            {
                // A gate switched off mid-swallow must never leave the ball kinematic.
                active.body.bodyType = active.bodyType;
                // Unity 6 renamed Rigidbody2D.velocity to linearVelocity.
                active.body.linearVelocity = Vector2.zero;
                active.body.WakeUp();
            }

            inside = null;
            active = null;
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
