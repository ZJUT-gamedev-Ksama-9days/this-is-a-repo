using System;
using System.Collections.Generic;
using UnityEngine;

namespace GravityPrototype.GameFlow
{
    /// <summary>Why the game is frozen; several reasons can overlap (side menu + settings popup).</summary>
    public enum PauseReason
    {
        None = 0,
        SideMenu = 1,
        PausePopup = 2,
        RecordsPopup = 3,
        SettingsPopup = 4,
        Victory = 5,
        Defeat = 6
    }

    /// <summary>
    /// Owns the frozen state of the run. Freezing stops Time.timeScale, pauses the level timer and
    /// blocks player input, so a menu never adds time to a run. Reasons are counted, which means
    /// closing the settings popup back into the side menu keeps the game paused.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamePauseController : MonoBehaviour
    {
        [SerializeField, Tooltip("暂停时把 Time.timeScale 设为 0，恢复时还原为 activeTimeScale。")]
        private bool freezeTimeScale = true;
        [SerializeField, Min(0.01f), Tooltip("正常游戏使用的时间缩放。")]
        private float activeTimeScale = 1f;

        private readonly HashSet<PauseReason> activeReasons = new HashSet<PauseReason>();
        private LevelController level;
        private LevelTimer timer;
        private PlayerController2D player;
        private bool appliedPaused;
        private float resumeTimeScale = 1f;

        public event Action<bool> PauseStateChanged;

        public bool IsPaused => activeReasons.Count > 0;

        /// <summary>Called by the level flow controller once the level controller of the scene is known.</summary>
        public void Initialize(LevelController levelController)
        {
            level = levelController;
            timer = level != null ? level.Timer : null;
            player = level != null ? level.PlayerMovement : null;
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerController2D>();
            }

            if (IsPaused)
            {
                timer?.Pause();
                if (level != null) level.KeyboardInputBlocked = true;
                SetPlayerBlocked(true);
            }
            if (IsPaused && !appliedPaused)
            {
                RefreshPauseState();
            }
        }

        public bool IsPausedFor(PauseReason reason)
        {
            return activeReasons.Contains(reason);
        }

        public void SetPaused(PauseReason reason, bool paused)
        {
            if (reason == PauseReason.None)
            {
                return;
            }

            bool changed = paused ? activeReasons.Add(reason) : activeReasons.Remove(reason);
            if (!changed)
            {
                return;
            }

            RefreshPauseState();
        }

        /// <summary>Releases every reason, e.g. right before loading another scene or quitting.</summary>
        public void ClearAll()
        {
            if (activeReasons.Count == 0)
            {
                return;
            }

            activeReasons.Clear();
            RefreshPauseState();
        }

        private void RefreshPauseState()
        {
            bool paused = IsPaused;
            if (paused == appliedPaused)
            {
                return;
            }

            appliedPaused = paused;
            if (paused)
            {
                EnterPause();
            }
            else
            {
                ExitPause();
            }

            PauseStateChanged?.Invoke(paused);
        }

        private void EnterPause()
        {
            if (freezeTimeScale)
            {
                resumeTimeScale = Time.timeScale > 0f ? Time.timeScale : activeTimeScale;
                Time.timeScale = 0f;
            }

            if (timer != null)
            {
                timer.Pause();
            }

            if (level != null)
            {
                level.KeyboardInputBlocked = true;
            }

            SetPlayerBlocked(true);
        }

        private void ExitPause()
        {
            if (freezeTimeScale)
            {
                Time.timeScale = resumeTimeScale > 0f ? resumeTimeScale : activeTimeScale;
            }

            if (timer != null)
            {
                timer.Resume();
            }

            if (level != null)
            {
                level.KeyboardInputBlocked = false;
            }

            SetPlayerBlocked(false);
        }

        private void SetPlayerBlocked(bool blocked)
        {
            if (player == null)
            {
                return;
            }

            player.InputBlocked = blocked;
            var tools = player.GetComponent<AstronautToolController>();
            if (tools != null) tools.InputBlocked = blocked;
            var arm = player.GetComponent<MechanicalArm2D>();
            if (blocked && arm != null) arm.Release();
            foreach (var drag in FindObjectsByType<GravityFrameDrag2D>(FindObjectsSortMode.None))
                if (drag.gameObject.scene == player.gameObject.scene)
                    drag.SetCanDrag(!blocked && tools != null && tools.Equipped == AstronautTool.GravityDevice && tools.IsOperational);
            foreach (var pad in FindObjectsByType<BouncePadMouseDrag2D>(FindObjectsSortMode.None))
                if (blocked && pad.gameObject.scene == player.gameObject.scene) pad.CancelDrag();
            if (blocked)
            {
                player.SetInput(0f, false);
            }
        }

        private void OnDestroy()
        {
            // A scene load must never inherit a frozen timeScale from a destroyed level.
            if (freezeTimeScale)
            {
                Time.timeScale = resumeTimeScale > 0f ? resumeTimeScale : activeTimeScale;
            }

            if (level != null)
            {
                level.KeyboardInputBlocked = false;
            }

            SetPlayerBlocked(false);
            activeReasons.Clear();
            appliedPaused = false;
        }
    }
}
