using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityPrototype
{
    /// <summary>
    /// Owns one level run: starts the clock, restarts the level when health reaches zero and stores
    /// the best completion time. Scene reload is used by default because it restores every prop
    /// without extra snapshot code; in-place reset keeps scripted tests and small scenes fast.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelController : MonoBehaviour
    {
        [SerializeField, Tooltip("最高记录使用的关卡标识；留空时使用场景名。")]
        private string levelId = "Level01";
        [SerializeField] private PlayerHealth player;
        [SerializeField] private PlayerController2D playerController;
        [SerializeField] private LevelTimer timer;
        [SerializeField] private LevelGoal2D goal;
        [SerializeField, Tooltip("死亡后重新加载场景；关闭时原地重置角色、计时器和机关。")]
        private bool reloadSceneOnDeath = true;
        [SerializeField, Min(0f), Tooltip("死亡到重开之间的等待时间，秒。")]
        private float deathRestartDelay = 0.8f;
        [SerializeField, Tooltip("允许按 R 重跑当前关卡。")]
        private bool restartWithRKey = true;
        [SerializeField] private bool readKeyboardInput = true;

        [Header("虚空 / 越界（设计文档 2.3.2）")]
        [SerializeField, Tooltip("角色或小球超出相机视野这块距离即视为掉入虚空，米。")]
        private float voidMargin = 0.8f;
        [SerializeField, Tooltip("角色掉入虚空立即死亡（走失败流程）。")]
        private bool playerDiesInVoid = true;
        [SerializeField, Tooltip("小球掉入虚空后闪一下并回到起始点。")]
        private bool ballsReturnFromVoid = true;

        [Header("失败流程")]
        [SerializeField, Tooltip("死亡后自动重开；关闭时由流程层弹失败弹窗（再来一次 / 结束游戏）。")]
        private bool autoRestartOnDeath = true;

        private Camera viewCamera;

        private sealed class BallSpawn
        {
            public Rigidbody2D body;
            public SpriteRenderer renderer;
            public Vector2 position;
            public float rotation;
            public RigidbodyType2D bodyType;
            public bool recovering;
        }

        private BallSpawn[] ballSpawns = new BallSpawn[0];

        private bool deathPending;
        private float restartCountdown;
        private CircuitGate2D[] circuitGates;

        public event Action<LevelController> LevelStarted;
        public event Action<LevelController> LevelCompleted;
        public event Action<LevelController> LevelRespawned;
        public event Action<LevelController> PlayerDied;

        public string LevelId => string.IsNullOrWhiteSpace(levelId) ? gameObject.scene.name : levelId;
        public PlayerHealth Player => player;
        public PlayerController2D PlayerMovement => playerController;
        public LevelTimer Timer => timer;
        public LevelGoal2D Goal => goal;
        public bool KeyboardInputBlocked { get; set; }
        public bool IsCompleted { get; private set; }
        public bool IsDeathPending => deathPending;

        /// <summary>关闭时死亡不再自动重开，由 LevelFlowController 的失败弹窗接管。</summary>
        public bool AutoRestartOnDeath
        {
            get { return autoRestartOnDeath; }
            set { autoRestartOnDeath = value; }
        }
        public float LastRunSeconds { get; private set; }
        public bool LastRunWasBest { get; private set; }
        public float BestSeconds => TryGetBestSeconds(out float seconds) ? seconds : 0f;

        /// <summary>
        /// 电路是否通路（设计文档 2.3.6.3）：场景里的电路门全部接通才算通路，没有电路门的关卡
        /// 一开始就通路。目标门只有在通路时才会被小球打开。
        /// </summary>
        public bool IsCircuitConnected
        {
            get
            {
                if (circuitGates == null)
                {
                    CircuitGate2D[] found = FindObjectsByType<CircuitGate2D>(FindObjectsSortMode.None);
                    var inScene = new System.Collections.Generic.List<CircuitGate2D>(found.Length);
                    for (int i = 0; i < found.Length; i++)
                    {
                        if (found[i] != null && found[i].gameObject.scene == gameObject.scene) inScene.Add(found[i]);
                    }

                    circuitGates = inScene.ToArray();
                }

                if (circuitGates.Length == 0)
                {
                    return true;
                }

                for (int i = 0; i < circuitGates.Length; i++)
                {
                    if (circuitGates[i] == null || !circuitGates[i].IsConnected) return false;
                }

                return true;
            }
        }

        public bool TryGetBestSeconds(out float seconds)
        {
            return LevelRecordStore.TryGetBestTime(LevelId, out seconds);
        }

        private void Awake()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerHealth>();
            }

            if (playerController == null)
            {
                playerController = player != null
                    ? player.GetComponent<PlayerController2D>()
                    : FindFirstObjectByType<PlayerController2D>();
            }

            if (timer == null)
            {
                timer = FindFirstObjectByType<LevelTimer>();
            }

            if (goal == null)
            {
                goal = FindFirstObjectByType<LevelGoal2D>();
            }

            viewCamera = Camera.main;
            CaptureBallSpawns();
        }

        private void CaptureBallSpawns()
        {
            BallMaterial2D[] balls = FindObjectsByType<BallMaterial2D>(FindObjectsSortMode.None);
            var list = new System.Collections.Generic.List<BallSpawn>(balls.Length);
            for (int i = 0; i < balls.Length; i++)
            {
                if (balls[i] == null || balls[i].gameObject.scene != gameObject.scene) continue;
                Rigidbody2D body = balls[i].GetComponent<Rigidbody2D>();
                if (body == null) continue;
                list.Add(new BallSpawn
                {
                    body = body,
                    renderer = balls[i].GetComponent<SpriteRenderer>(),
                    position = body.position,
                    rotation = body.rotation,
                    bodyType = body.bodyType
                });
            }

            ballSpawns = list.ToArray();
        }

        private void OnEnable()
        {
            if (player != null)
            {
                player.Died += HandlePlayerDied;
            }

            if (goal != null)
            {
                goal.Reached += HandleGoalReached;
            }
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.Died -= HandlePlayerDied;
            }

            if (goal != null)
            {
                goal.Reached -= HandleGoalReached;
            }
        }

        private void Start()
        {
            BeginRun();
        }

        private void Update()
        {
            if (deathPending)
            {
                if (!autoRestartOnDeath)
                {
                    // 失败弹窗接管：等到玩家点“再来一次”再重开。
                    return;
                }

                restartCountdown -= Time.deltaTime;
                if (restartCountdown <= 0f)
                {
                    RestartLevel();
                }

                return;
            }

            CheckVoid();

            if (!KeyboardInputBlocked && readKeyboardInput && restartWithRKey && Input.GetKeyDown(KeyCode.R))
            {
                RestartLevel();
            }
        }

        /// <summary>Starts a fresh run without touching the scene layout.</summary>
        public void BeginRun()
        {
            deathPending = false;
            restartCountdown = 0f;
            IsCompleted = false;
            LastRunSeconds = 0f;
            LastRunWasBest = false;

            if (player != null)
            {
                player.ResetHealth();
            }

            if (playerController != null)
            {
                playerController.enabled = true;
                playerController.ResetToSpawn();
                var tools = playerController.GetComponent<AstronautToolController>();
                if (tools != null) tools.ResetTools();
            }

            if (goal != null)
            {
                goal.ResetGoal();
            }

            if (timer != null)
            {
                timer.BeginRun();
            }

            LevelStarted?.Invoke(this);
        }

        /// <summary>Re-enters the level: reloads the scene when possible, otherwise resets in place.</summary>
        public void RestartLevel()
        {
            if (CanReloadScene())
            {
                SceneManager.LoadScene(gameObject.scene.name);
                return;
            }

            ResetLevelInPlace();
        }

        /// <summary>Resets the player, timer, goal, pads and spike contacts without a scene reload.</summary>
        public void ResetLevelInPlace()
        {
            if (player != null)
            {
                player.ResetHealth();
            }

            if (playerController != null)
            {
                playerController.enabled = true;
                playerController.ResetToSpawn();
                var tools = playerController.GetComponent<AstronautToolController>();
                if (tools != null) tools.ResetTools();
            }

            GravityBouncePad2D[] pads = FindObjectsByType<GravityBouncePad2D>(FindObjectsSortMode.None);
            for (int i = 0; i < pads.Length; i++)
            {
                if (pads[i].gameObject.scene == gameObject.scene) pads[i].ReturnToHome();
            }

            SpikeHazard2D[] hazards = FindObjectsByType<SpikeHazard2D>(FindObjectsSortMode.None);
            for (int i = 0; i < hazards.Length; i++)
            {
                if (hazards[i].gameObject.scene == gameObject.scene) hazards[i].ClearContacts();
            }

            foreach (var lever in FindObjectsByType<LeverMechanism2D>(FindObjectsSortMode.None))
                if (lever.gameObject.scene == gameObject.scene) lever.ResetLever();
            // 电路门和电闸无关：只有电路门会断开电路，电闸只是装饰。
            foreach (var gate in FindObjectsByType<CircuitGate2D>(FindObjectsSortMode.None))
                if (gate.gameObject.scene == gameObject.scene) gate.Disconnect();
            circuitGates = null;

            deathPending = false;
            restartCountdown = 0f;
            IsCompleted = false;
            LastRunSeconds = 0f;
            LastRunWasBest = false;
            if (goal != null)
            {
                goal.ResetGoal();
            }

            if (timer != null)
            {
                timer.BeginRun();
            }

            LevelStarted?.Invoke(this);
            LevelRespawned?.Invoke(this);
        }

        private void HandlePlayerDied(PlayerHealth deadPlayer)
        {
            if (IsCompleted || deathPending)
            {
                return;
            }

            deathPending = true;
            restartCountdown = Mathf.Max(0f, deathRestartDelay);
            if (timer != null)
            {
                timer.ResetTimer();
            }

            // Stop accepting input immediately; BeginRun/ResetLevelInPlace re-enables the controller.
            if (playerController != null)
            {
                playerController.enabled = false;
            }

            PlayerDied?.Invoke(this);
        }

        // --- 虚空 / 越界 ---------------------------------------------------------------

        private void CheckVoid()
        {
            if (IsCompleted) return;
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) return;

            if (playerDiesInVoid && player != null && !player.IsDead && playerController != null
                && IsOutsideView(playerController.transform.position))
            {
                player.Kill(PlayerDamageSource.Void);
                return;
            }

            if (!ballsReturnFromVoid) return;
            for (int i = 0; i < ballSpawns.Length; i++)
            {
                BallSpawn spawn = ballSpawns[i];
                if (spawn.recovering || spawn.body == null) continue;
                if (!IsOutsideView(spawn.body.position)) continue;
                spawn.recovering = true;
                StartCoroutine(RecoverBallFromVoid(spawn));
            }
        }

        /// <summary>相机视野外（带余量）即虚空；相机固定，所以直接用正交尺寸算矩形。</summary>
        private bool IsOutsideView(Vector2 worldPoint)
        {
            Vector2 center = viewCamera.transform.position;
            float halfHeight = (viewCamera.orthographic ? viewCamera.orthographicSize : 5f) + voidMargin;
            float halfWidth = halfHeight * Mathf.Max(0.1f, viewCamera.aspect);
            return Mathf.Abs(worldPoint.x - center.x) > halfWidth
                || Mathf.Abs(worldPoint.y - center.y) > halfHeight;
        }

        private System.Collections.IEnumerator RecoverBallFromVoid(BallSpawn spawn)
        {
            Rigidbody2D body = spawn.body;
            if (body == null) { spawn.recovering = false; yield break; }

            if (spawn.renderer != null) spawn.renderer.enabled = false;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            yield return new WaitForSeconds(0.12f);

            if (body != null)
            {
                body.bodyType = spawn.bodyType;
                body.position = spawn.position;
                body.rotation = spawn.rotation;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.transform.SetPositionAndRotation(spawn.position, Quaternion.Euler(0f, 0f, spawn.rotation));
                body.WakeUp();
            }

            yield return new WaitForSeconds(0.08f);
            if (spawn.renderer != null) spawn.renderer.enabled = true;
            if (body != null) body.WakeUp();
            spawn.recovering = false;
        }

        private void HandleGoalReached(LevelGoal2D reachedGoal)
        {
            if (IsCompleted || deathPending)
            {
                return;
            }

            IsCompleted = true;
            LastRunSeconds = timer != null ? timer.Stop() : 0f;
            LastRunWasBest = LastRunSeconds > 0f && LevelRecordStore.SubmitTime(LevelId, LastRunSeconds);
            LevelCompleted?.Invoke(this);
        }

        private bool CanReloadScene()
        {
            if (!reloadSceneOnDeath)
            {
                return false;
            }

            Scene scene = gameObject.scene;
            return scene.IsValid() && !string.IsNullOrEmpty(scene.name)
                && Application.CanStreamedLevelBeLoaded(scene.name);
        }
    }
}
