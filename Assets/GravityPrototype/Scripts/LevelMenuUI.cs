using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GravityPrototype.Progress;
using GravityPrototype.UI;

namespace GravityPrototype
{
    /// <summary>
    /// 开始界面（设计文档 2.8.2.1）：开始 / 继续 / 设置 / 退出游戏。
    /// “开始”进入第一关，“继续”读取单档存档回到上次关卡，“设置”打开共享设置弹窗。
    /// 视图全部按代码生成，场景里只需要挂这个组件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelMenuUI : MonoBehaviour
    {
        [SerializeField] private LevelCatalog catalog;
        [SerializeField] private string headline = "GRAVITY FRAME";
        [SerializeField] private string footer = "拖动重力框改变重力 · WASD 移动角色 · 小球开门";

        private RectTransform uiRoot;
        private Button continueButton;
        private SettingsPopupView settings;

        private void Start()
        {
            GameSettingsStore.Load();
            Canvas canvas = LevelUiFactory.EnsureCanvas(gameObject.scene);
            uiRoot = LevelUiFactory.CreateRect("Level Select UI", canvas.transform);
            LevelUiFactory.SetStretch(uiRoot, 0f, 0f, 0f, 0f);

            // 整屏压暗，避免菜单直接盖在关卡背景上。
            Image backdrop = LevelUiFactory.CreatePanel("Menu Backdrop", uiRoot, LevelUiFactory.DimColor);
            LevelUiFactory.SetStretch(backdrop.rectTransform, 0f, 0f, 0f, 0f);

            Image window = LevelUiFactory.CreatePanel("Start Window", uiRoot, LevelUiFactory.PanelColor);
            LevelUiFactory.SetAnchored(window.rectTransform, Vector2.one * .5f, Vector2.one * .5f,
                Vector2.zero, new Vector2(760f, 720f));

            Text title = LevelUiFactory.CreateText("Menu Title", window.transform, headline, 52,
                TextAnchor.MiddleCenter, LevelUiFactory.AccentColor, FontStyle.Bold);
            LevelUiFactory.SetAnchored(title.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(0f, -72f), new Vector2(680f, 88f));

            AddButton("Start Button", window.transform, "开始", new Vector2(0f, 40f), () => StartFirstLevel());
            continueButton = AddButton("Continue Button", window.transform, "继续存档", new Vector2(0f, -56f),
                ContinueSaved);
            AddButton("Settings Button", window.transform, "设置", new Vector2(0f, -152f),
                () => { settings.Hide(); settings.Show(); });
            AddButton("Quit Button", window.transform, "退出游戏", new Vector2(0f, -248f), QuitGame);
            RefreshContinue();

            Text hint = LevelUiFactory.CreateText("Menu Footer", window.transform, footer, 24,
                TextAnchor.MiddleCenter, LevelUiFactory.MutedTextColor);
            LevelUiFactory.SetAnchored(hint.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(0f, 28f), new Vector2(680f, 64f));

            settings = LevelUiFactory.BuildSettingsPopup(uiRoot);
            settings.Closed += settings.Hide;
            settings.Hide();
            UiFontProvider.Apply(uiRoot);
        }

        private static Button AddButton(string name, Transform parent, string label, Vector2 position,
            UnityEngine.Events.UnityAction action)
        {
            Vector2 size = new Vector2(520f, 76f);
            Button button = LevelUiFactory.CreateButton(name, parent, label, size, LevelUiFactory.ButtonColor, out _);
            LevelUiFactory.SetAnchored(button.GetComponent<RectTransform>(), Vector2.one * .5f,
                Vector2.one * .5f, position, size);
            button.onClick.AddListener(action);
            return button;
        }

        /// <summary>“开始”：进入目录里的第一关。</summary>
        private void StartFirstLevel()
        {
            if (catalog != null && catalog.TryGetLevel(0, out LevelDefinition first))
            {
                StartLevel(first.SceneName);
                return;
            }

            Debug.LogWarning("开始界面没有配置关卡目录（LevelCatalog）。");
        }

        /// <summary>“继续”：读取单档存档，回到上次的关卡并恢复计时。</summary>
        private void ContinueSaved()
        {
            if (!TryGetSavedLevel(out LevelDefinition entry)) { RefreshContinue(); return; }
            SaveGameStore.RequestResumeOnNextLevel();
            StartLevel(entry.SceneName);
        }

        private bool TryGetSavedLevel(out LevelDefinition entry)
        {
            entry = null;
            if (catalog == null || !SaveGameStore.TryLoad(out SaveGameData save)) return false;
            if (!catalog.TryGetIndex(save.levelId, out int index) || !catalog.TryGetLevel(index, out entry)) return false;
            return entry != null && SceneLoadUtility.CanLoadScene(entry.SceneName);
        }

        private void RefreshContinue()
        {
            if (continueButton != null) continueButton.interactable = TryGetSavedLevel(out _);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && settings != null && settings.IsVisible)
            {
                settings.Hide();
            }
        }

        private void OnDestroy()
        {
            if (uiRoot != null) Destroy(uiRoot.gameObject);
        }

        private void StartLevel(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning("Scene '" + sceneName + "' is not in Build Settings; add it before starting the level.");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}
