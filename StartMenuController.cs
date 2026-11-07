using GravityPrototype.Progress;
using GravityPrototype.UI;
using UnityEngine;

namespace GravityPrototype
{
    /// <summary>
    /// Drives the start menu scene. The view only raises intents; scene loads, save reads and
    /// Application.Quit happen here, which is the same split <see cref="GameFlow.LevelFlowController"/>
    /// uses for the level screen.
    /// "开始" does not load a scene: it swaps the start panel for <see cref="LevelMenuUI"/> inside the
    /// same scene (SetActive, like every other view). Picking a level there is what loads a scene.
    /// "继续" restores the single save slot through <see cref="SaveGameStore.RequestResumeOnNextLevel"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartMenuController : MonoBehaviour
    {
        [SerializeField] private StartMenuView view;
        [SerializeField, Tooltip("关卡选择面板；留空时按代码创建在当前 Canvas 下。")]
        private LevelMenuUI levelMenu;
        [SerializeField, Tooltip("共享设置面板；留空时按代码创建并挂到同一个 Canvas 下。")]
        private SettingsPopupView settingsPopup;
        [SerializeField, Tooltip("关卡表；关卡面板与存档都靠它换场景名。")]
        private LevelCatalog catalog;
        [SerializeField, Tooltip("关卡表为空时，“开始”回退加载的场景。")]
        private string testLevelScene = "PlatformLevel";
        [SerializeField, Tooltip("进入场景时按存档状态刷新“继续”按钮的可交互性。")]
        private bool refreshContinueOnEnable = true;

        private bool listenersBound;
        private bool levelMenuBound;
        private bool settingsPopupBound;
        private bool transitioning;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindListeners();
            if (refreshContinueOnEnable)
            {
                RefreshContinueAvailability();
            }
        }

        private void OnDisable()
        {
            UnbindListeners();
        }

        private void OnDestroy()
        {
            UnbindListeners();
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }

            if (levelMenu != null && levelMenu.IsVisible)
            {
                ShowStartPanel();
                return;
            }

            if (view != null && view.IsQuitConfirmVisible)
            {
                view.HideQuitConfirm();
                return;
            }

            if (settingsPopup != null && settingsPopup.IsVisible)
            {
                settingsPopup.Hide();
            }
#endif
        }

        private void ResolveReferences()
        {
            if (view == null)
            {
                view = GetComponentInChildren<StartMenuView>(true);
            }

            if (view == null)
            {
                Debug.LogError("StartMenuController on '" + name
                    + "' has no StartMenuView. Add the StartMenuView component to this GameObject or a "
                    + "child that contains the menu nodes.", this);
                return;
            }

            if (levelMenu == null)
            {
                levelMenu = GetComponentInChildren<LevelMenuUI>(true);
            }

            if (settingsPopup == null)
            {
                settingsPopup = GetComponentInChildren<SettingsPopupView>(true);
            }

            if (settingsPopup == null)
            {
                Transform canvas = FindMenuCanvas();
                if (canvas != null)
                {
                    settingsPopup = LevelUiFactory.CreateSettingsPopup(canvas);
                }
            }

            // These can be created after OnEnable, so they are bound here as well as in BindListeners.
            BindLevelMenu();
            BindSettingsPopup();
        }

        private Transform FindMenuCanvas()
        {
            Canvas canvas = view != null ? view.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                canvas = LevelUiFactory.EnsureCanvas();
            }

            return canvas != null ? canvas.transform : null;
        }

        private void BindListeners()
        {
            if (listenersBound || view == null)
            {
                return;
            }

            listenersBound = true;
            view.StartClicked += OnStartClick;
            view.ContinueClicked += OnContinueClick;
            view.SettingsClicked += OnSettingsClick;
            view.QuitClicked += OnQuitClick;
            view.QuitConfirmed += OnConfirmQuitClick;
            view.QuitCancelled += OnCancelQuitClick;

            BindLevelMenu();
            BindSettingsPopup();
        }

        private void BindLevelMenu()
        {
            if (levelMenu == null || levelMenuBound)
            {
                return;
            }

            levelMenu.LevelSelected += OnLevelSelected;
            levelMenu.BackClicked += OnLevelMenuBackClick;
            levelMenuBound = true;
        }

        private void UnbindLevelMenu()
        {
            if (!levelMenuBound)
            {
                return;
            }

            levelMenuBound = false;
            if (levelMenu != null)
            {
                levelMenu.LevelSelected -= OnLevelSelected;
                levelMenu.BackClicked -= OnLevelMenuBackClick;
            }
        }

        private void BindSettingsPopup()
        {
            if (settingsPopup == null || settingsPopupBound)
            {
                return;
            }

            settingsPopup.Closed += HandleSettingsClosed;
            settingsPopupBound = true;
        }

        private void UnbindSettingsPopup()
        {
            if (!settingsPopupBound)
            {
                return;
            }

            settingsPopupBound = false;
            if (settingsPopup != null)
            {
                settingsPopup.Closed -= HandleSettingsClosed;
            }
        }

        private void UnbindListeners()
        {
            if (!listenersBound)
            {
                return;
            }

            listenersBound = false;
            if (view != null)
            {
                view.StartClicked -= OnStartClick;
                view.ContinueClicked -= OnContinueClick;
                view.SettingsClicked -= OnSettingsClick;
                view.QuitClicked -= OnQuitClick;
                view.QuitConfirmed -= OnConfirmQuitClick;
                view.QuitCancelled -= OnCancelQuitClick;
            }

            UnbindLevelMenu();
            UnbindSettingsPopup();
        }

        /// <summary>Enables "继续" only when the single save slot holds a usable payload.</summary>
        public void RefreshContinueAvailability()
        {
            if (view == null)
            {
                return;
            }

            bool hasSave = SaveGameStore.TryLoad(out _);
            view.SetContinueAvailable(hasSave);
        }

        // Button intents -----------------------------------------------------------------------

        /// <summary>"开始": swaps the start panel for the level select panel in this scene.</summary>
        public void OnStartClick()
        {
            if (transitioning)
            {
                return;
            }

            if (view != null)
            {
                view.HideQuitConfirm();
            }

            if (levelMenu == null)
            {
                levelMenu = CreateLevelMenu();
                // A menu created here is not covered by OnEnable, so subscribe to it immediately.
                BindLevelMenu();
            }

            if (levelMenu == null)
            {
                // No canvas to host the panel: fall back to loading the level directly.
                Debug.LogWarning("StartMenuController: the level panel could not be created, so '开始' "
                    + "loads '" + testLevelScene + "' directly.", this);
                LoadScene(testLevelScene);
                return;
            }

            if (view != null)
            {
                view.Hide();
            }

            levelMenu.Show();
        }

        /// <summary>"继续": restores the save slot, or greys the button out when there is none.</summary>
        public void OnContinueClick()
        {
            if (transitioning)
            {
                return;
            }

            if (!SaveGameStore.TryLoad(out SaveGameData save))
            {
                RefreshContinueAvailability();
                Debug.LogWarning("StartMenuController: no usable save; '继续' stays disabled.", this);
                return;
            }

            string sceneName = ResolveSceneName(save);
            SaveGameStore.RequestResumeOnNextLevel();
            LoadScene(sceneName, true);
        }

        /// <summary>"设置": opens the settings popup shared with the level screen.</summary>
        public void OnSettingsClick()
        {
            ResolveReferences();
            if (settingsPopup == null)
            {
                Debug.LogError("StartMenuController: no SettingsPopupView and one could not be created; "
                    + "the settings popup stays closed.", this);
                return;
            }

            settingsPopup.Show();
        }

        /// <summary>"退出游戏": opens the confirmation popup first.</summary>
        public void OnQuitClick()
        {
            if (view == null)
            {
                Debug.LogError("StartMenuController: no StartMenuView, so the quit confirmation cannot open.",
                    this);
                return;
            }

            view.ShowQuitConfirm();
        }

        /// <summary>Confirmation popup "退出": quits the player.</summary>
        public void OnConfirmQuitClick()
        {
            if (view != null)
            {
                view.HideQuitConfirm();
            }

            Debug.Log("StartMenuController: Application.Quit() requested. The Editor ignores it, so stop "
                + "Play Mode manually; a standalone build closes.", this);
            Application.Quit();
        }

        /// <summary>Confirmation popup "取消".</summary>
        public void OnCancelQuitClick()
        {
            if (view != null)
            {
                view.HideQuitConfirm();
            }
        }

        /// <summary>Level panel row: load that level as a fresh run.</summary>
        public void OnLevelSelected(int index)
        {
            if (transitioning)
            {
                return;
            }

            if (levelMenu == null || !levelMenu.TryGetSceneName(index, out string sceneName))
            {
                Debug.LogError("StartMenuController: level index " + index
                    + " has no scene name. Check the level catalog / level panel entries.", this);
                return;
            }

            // A level chosen from the list starts a fresh run unless the save already belongs to it.
            bool saveBelongsToLevel = SaveGameStore.TryLoad(out SaveGameData save)
                && string.Equals(save.levelId, LevelIdOf(index), System.StringComparison.Ordinal);
            if (!saveBelongsToLevel)
            {
                SaveGameStore.ConsumeResumeRequest();
            }

            LoadScene(sceneName);
        }

        /// <summary>Level panel "返回": back to the start panel without leaving the scene.</summary>
        public void OnLevelMenuBackClick()
        {
            ShowStartPanel();
        }

        // --- panel switching -------------------------------------------------------------------

        private void ShowStartPanel()
        {
            if (levelMenu != null)
            {
                levelMenu.Hide();
            }

            if (view != null)
            {
                view.Show();
            }

            RefreshContinueAvailability();
        }

        private LevelMenuUI CreateLevelMenu()
        {
            Transform parent = view != null ? view.transform : transform;
            if (parent == null)
            {
                return null;
            }

            GameObject host = new GameObject("Level Menu", typeof(RectTransform));
            RectTransform rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            LevelMenuUI menu = host.AddComponent<LevelMenuUI>();
            menu.Configure(catalog);
            menu.Hide();
            return menu;
        }

        // --- helpers ---------------------------------------------------------------------------

        private void LoadScene(string sceneName, bool resumeRequested = false)
        {
            transitioning = true;
            if (SceneLoadUtility.TryLoadScene(sceneName))
            {
                return;
            }

            transitioning = false;
            if (resumeRequested)
            {
                // Roll the flag back, otherwise the next level entry would resume by accident.
                SaveGameStore.ConsumeResumeRequest();
            }

            Debug.LogError("StartMenuController: cannot load scene '" + sceneName
                + "'. Add it to Build Settings or fix the level catalog.", this);
        }

        private string LevelIdOf(int index)
        {
            if (catalog == null || !catalog.TryGetLevel(index, out LevelDefinition definition))
            {
                return string.Empty;
            }

            return definition.LevelId;
        }

        private void HandleSettingsClosed()
        {
            if (settingsPopup != null)
            {
                settingsPopup.Hide();
            }
        }

        private string ResolveSceneName(SaveGameData save)
        {
            if (catalog == null || save == null)
            {
                return testLevelScene;
            }

            if (catalog.TryGetIndex(save.levelId, out int index))
            {
                LevelDefinition definition = catalog.GetLevel(index);
                if (definition != null && !string.IsNullOrWhiteSpace(definition.SceneName))
                {
                    return definition.SceneName;
                }
            }

            Debug.LogWarning("StartMenuController: saved level '" + save.levelId
                + "' is not in the level catalog; falling back to '" + testLevelScene + "'.", this);
            return testLevelScene;
        }
    }
}
