using System;
using System.Collections.Generic;
using GravityPrototype.Progress;
using GravityPrototype.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GravityPrototype
{
    /// <summary>
    /// Level select screen. It used to draw itself with OnGUI; it now builds a Canvas-based panel so the
    /// start menu can swap it in and out with SetActive next to StartMenuView. The serialized fields keep
    /// their old names, so an existing LevelMenu scene keeps its configuration. Like every other view it
    /// only raises intents — the owning controller decides what a click means and loads the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelMenuUI : MonoBehaviour
    {
        /// <summary>One row of the level table; kept compatible with the old OnGUI version.</summary>
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
            public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? levelId : displayName;
            public string SceneName => sceneName;
        }

        /// <summary>Node names used by the code-built panel and by the name lookup fallback.</summary>
        public const string PanelObjectName = "LevelMenuPanel";
        public const string TitleObjectName = "LevelMenu Title";
        public const string FooterObjectName = "LevelMenu Footer";
        public const string RowsObjectName = "Level Rows";
        public const string BackButtonObjectName = "Btn_Back";
        public const string RowNameObjectName = "Level Name";
        public const string RowRecordObjectName = "Level Record";
        public const string RowClearButtonObjectName = "Btn_Clear";

        private static readonly Vector2 PanelSize = new Vector2(1280f, 800f);
        private const float RowHeight = 84f;

        [SerializeField] private string headline = "GRAVITY FRAME / LEVEL SELECT";
        [SerializeField, Tooltip("关卡表；为空时改用下方的 catalog。")]
        private LevelEntry[] levels = Array.Empty<LevelEntry>();
        [SerializeField, Tooltip("没有历史记录时显示的文本。")]
        private string noRecordText = LevelRecordStore.NoRecordText;
        [SerializeField, Tooltip("允许在菜单里清空某个关卡的最高记录。")]
        private bool allowClearingRecords = true;
        [SerializeField] private string footer = "选择关卡开始挑战";
        [SerializeField, Tooltip("levels 为空时改用这份关卡表；运行时创建的面板靠它拿数据。")]
        private LevelCatalog catalog;

        [Header("面板节点（留空则按名字查找，找不到就按代码创建）")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text footerLabel;
        [SerializeField] private RectTransform rowsRoot;
        [SerializeField] private Button backButton;

        private readonly List<RowWidgets> rows = new List<RowWidgets>();
        private bool buildAttempted;
        private bool listenersBound;
        private bool missingReported;

        /// <summary>Raised with the index of the chosen level; the controller loads the scene.</summary>
        public event Action<int> LevelSelected;

        /// <summary>Raised by the "返回" button.</summary>
        public event Action BackClicked;

        public bool IsVisible => panelRoot != null && panelRoot.gameObject.activeSelf;

        public int LevelCount => EntryCount;

        /// <summary>Shows the panel and refreshes records / unlock states.</summary>
        public void Show()
        {
            EnsureBuilt();
            if (panelRoot == null)
            {
                return;
            }

            Refresh();
            panelRoot.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.gameObject.SetActive(false);
            }
        }

        /// <summary>Points the panel at a level table; used when the panel is built at runtime.</summary>
        public void Configure(LevelCatalog source)
        {
            catalog = source;
            if (buildAttempted && panelRoot != null)
            {
                Refresh();
            }
        }

        /// <summary>Re-reads best times and unlock states.</summary>
        public void Refresh()
        {
            EnsureBuilt();
            RebuildRows();
        }

        /// <summary>Scene name of one entry, for the controller that loads it.</summary>
        public bool TryGetSceneName(int index, out string sceneName)
        {
            sceneName = string.Empty;
            if (!TryGetEntry(index, out _, out _, out string found))
            {
                return false;
            }

            sceneName = found;
            return !string.IsNullOrWhiteSpace(sceneName);
        }

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            EnsureBuilt();
            BindListeners();
        }

        private void OnDisable()
        {
            UnbindListeners();
        }

        private void OnDestroy()
        {
            UnbindListeners();
        }

        private void EnsureBuilt()
        {
            ResolveReferences();
            if (!buildAttempted)
            {
                buildAttempted = true;
                if (panelRoot == null)
                {
                    BuildHierarchy();
                    ResolveReferences();
                }
            }

            if (missingReported || (panelRoot != null && backButton != null && rowsRoot != null))
            {
                return;
            }

            missingReported = true;
            Debug.LogError("LevelMenuUI on '" + BuildHierarchyPath() + "' could not resolve "
                + PanelObjectName + " / " + RowsObjectName + " / " + BackButtonObjectName
                + ". Create those nodes, or clear the inspector header fields so the panel is built "
                + "from code.", this);
        }

        private void ResolveReferences()
        {
            if (panelRoot == null)
            {
                Transform found = UiLookup.FindDeep<Transform>(transform, PanelObjectName);
                if (found != null)
                {
                    panelRoot = found as RectTransform;
                }
            }

            if (titleLabel == null)
            {
                titleLabel = UiLookup.FindDeep<Text>(transform, TitleObjectName);
            }

            if (footerLabel == null)
            {
                footerLabel = UiLookup.FindDeep<Text>(transform, FooterObjectName);
            }

            if (rowsRoot == null)
            {
                Transform found = UiLookup.FindDeep<Transform>(transform, RowsObjectName);
                if (found != null)
                {
                    rowsRoot = found as RectTransform;
                }
            }

            if (backButton == null)
            {
                backButton = UiLookup.FindDeep<Button>(transform, BackButtonObjectName);
            }
        }

        private void BuildHierarchy()
        {
            RectTransform parent = transform as RectTransform;
            if (parent == null)
            {
                // A plain Transform cannot hold UI; build under the scene canvas instead.
                parent = LevelUiFactory.EnsureCanvas().transform as RectTransform;
            }

            if (parent == null)
            {
                Debug.LogError("LevelMenuUI on '" + BuildHierarchyPath()
                    + "' has no RectTransform parent and no Canvas was found, so the panel was not "
                    + "built.", this);
                return;
            }

            Image panel = LevelUiFactory.CreatePanel(PanelObjectName, parent, LevelUiFactory.WindowColor);
            panelRoot = panel.rectTransform;
            LevelUiFactory.SetAnchored(panelRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, PanelSize);

            titleLabel = LevelUiFactory.CreateText(TitleObjectName, panelRoot, headline, 40,
                TextAnchor.MiddleCenter, LevelUiFactory.AccentColor, FontStyle.Bold);
            LevelUiFactory.SetAnchored(titleLabel.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(1200f, 68f));

            footerLabel = LevelUiFactory.CreateText(FooterObjectName, panelRoot, footer, 28,
                TextAnchor.MiddleCenter, LevelUiFactory.MutedTextColor);
            LevelUiFactory.SetAnchored(footerLabel.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1200f, 44f));

            RectTransform rowsArea = LevelUiFactory.CreateRect(RowsObjectName, panelRoot);
            LevelUiFactory.SetStretch(rowsArea, 60f, 150f, 60f, 180f);
            VerticalLayoutGroup layout = rowsArea.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            rowsRoot = rowsArea;

            Text backLabel;
            backButton = LevelUiFactory.CreateButton(BackButtonObjectName, panelRoot, "返回",
                new Vector2(320f, 80f), LevelUiFactory.ButtonAccentColor, out backLabel);
            LevelUiFactory.SetAnchored(backButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 84f), new Vector2(320f, 80f));

            UiFontProvider.Apply(panelRoot);
            panelRoot.gameObject.SetActive(false);
        }

        private void RebuildRows()
        {
            if (rowsRoot == null)
            {
                return;
            }

            int count = EntryCount;
            while (rows.Count < count)
            {
                rows.Add(CreateRow(rows.Count));
            }

            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < count;
                rows[i].Root.SetActive(used);
                if (used)
                {
                    BindRow(rows[i], i);
                }
            }

            EnsureRowListeners();
        }

        private RowWidgets CreateRow(int index)
        {
            Image background = LevelUiFactory.CreatePanel("Level Row " + index, rowsRoot,
                LevelUiFactory.RowColor);
            RectTransform rect = background.rectTransform;
            LevelUiFactory.AddLayoutHeight(background.gameObject, RowHeight);

            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            Text nameLabel = LevelUiFactory.CreateText(RowNameObjectName, rect, string.Empty, 30,
                TextAnchor.MiddleLeft, LevelUiFactory.TextColor);
            LevelUiFactory.SetAnchored(nameLabel.rectTransform, new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(620f, 60f));

            Text recordLabel = LevelUiFactory.CreateText(RowRecordObjectName, rect, string.Empty, 30,
                TextAnchor.MiddleRight, LevelUiFactory.MutedTextColor, FontStyle.Bold);
            LevelUiFactory.SetAnchored(recordLabel.rectTransform, new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(360f, 60f));

            Text clearLabel;
            Button clearButton = LevelUiFactory.CreateButton(RowClearButtonObjectName, rect, "清除",
                new Vector2(120f, 50f), LevelUiFactory.ButtonColor, out clearLabel);
            LevelUiFactory.SetAnchored(clearButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-420f, 0f), new Vector2(120f, 50f));

            RowWidgets row = new RowWidgets
            {
                Root = background.gameObject,
                Background = background,
                Button = button,
                NameLabel = nameLabel,
                RecordLabel = recordLabel,
                ClearButton = clearButton
            };
            row.ClickHandler = () => HandleRowClicked(row.Index);
            row.ClearHandler = () => HandleClearClicked(row.Index);
            return row;
        }

        private void BindRow(RowWidgets row, int index)
        {
            row.Index = index;
            if (!TryGetEntry(index, out string levelId, out string displayName, out _))
            {
                row.Button.interactable = false;
                row.NameLabel.text = string.Empty;
                row.RecordLabel.text = noRecordText;
                row.ClearButton.gameObject.SetActive(false);
                return;
            }

            bool unlocked = IsUnlocked(index);
            bool hasRecord = LevelRecords.TryGetRecord(levelId, out _);

            row.Button.interactable = unlocked;
            row.NameLabel.text = unlocked ? displayName : displayName + " · 未解锁";
            row.NameLabel.color = unlocked ? LevelUiFactory.TextColor : LevelUiFactory.MutedTextColor;
            row.RecordLabel.text = hasRecord ? LevelRecords.GetRecordDisplay(levelId) : noRecordText;
            row.Background.color = unlocked ? LevelUiFactory.RowColor : LevelUiFactory.PanelColor;
            row.ClearButton.gameObject.SetActive(allowClearingRecords && unlocked && hasRecord);
        }

        /// <summary>First level is always open; the next one opens once the previous one has a record.</summary>
        private bool IsUnlocked(int index)
        {
            if (index <= 0)
            {
                return true;
            }

            if (!TryGetEntry(index - 1, out string previousId, out _, out _))
            {
                return true;
            }

            return LevelRecords.TryGetRecord(previousId, out _);
        }

        private int EntryCount
        {
            get
            {
                if (catalog != null && catalog.Count > 0)
                {
                    return catalog.Count;
                }

                return levels != null ? levels.Length : 0;
            }
        }

        private bool TryGetEntry(int index, out string levelId, out string displayName, out string sceneName)
        {
            levelId = string.Empty;
            displayName = string.Empty;
            sceneName = string.Empty;
            if (index < 0 || index >= EntryCount)
            {
                return false;
            }

            if (catalog != null && catalog.Count > 0)
            {
                LevelDefinition definition = catalog.GetLevel(index);
                if (definition == null)
                {
                    return false;
                }

                levelId = definition.LevelId;
                displayName = definition.DisplayName;
                sceneName = definition.SceneName;
                return true;
            }

            LevelEntry entry = levels[index];
            if (entry == null)
            {
                return false;
            }

            levelId = entry.LevelId;
            displayName = entry.DisplayName;
            sceneName = entry.SceneName;
            return true;
        }

        private void EnsureRowListeners()
        {
            if (!listenersBound)
            {
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                RowWidgets row = rows[i];
                if (row.ListenersBound)
                {
                    continue;
                }

                row.Button.onClick.AddListener(row.ClickHandler);
                row.ClearButton.onClick.AddListener(row.ClearHandler);
                row.ListenersBound = true;
            }
        }

        private void BindListeners()
        {
            if (listenersBound)
            {
                return;
            }

            listenersBound = true;
            if (backButton != null)
            {
                backButton.onClick.AddListener(HandleBackClicked);
            }

            EnsureRowListeners();
        }

        private void UnbindListeners()
        {
            if (!listenersBound)
            {
                return;
            }

            listenersBound = false;
            if (backButton != null)
            {
                backButton.onClick.RemoveListener(HandleBackClicked);
            }

            for (int i = 0; i < rows.Count; i++)
            {
                RowWidgets row = rows[i];
                if (!row.ListenersBound)
                {
                    continue;
                }

                row.ListenersBound = false;
                row.Button.onClick.RemoveListener(row.ClickHandler);
                row.ClearButton.onClick.RemoveListener(row.ClearHandler);
            }
        }

        private void HandleRowClicked(int index)
        {
            if (index < 0)
            {
                return;
            }

            LevelSelected?.Invoke(index);
        }

        private void HandleClearClicked(int index)
        {
            if (!allowClearingRecords || !TryGetEntry(index, out string levelId, out _, out _))
            {
                return;
            }

            LevelRecordStore.ClearRecord(levelId);
            Refresh();
        }

        private void HandleBackClicked()
        {
            BackClicked?.Invoke();
        }

        private string BuildHierarchyPath()
        {
            string path = name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }

        private sealed class RowWidgets
        {
            public GameObject Root;
            public Image Background;
            public Button Button;
            public Button ClearButton;
            public Text NameLabel;
            public Text RecordLabel;
            public UnityAction ClickHandler;
            public UnityAction ClearHandler;
            public int Index = -1;
            public bool ListenersBound;
        }
    }
}
