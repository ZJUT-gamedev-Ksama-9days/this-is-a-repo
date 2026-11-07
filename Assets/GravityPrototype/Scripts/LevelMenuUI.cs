using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GravityPrototype.Progress;
using GravityPrototype.UI;
using System.Collections.Generic;

namespace GravityPrototype
{
    /// <summary>
    /// Pixel-styled UGUI level selection with shared patch4 settings and records. Each entry lists the persisted best time of its level id, or the
    /// configured "暂无记录" text when the level has never been completed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelMenuUI : MonoBehaviour
    {
        [Serializable]
        public sealed class LevelEntry
        {
            [SerializeField] private string levelId = "Level01";
            [SerializeField] private string displayName = "Level 01";
            [SerializeField] private string sceneName = "PlatformLevel";

            public LevelEntry()
            {
            }

            public LevelEntry(string levelId, string displayName, string sceneName)
            {
                this.levelId = levelId;
                this.displayName = displayName;
                this.sceneName = sceneName;
            }

            public string LevelId => levelId;
            public string DisplayName => displayName;
            public string SceneName => sceneName;
        }

        [SerializeField] private LevelCatalog catalog;
        private RectTransform uiRoot;
        private RectTransform entriesRoot;
        private Button continueButton;
        private SettingsPopupView settings;
        private RecordsPopupView records;

        [SerializeField] private string headline = "GRAVITY FRAME / LEVEL SELECT";
        [SerializeField] private LevelEntry[] levels = Array.Empty<LevelEntry>();
        [SerializeField, Tooltip("没有历史记录时显示的文本。")]
        private string noRecordText = LevelRecordStore.NoRecordText;
        [SerializeField, Tooltip("允许在菜单里清空某个关卡的最高记录。")]
        private bool allowClearingRecords = true;
        [SerializeField] private string footer = "Reach the goal to stop the clock. Best times are saved with PlayerPrefs.";

        private void Start()
        {
            GameSettingsStore.Load();
            Canvas canvas = LevelUiFactory.EnsureCanvas(gameObject.scene);
            uiRoot = LevelUiFactory.CreateRect("Level Select UI", canvas.transform);
            LevelUiFactory.SetStretch(uiRoot, 0f, 0f, 0f, 0f);
            var window = LevelUiFactory.CreatePanel("Level Select Window", uiRoot, LevelUiFactory.PanelColor);
            LevelUiFactory.SetAnchored(window.rectTransform, Vector2.one * .5f, Vector2.one * .5f,
                Vector2.zero, new Vector2(1160f, 820f));
            var title = LevelUiFactory.CreateText("Menu Title", window.transform, headline, 36,
                TextAnchor.MiddleCenter, LevelUiFactory.AccentColor);
            LevelUiFactory.SetTopStretch(title.rectTransform, 84f);

            // A real ScrollRect keeps additional catalog entries clear of the fixed action buttons.
            var scrollRect = LevelUiFactory.CreateRect("Level Scroll", window.transform);
            LevelUiFactory.SetStretch(scrollRect, 32f, 260f, 32f, 100f);
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            var viewport = LevelUiFactory.CreateRect("Viewport", scrollRect);
            LevelUiFactory.SetStretch(viewport, 0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            entriesRoot = LevelUiFactory.CreateRect("Levels", viewport);
            entriesRoot.anchorMin = new Vector2(0f, 1f);
            entriesRoot.anchorMax = Vector2.one;
            entriesRoot.pivot = new Vector2(.5f, 1f);
            entriesRoot.sizeDelta = Vector2.zero;
            var layout = entriesRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlHeight = true; layout.childControlWidth = true;
            layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            var fitter = entriesRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = entriesRoot;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            BuildEntries();

            continueButton = AddButton("Continue Button", window.transform, "继续存档 / CONTINUE",
                new Vector2(0f, -190f), new Vector2(1064f, 64f), ContinueSaved);
            RefreshContinue();
            AddButton("Settings Button", window.transform, "设置", new Vector2(-280f, -274f),
                new Vector2(500f, 64f), () => { records.Hide(); settings.Show(); });
            AddButton("Records Button", window.transform, "查看记录", new Vector2(280f, -274f),
                new Vector2(500f, 64f), ShowRecords);
            var hint = LevelUiFactory.CreateText("Menu Footer", window.transform, footer, 24,
                TextAnchor.MiddleCenter, LevelUiFactory.MutedTextColor);
            LevelUiFactory.SetAnchored(hint.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(0f, 20f), new Vector2(1064f, 64f));
            settings = LevelUiFactory.BuildSettingsPopup(uiRoot);
            records = LevelUiFactory.BuildRecordsPopup(uiRoot);
            settings.Closed += settings.Hide;
            records.CloseClicked += records.Hide;
            settings.Hide(); records.Hide();
            UiFontProvider.Apply(uiRoot);
        }

        private List<LevelEntry> GetEntries()
        {
            var entries = new List<LevelEntry>();
            if (catalog != null && catalog.Count > 0)
            {
                for (int i = 0; i < catalog.Count; i++)
                {
                    var level = catalog.GetLevel(i);
                    if (level != null) entries.Add(new LevelEntry(level.LevelId, level.DisplayName, level.SceneName));
                }
            }
            else if (levels != null)
                foreach (var entry in levels) if (entry != null) entries.Add(entry);
            return entries;
        }

        private void BuildEntries()
        {
            var entries = GetEntries();
            if (entries.Count == 0)
            {
                var empty = LevelUiFactory.CreateText("No Levels", entriesRoot, "暂无关卡", 30,
                    TextAnchor.MiddleCenter, LevelUiFactory.MutedTextColor);
                LevelUiFactory.AddLayoutHeight(empty.gameObject, 100f);
            }
            foreach (var entry in entries)
            {
                var row = LevelUiFactory.CreatePanel("Level " + entry.LevelId, entriesRoot, LevelUiFactory.RowColor);
                LevelUiFactory.AddLayoutHeight(row.gameObject, 128f);
                var label = LevelUiFactory.CreateText("Level Name", row.transform, entry.DisplayName, 36,
                    TextAnchor.MiddleLeft, LevelUiFactory.TextColor);
                LevelUiFactory.SetAnchored(label.rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f),
                    new Vector2(24f, 26f), new Vector2(600f, 48f));
                var best = LevelUiFactory.CreateText("Best Time", row.transform, RecordText(entry), 24,
                    TextAnchor.MiddleLeft, LevelUiFactory.MutedTextColor);
                LevelUiFactory.SetAnchored(best.rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f),
                    new Vector2(24f, -28f), new Vector2(600f, 40f));
                AddButton("Start Button", row.transform, "开始 >", new Vector2(420f, 0f),
                    new Vector2(160f, 72f), () => StartLevel(entry.SceneName)).interactable =
                    SceneLoadUtility.CanLoadScene(entry.SceneName);
                if (allowClearingRecords)
                {
                    var clear = AddButton("Clear Record Button", row.transform, "清除记录", new Vector2(224f, 0f),
                        new Vector2(200f, 72f), () => { LevelRecordStore.ClearRecord(entry.LevelId); best.text = RecordText(entry); });
                    clear.interactable = LevelRecordStore.HasRecord(entry.LevelId);
                    clear.onClick.AddListener(() => clear.interactable = false);
                }
            }
        }

        private string RecordText(LevelEntry entry) => "BEST  " +
            (LevelRecordStore.TryGetBestTime(entry.LevelId, out float best) ? LevelRecordStore.FormatTime(best) : noRecordText);

        private static Button AddButton(string name, Transform parent, string label, Vector2 position,
            Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var button = LevelUiFactory.CreateButton(name, parent, label, size, LevelUiFactory.ButtonColor, out _);
            LevelUiFactory.SetAnchored(button.GetComponent<RectTransform>(), Vector2.one * .5f,
                Vector2.one * .5f, position, size);
            button.onClick.AddListener(action);
            return button;
        }

        private bool TryGetSavedEntry(out LevelEntry entry)
        {
            entry = null;
            if (!SaveGameStore.TryLoad(out var save)) return false;
            foreach (var candidate in GetEntries())
                if (candidate.LevelId == save.levelId && SceneLoadUtility.CanLoadScene(candidate.SceneName))
                { entry = candidate; return true; }
            return false;
        }

        private void RefreshContinue() => continueButton.interactable = TryGetSavedEntry(out _);

        private void ContinueSaved()
        {
            if (!TryGetSavedEntry(out var entry)) { RefreshContinue(); return; }
            SaveGameStore.RequestResumeOnNextLevel();
            StartLevel(entry.SceneName);
        }

        private void ShowRecords()
        {
            settings.Hide();
            var rows = new List<LevelRecordRowData>();
            foreach (var entry in GetEntries())
                rows.Add(new LevelRecordRowData(rows.Count, entry.DisplayName,
                    LevelRecordStore.DescribeBestTime(entry.LevelId), LevelRecordStore.HasRecord(entry.LevelId)));
            records.Show(RecordsPopupMode.Browse, rows);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            { if (settings != null) settings.Hide(); if (records != null) records.Hide(); }
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
