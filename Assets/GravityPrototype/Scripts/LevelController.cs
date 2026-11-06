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

        private bool deathPending;
        private float restartCountdown;

        public event Action<LevelController> LevelStarted;
        public event Action<LevelController> LevelCompleted;
        public event Action<LevelController> LevelRespawned;

        public string LevelId => string.IsNullOrWhiteSpace(levelId) ? gameObject.scene.name : levelId;
        public PlayerHealth Player => player;
        public PlayerController2D PlayerMovement => playerController;
        public LevelTimer Timer => timer;
        public LevelGoal2D Goal => goal;
        public bool KeyboardInputBlocked { get; set; }
        public bool IsCompleted { get; private set; }
        public bool IsDeathPending => deathPending;
        public float LastRunSeconds { get; private set; }
        public bool LastRunWasBest { get; private set; }
        public float BestSeconds => TryGetBestSeconds(out float seconds) ? seconds : 0f;

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
                restartCountdown -= Time.deltaTime;
                if (restartCountdown <= 0f)
                {
                    RestartLevel();
                }

                return;
            }

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
