using System;
using System.Collections;
using System.Collections.Generic;
using GravityPrototype.Progress;
using GravityPrototype.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityPrototype.GameFlow
{
    /// <summary>
    /// Orchestrates the level screen: side menu, pause popup, records popup, single save slot, victory
    /// sequence and level progression. Views only raise intents; all state changes and scene loads
    /// happen here, and every transition goes through <see cref="GamePauseController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelFlowController : MonoBehaviour
    {
        [SerializeField] private LevelController level;
        [SerializeField] private LevelCatalog catalog;
        [SerializeField] private GamePauseController pauseController;
        [SerializeField, Min(0.2f), Tooltip("胜利特效播放时长，秒；结束后才显示记录弹窗。")]
        private float victoryEffectSeconds = 2.4f;
        [SerializeField, Tooltip("场景里没有 UI 视图时按代码创建 Canvas UI。")]
        private bool buildUiWhenMissing = true;
        [SerializeField, Tooltip("进入关卡时若存档指向当前关卡，则恢复计时。")]
        private bool resumeFromSaveOnLoad = true;
        [SerializeField, Tooltip("侧边菜单“返回”要回到的开始界面场景。")]
        private string startMenuSceneName = DefaultStartMenuSceneName;
        [SerializeField, Tooltip("点侧边菜单“返回”时先写入存档槽，让开始界面的“继续”能接上。")]
        private bool saveBeforeReturningHome = true;

        /// <summary>Scene name of the start menu; it is index 0 of Build Settings.</summary>
        public const string DefaultStartMenuSceneName = "StartMenu";

        private LevelHudView hud;
        private SideMenuView sideMenu;
        private PausePopupView pausePopup;
        private RecordsPopupView recordsPopup;
        private SettingsPopupView settingsPopup;
        private VictoryEffectView victoryEffect;
        private ToastView toast;

        private int currentLevelIndex = -1;
        private bool resumePending;
        private bool runCompleted;
        private bool viewsBound;
        private string lastTimeText;

        public LevelController Level => level;
        public int CurrentLevelIndex => currentLevelIndex;
        public string CurrentLevelId => level != null ? level.LevelId : string.Empty;

        private void Awake()
        {
            ResolveReferences();
            ResolveViews();
            BindViewEvents();
            if (level != null)
            {
                level.LevelStarted += HandleLevelStarted;
                level.LevelCompleted += HandleLevelCompleted;
            }

            if (pauseController != null) pauseController.Initialize(level);
            resumePending = resumeFromSaveOnLoad && SaveGameStore.ConsumeResumeRequest();
            if (hud != null)
            {
                hud.SetLevelText(BuildLevelTitle());
            }

            HideAllOverlays();
        }

        private void Start()
        {
            if (pauseController != null)
            {
                pauseController.Initialize(level);
            }

            GameSettingsStore.Load();
            RefreshHudTime();
        }

        private void OnDestroy()
        {
            if (level != null)
            {
                level.LevelStarted -= HandleLevelStarted;
                level.LevelCompleted -= HandleLevelCompleted;
            }

            StopAllCoroutines();
        }

        private void Update()
        {
            RefreshHud();
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleEscape();
            }
#endif
        }

        private void ResolveReferences()
        {
            if (level == null)
            {
                level = FindFirstObjectByType<LevelController>();
            }

            if (pauseController == null)
            {
                pauseController = GetComponentInChildren<GamePauseController>(true);
            }

            if (pauseController == null)
            {
                pauseController = FindFirstObjectByType<GamePauseController>();
            }

            if (pauseController == null) pauseController = gameObject.AddComponent<GamePauseController>();
            if (level != null)
            {
                currentLevelIndex = 0;
                if (catalog != null && catalog.TryGetIndex(level.LevelId, out int foundIndex))
                {
                    currentLevelIndex = foundIndex;
                }
            }
        }

        private void ResolveViews()
        {
            hud = GetComponentInChildren<LevelHudView>(true);
            sideMenu = GetComponentInChildren<SideMenuView>(true);
            pausePopup = GetComponentInChildren<PausePopupView>(true);
            recordsPopup = GetComponentInChildren<RecordsPopupView>(true);
            settingsPopup = GetComponentInChildren<SettingsPopupView>(true);
            victoryEffect = GetComponentInChildren<VictoryEffectView>(true);
            toast = GetComponentInChildren<ToastView>(true);

            bool complete = hud != null && sideMenu != null && pausePopup != null && recordsPopup != null
                && settingsPopup != null && victoryEffect != null && toast != null;
            if (complete || !buildUiWhenMissing)
            {
                return;
            }

            Canvas canvas = LevelUiFactory.EnsureCanvas();
            LevelUiParts parts = LevelUiFactory.Build(canvas.transform);
            hud = parts.Hud;
            sideMenu = parts.SideMenu;
            pausePopup = parts.PausePopup;
            recordsPopup = parts.RecordsPopup;
            settingsPopup = parts.SettingsPopup;
            victoryEffect = parts.VictoryEffect;
            toast = parts.Toast;
        }

        private void BindViewEvents()
        {
            if (viewsBound)
            {
                return;
            }

            viewsBound = true;
            if (hud != null)
            {
                hud.MenuClicked += HandleMenuClicked;
                hud.PauseClicked += HandlePauseClicked;
            }

            if (sideMenu != null)
            {
                sideMenu.SettingsClicked += HandleSettingsClicked;
                sideMenu.RecordsClicked += HandleRecordsClicked;
                sideMenu.SaveClicked += HandleSaveClicked;
                sideMenu.QuitClicked += HandleQuitClicked;
                sideMenu.HomeClicked += HandleHomeClicked;
                sideMenu.CloseClicked += HandleSideMenuClosed;
            }

            if (pausePopup != null)
            {
                pausePopup.ResumeClicked += HandleResumeClicked;
                pausePopup.QuitClicked += HandleQuitClicked;
            }

            if (recordsPopup != null)
            {
                recordsPopup.PrimaryClicked += HandleRecordsPrimaryClicked;
                recordsPopup.QuitClicked += HandleQuitClicked;
                recordsPopup.CloseClicked += HandleRecordsCloseClicked;
            }

            if (settingsPopup != null)
            {
                settingsPopup.Closed += HandleSettingsClosed;
            }
        }

        private void HideAllOverlays()
        {
            if (sideMenu != null)
            {
                sideMenu.Show(false);
            }

            pausePopup?.Hide();
            recordsPopup?.Hide();
            settingsPopup?.Hide();
            victoryEffect?.Hide();
            toast?.Hide();
        }

        // --- side menu -------------------------------------------------------------------------

        private void HandleMenuClicked()
        {
            if (runCompleted || sideMenu == null)
            {
                return;
            }

            sideMenu.Show(true);
            SetPause(PauseReason.SideMenu, true);
        }

        private void HandleSideMenuClosed()
        {
            if (sideMenu != null)
            {
                sideMenu.Show(false);
            }

            SetPause(PauseReason.SideMenu, false);
        }

        private void HandleSettingsClicked()
        {
            if (sideMenu != null)
            {
                sideMenu.Show(false);
            }

            OpenSettings();
            SetPause(PauseReason.SideMenu, false);
        }

        private void OpenSettings()
        {
            if (settingsPopup == null)
            {
                return;
            }

            settingsPopup.Show();
            SetPause(PauseReason.SettingsPopup, true);
        }

        private void HandleSettingsClosed()
        {
            settingsPopup?.Hide();
            if (!runCompleted && sideMenu != null)
            {
                // Return to the side menu so the player lands where they came from.
                sideMenu.Show(true);
                SetPause(PauseReason.SideMenu, true);
            }

            SetPause(PauseReason.SettingsPopup, false);
        }

        private void HandlePauseClicked()
        {
            if (sideMenu != null)
            {
                sideMenu.Show(false);
            }

            if (pausePopup != null)
            {
                pausePopup.Show();
                SetPause(PauseReason.PausePopup, true);
            }

            SetPause(PauseReason.SideMenu, false);
        }

        private void HandleResumeClicked()
        {
            pausePopup?.Hide();
            SetPause(PauseReason.PausePopup, false);
        }

        private void HandleRecordsClicked()
        {
            if (sideMenu != null)
            {
                sideMenu.Show(false);
            }

            ShowRecords(RecordsPopupMode.Browse);
            SetPause(PauseReason.SideMenu, false);
        }

        private void HandleSaveClicked()
        {
            SaveCurrentRun();
        }

        private void HandleRecordsCloseClicked()
        {
            recordsPopup?.Hide();
            SetPause(PauseReason.RecordsPopup, false);
        }

        private void HandleRecordsPrimaryClicked()
        {
            RecordsPopupMode mode = recordsPopup != null ? recordsPopup.Mode : RecordsPopupMode.Browse;
            recordsPopup?.Hide();
            SetPause(PauseReason.RecordsPopup, false);

            if (mode == RecordsPopupMode.LevelVictory)
            {
                GoToNextLevel();
            }
            else if (mode == RecordsPopupMode.FinalVictory)
            {
                LoadLevel(0);
            }
        }

        private void HandleQuitClicked()
        {
            QuitGame();
        }

        /// <summary>
        /// Side menu "返回": close the menu first (so it is not left open across the load), write the
        /// run into the save slot so "继续" in the start menu can pick it up, then go back.
        /// </summary>
        private void HandleHomeClicked()
        {
            if (sideMenu != null && sideMenu.IsVisible)
            {
                sideMenu.Show(false);
            }

            SetPause(PauseReason.SideMenu, false);

            // Silent: the toast would be destroyed by the scene load in the same frame anyway.
            if (saveBeforeReturningHome && !SaveCurrentRun(false))
            {
                Debug.LogWarning("LevelFlowController: there is no level to save, so '返回' left the "
                    + "save slot untouched.", this);
            }

            GoToStartMenu();
        }

        private void HandleEscape()
        {
            if (runCompleted)
            {
                return;
            }

            if (settingsPopup != null && settingsPopup.IsVisible)
            {
                HandleSettingsClosed();
                return;
            }

            if (recordsPopup != null && recordsPopup.IsVisible)
            {
                HandleRecordsCloseClicked();
                return;
            }

            if (pausePopup != null && pausePopup.IsVisible)
            {
                HandleResumeClicked();
                return;
            }

            if (sideMenu != null && sideMenu.IsVisible)
            {
                HandleSideMenuClosed();
                return;
            }

            HandlePauseClicked();
        }

        // --- records ---------------------------------------------------------------------------

        private void ShowRecords(RecordsPopupMode mode)
        {
            if (recordsPopup == null)
            {
                return;
            }

            recordsPopup.Show(mode, BuildRecordRows());
            SetPause(PauseReason.RecordsPopup, true);
        }

        private IReadOnlyList<LevelRecordRowData> BuildRecordRows()
        {
            List<LevelRecordRowData> rows = new List<LevelRecordRowData>();
            if (catalog == null || catalog.Count == 0)
            {
                if (level != null)
                {
                    rows.Add(new LevelRecordRowData(0, BuildLevelTitle(),
                        LevelRecords.GetRecordDisplay(level.LevelId),
                        LevelRecords.TryGetRecord(level.LevelId, out _)));
                }

                return rows;
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                LevelDefinition definition = catalog.GetLevel(i);
                if (definition == null)
                {
                    continue;
                }

                bool hasRecord = LevelRecords.TryGetRecord(definition.LevelId, out _);
                rows.Add(new LevelRecordRowData(i, definition.DisplayName,
                    LevelRecords.GetRecordDisplay(definition.LevelId), hasRecord));
            }

            return rows;
        }

        // --- victory ---------------------------------------------------------------------------

        private void HandleLevelCompleted(LevelController completed)
        {
            if (runCompleted)
            {
                return;
            }

            runCompleted = true;
            if (hud != null)
            {
                hud.SetMenuInteractable(false);
            }

            if (sideMenu != null && sideMenu.IsVisible)
            {
                sideMenu.Show(false);
                SetPause(PauseReason.SideMenu, false);
            }

            SetPause(PauseReason.Victory, true);
            RefreshHudTime();
            StartCoroutine(PlayVictorySequence());
        }

        private IEnumerator PlayVictorySequence()
        {
            bool finalLevel = catalog != null && catalog.IsLastLevel(currentLevelIndex);
            string title = finalLevel ? "全部关卡完成" : "关卡完成";
            string subtitle = BuildVictorySubtitle();
            victoryEffect?.Play(title, subtitle, victoryEffectSeconds);
            yield return new WaitForSecondsRealtime(victoryEffectSeconds);
            victoryEffect?.Hide();
            ShowRecords(finalLevel ? RecordsPopupMode.FinalVictory : RecordsPopupMode.LevelVictory);
        }

        private string BuildVictorySubtitle()
        {
            if (level == null)
            {
                return string.Empty;
            }

            string time = LevelRecords.FormatRecord(level.LastRunSeconds);
            return level.LastRunWasBest ? time + " · 新纪录" : time;
        }

        // --- save / load -----------------------------------------------------------------------

        /// <summary>
        /// Writes the current run into the single save slot. Returns false when there is no level to
        /// save from, so an auto-save caller can report that instead of failing silently.
        /// </summary>
        private bool SaveCurrentRun(bool notify = true)
        {
            if (level == null)
            {
                return false;
            }

            float elapsed = runCompleted
                ? level.LastRunSeconds
                : (level.Timer != null ? level.Timer.ElapsedSeconds : 0f);
            int unlocked = catalog != null
                ? LevelRecords.FindHighestUnlockedIndex(catalog)
                : Mathf.Max(currentLevelIndex, 0);
            SaveGameData data = SaveGameData.Create(Mathf.Max(currentLevelIndex, 0), level.LevelId,
                elapsed, unlocked);
            SaveGameStore.Write(data);
            if (notify)
            {
                ShowToast("存档已保存 · " + LevelRecords.FormatRecord(elapsed));
            }

            return true;
        }

        private void HandleLevelStarted(LevelController started)
        {
            runCompleted = false;
            if (hud != null)
            {
                hud.SetMenuInteractable(true);
            }

            if (pauseController != null && pauseController.IsPaused)
            {
                started.Timer?.Pause();
                started.KeyboardInputBlocked = true;
                if (started.PlayerMovement != null) started.PlayerMovement.InputBlocked = true;
            }
            RefreshHudTime();
            if (!resumePending)
            {
                return;
            }

            resumePending = false;
            if (!SaveGameStore.TryLoad(out SaveGameData save))
            {
                return;
            }

            if (!string.Equals(save.levelId, started.LevelId, StringComparison.Ordinal))
            {
                ShowToast("存档属于其他关卡，未恢复计时");
                return;
            }

            if (started.Timer != null)
            {
                started.Timer.ResumeRunAt(save.elapsedSeconds);
                if (pauseController != null && pauseController.IsPaused) started.Timer.Pause();
            }

            ShowToast("已恢复存档 · " + LevelRecords.FormatRecord(save.elapsedSeconds));
            RefreshHudTime();
        }

        private void GoToNextLevel()
        {
            int nextIndex = Mathf.Max(currentLevelIndex, 0) + 1;
            if (catalog == null || !catalog.TryGetLevel(nextIndex, out _))
            {
                ShowToast("没有下一关，重新开始第一关");
                LoadLevel(0);
                return;
            }

            LoadLevel(nextIndex);
        }

        private void LoadLevel(int index)
        {
            if (catalog == null || !catalog.TryGetLevel(index, out LevelDefinition target))
            {
                ShowToast("关卡数据缺失，无法加载");
                return;
            }

            if (!SceneLoadUtility.CanLoadScene(target.SceneName))
            {
                ShowToast("场景未加入 Build Settings：" + target.SceneName);
                return;
            }

            ReleasePauseBeforeSceneChange();
            // SceneManager is the only supported way to switch levels; Application.LoadLevel is gone.
            SceneManager.LoadScene(target.SceneName);
        }

        private void QuitGame()
        {
            ReleasePauseBeforeSceneChange();
#if UNITY_EDITOR
            // Unity 6 keeps EditorApplication.ExitPlaymode() as the explicit way to leave Play mode.
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        private void GoToStartMenu()
        {
            string sceneName = string.IsNullOrWhiteSpace(startMenuSceneName)
                ? DefaultStartMenuSceneName
                : startMenuSceneName.Trim();
            if (!SceneLoadUtility.CanLoadScene(sceneName))
            {
                ShowToast("场景未加入 Build Settings：" + sceneName);
                Debug.LogError("LevelFlowController: the start menu scene '" + sceneName
                    + "' is not in Build Settings, so '返回' did nothing.", this);
                return;
            }

            ReleasePauseBeforeSceneChange();
            SceneManager.LoadScene(sceneName);
        }

        private void ReleasePauseBeforeSceneChange()
        {
            pauseController?.ClearAll();
        }

        // --- helpers ---------------------------------------------------------------------------

        private void SetPause(PauseReason reason, bool paused)
        {
            pauseController?.SetPaused(reason, paused);
        }

        private void RefreshHud()
        {
            RefreshHudTime();
        }

        private void RefreshHudTime()
        {
            if (hud == null || level == null)
            {
                return;
            }

            float seconds = level.IsCompleted
                ? level.LastRunSeconds
                : (level.Timer != null ? level.Timer.ElapsedSeconds : 0f);
            string text = LevelRecords.FormatRecord(seconds);
            if (string.Equals(text, lastTimeText, StringComparison.Ordinal))
            {
                return;
            }

            lastTimeText = text;
            hud.SetTimeText(text);
        }

        private string BuildLevelTitle()
        {
            if (catalog != null && catalog.TryGetLevel(currentLevelIndex, out LevelDefinition definition))
            {
                return definition.DisplayName;
            }

            return level != null ? level.LevelId : string.Empty;
        }

        private void ShowToast(string message)
        {
            if (toast != null)
            {
                toast.Show(message);
            }
            else
            {
                Debug.Log(message);
            }
        }
    }
}
