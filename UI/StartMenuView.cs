using System;
using UnityEngine;
using UnityEngine.UI;

namespace GravityPrototype.UI
{
    /// <summary>
    /// Start menu screen: a centred rounded panel with start / continue / settings / quit plus the quit
    /// confirmation popup. Like the other views it only raises intents; <see cref="StartMenuController"/>
    /// owns scene loads, save reads and Application.Quit. The hierarchy is built on demand with the
    /// <see cref="LevelUiFactory"/> helpers, and an authored hierarchy using the same node names is
    /// picked up instead, so the script keeps working after the nodes are moved into the scene file.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartMenuView : MonoBehaviour
    {
        /// <summary>Node names used by the code-built hierarchy and by the name lookup fallback.</summary>
        public const string PanelObjectName = "StartMenuPanel";
        public const string StartButtonObjectName = "Btn_Start";
        public const string ContinueButtonObjectName = "Btn_Continue";
        public const string SettingsButtonObjectName = "Btn_Settings";
        public const string QuitButtonObjectName = "Btn_Quit";
        public const string QuitConfirmObjectName = "Quit Confirm Popup";
        public const string ConfirmQuitButtonObjectName = "Btn_ConfirmQuit";
        public const string CancelQuitButtonObjectName = "Btn_CancelQuit";

        [Header("Menu buttons (leave empty to resolve by child name)")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Quit confirmation (leave empty to resolve by child name)")]
        [SerializeField] private GameObject quitConfirmRoot;
        [SerializeField] private Button confirmQuitButton;
        [SerializeField] private Button cancelQuitButton;

        private bool buildAttempted;
        private bool listenersBound;
        private bool missingReferencesReported;
        private RectTransform panelRoot;

        public event Action StartClicked;
        public event Action ContinueClicked;
        public event Action SettingsClicked;
        public event Action QuitClicked;
        public event Action QuitConfirmed;
        public event Action QuitCancelled;

        public bool IsQuitConfirmVisible => quitConfirmRoot != null && quitConfirmRoot.activeSelf;

        /// <summary>
        /// True while the menu panel itself is up. Show()/Hide() toggle the StartMenuPanel child, never
        /// this GameObject, so StartMenuController stays enabled and keeps listening.
        /// </summary>
        public bool IsVisible => panelRoot != null && panelRoot.gameObject.activeSelf;

        /// <summary>Brings the menu panel back after <see cref="Hide"/>.</summary>
        public void Show()
        {
            EnsureBuilt();
            if (panelRoot != null)
            {
                panelRoot.gameObject.SetActive(true);
            }
        }

        /// <summary>Hides the menu panel and its confirmation popup; the component stays enabled.</summary>
        public void Hide()
        {
            EnsureBuilt();
            HideQuitConfirm();
            if (panelRoot != null)
            {
                panelRoot.gameObject.SetActive(false);
            }
        }

        /// <summary>Greys out and blocks "继续" when the save slot is empty.</summary>
        public void SetContinueAvailable(bool available)
        {
            EnsureBuilt();
            if (continueButton != null)
            {
                continueButton.interactable = available;
            }
        }

        public void ShowQuitConfirm()
        {
            EnsureBuilt();
            if (quitConfirmRoot != null)
            {
                quitConfirmRoot.SetActive(true);
            }
        }

        public void HideQuitConfirm()
        {
            if (quitConfirmRoot != null)
            {
                quitConfirmRoot.SetActive(false);
            }
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
                if (!IsHierarchyComplete())
                {
                    BuildHierarchy();
                    ResolveReferences();
                }
            }

            ReportMissingReferences();
        }

        private bool IsHierarchyComplete()
        {
            return startButton != null
                && continueButton != null
                && settingsButton != null
                && quitButton != null
                && quitConfirmRoot != null
                && confirmQuitButton != null
                && cancelQuitButton != null;
        }

        private void ResolveReferences()
        {
            if (startButton == null)
            {
                startButton = UiLookup.FindDeep<Button>(transform, StartButtonObjectName);
            }

            if (continueButton == null)
            {
                continueButton = UiLookup.FindDeep<Button>(transform, ContinueButtonObjectName);
            }

            if (settingsButton == null)
            {
                settingsButton = UiLookup.FindDeep<Button>(transform, SettingsButtonObjectName);
            }

            if (quitButton == null)
            {
                quitButton = UiLookup.FindDeep<Button>(transform, QuitButtonObjectName);
            }

            if (confirmQuitButton == null)
            {
                confirmQuitButton = UiLookup.FindDeep<Button>(transform, ConfirmQuitButtonObjectName);
            }

            if (cancelQuitButton == null)
            {
                cancelQuitButton = UiLookup.FindDeep<Button>(transform, CancelQuitButtonObjectName);
            }

            if (quitConfirmRoot == null)
            {
                Transform found = UiLookup.FindDeep<Transform>(transform, QuitConfirmObjectName);
                if (found != null)
                {
                    quitConfirmRoot = found.gameObject;
                }
            }

            if (panelRoot == null)
            {
                panelRoot = UiLookup.FindDeep<RectTransform>(transform, PanelObjectName);
            }
        }

        private void BuildHierarchy()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = LevelUiFactory.EnsureCanvas();
            }

            Transform parent = transform;
            RectTransform viewRect = transform as RectTransform;
            if (viewRect == null)
            {
                // A plain Transform cannot be a child of a Canvas; build under the canvas instead.
                parent = canvas.transform;
            }
            else
            {
                viewRect.anchorMin = Vector2.zero;
                viewRect.anchorMax = Vector2.one;
                viewRect.pivot = new Vector2(0.5f, 0.5f);
                viewRect.offsetMin = Vector2.zero;
                viewRect.offsetMax = Vector2.zero;
            }

            // StartMenuPanel is layout-only: it carries the VerticalLayoutGroup that arranges the
            // buttons but no Image, so there is no plate behind them. The node itself must stay.
            RectTransform panelRect = LevelUiFactory.CreateRect(PanelObjectName, parent);
            LevelUiFactory.SetAnchored(panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(680f, 700f));
            panelRoot = panelRect;

            VerticalLayoutGroup layout = panelRect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(64, 64, 48, 48);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Text title = LevelUiFactory.CreateText("StartMenu Title", panelRect, "GRAVITY FRAME", 44,
                TextAnchor.MiddleCenter, LevelUiFactory.TextColor, FontStyle.Bold);
            LevelUiFactory.AddLayoutHeight(title.gameObject, 60f);

            Text subtitle = LevelUiFactory.CreateText("StartMenu Subtitle", panelRect, "主菜单 / MAIN MENU",
                30, TextAnchor.MiddleCenter, LevelUiFactory.MutedTextColor);
            LevelUiFactory.AddLayoutHeight(subtitle.gameObject, 40f);

            Text label;
            Button start = LevelUiFactory.CreateButton(StartButtonObjectName, panelRect, "开始",
                new Vector2(552f, 96f), LevelUiFactory.ButtonAccentColor, out label);
            LevelUiFactory.AddLayoutHeight(start.gameObject, 96f);
            ApplyRoundedSprite(start);

            Button resume = LevelUiFactory.CreateButton(ContinueButtonObjectName, panelRect, "继续",
                new Vector2(552f, 96f), LevelUiFactory.ButtonColor, out label);
            LevelUiFactory.AddLayoutHeight(resume.gameObject, 96f);
            ApplyRoundedSprite(resume);

            Button settings = LevelUiFactory.CreateButton(SettingsButtonObjectName, panelRect, "设置",
                new Vector2(552f, 96f), LevelUiFactory.ButtonColor, out label);
            LevelUiFactory.AddLayoutHeight(settings.gameObject, 96f);
            ApplyRoundedSprite(settings);

            Button quit = LevelUiFactory.CreateButton(QuitButtonObjectName, panelRect, "退出游戏",
                new Vector2(552f, 96f), LevelUiFactory.ButtonColor, out label);
            LevelUiFactory.AddLayoutHeight(quit.gameObject, 96f);
            ApplyRoundedSprite(quit);

            BuildQuitConfirm(parent);
            UiFontProvider.Apply(panelRect);
            UiFontProvider.Apply(transform);
        }

        private void BuildQuitConfirm(Transform parent)
        {
            Image dim = LevelUiFactory.CreatePanel(QuitConfirmObjectName, parent, LevelUiFactory.DimColor);
            LevelUiFactory.SetStretch(dim.rectTransform, 0f, 0f, 0f, 0f);

            Image window = LevelUiFactory.CreatePanel("Quit Confirm Window", dim.rectTransform,
                LevelUiFactory.WindowColor);
            LevelUiFactory.SetAnchored(window.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 340f));
            ApplyRoundedSprite(window);

            Text title = LevelUiFactory.CreateText("Quit Confirm Title", window.rectTransform,
                "确认退出游戏？", 36, TextAnchor.MiddleCenter, LevelUiFactory.AccentColor, FontStyle.Bold);
            LevelUiFactory.SetAnchored(title.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(580f, 72f));

            Text hint = LevelUiFactory.CreateText("Quit Confirm Hint", window.rectTransform,
                "未保存的进度会丢失", 30, TextAnchor.MiddleCenter, LevelUiFactory.MutedTextColor);
            LevelUiFactory.SetAnchored(hint.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(580f, 56f));

            Text label;
            Button confirm = LevelUiFactory.CreateButton(ConfirmQuitButtonObjectName, window.rectTransform,
                "退出", new Vector2(260f, 88f), LevelUiFactory.ButtonColor, out label);
            LevelUiFactory.SetAnchored(confirm.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(-150f, 96f), new Vector2(260f, 88f));
            ApplyRoundedSprite(confirm);

            Button cancel = LevelUiFactory.CreateButton(CancelQuitButtonObjectName, window.rectTransform,
                "取消", new Vector2(260f, 88f), LevelUiFactory.ButtonAccentColor, out label);
            LevelUiFactory.SetAnchored(cancel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(150f, 96f), new Vector2(260f, 88f));
            ApplyRoundedSprite(cancel);

            dim.gameObject.SetActive(false);
        }

        private void BindListeners()
        {
            if (listenersBound)
            {
                return;
            }

            listenersBound = true;
            if (startButton != null)
            {
                startButton.onClick.AddListener(HandleStart);
            }

            if (continueButton != null)
            {
                continueButton.onClick.AddListener(HandleContinue);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(HandleSettings);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(HandleQuit);
            }

            if (confirmQuitButton != null)
            {
                confirmQuitButton.onClick.AddListener(HandleConfirmQuit);
            }

            if (cancelQuitButton != null)
            {
                cancelQuitButton.onClick.AddListener(HandleCancelQuit);
            }
        }

        private void UnbindListeners()
        {
            if (!listenersBound)
            {
                return;
            }

            listenersBound = false;
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(HandleStart);
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(HandleContinue);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(HandleSettings);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(HandleQuit);
            }

            if (confirmQuitButton != null)
            {
                confirmQuitButton.onClick.RemoveListener(HandleConfirmQuit);
            }

            if (cancelQuitButton != null)
            {
                cancelQuitButton.onClick.RemoveListener(HandleCancelQuit);
            }
        }

        private void HandleStart()
        {
            StartClicked?.Invoke();
        }

        private void HandleContinue()
        {
            ContinueClicked?.Invoke();
        }

        private void HandleSettings()
        {
            SettingsClicked?.Invoke();
        }

        private void HandleQuit()
        {
            QuitClicked?.Invoke();
        }

        private void HandleConfirmQuit()
        {
            QuitConfirmed?.Invoke();
        }

        private void HandleCancelQuit()
        {
            QuitCancelled?.Invoke();
        }

        private void ReportMissingReferences()
        {
            if (missingReferencesReported)
            {
                return;
            }

            string missing = string.Empty;
            if (startButton == null)
            {
                missing = AppendName(missing, StartButtonObjectName);
            }

            if (continueButton == null)
            {
                missing = AppendName(missing, ContinueButtonObjectName);
            }

            if (settingsButton == null)
            {
                missing = AppendName(missing, SettingsButtonObjectName);
            }

            if (quitButton == null)
            {
                missing = AppendName(missing, QuitButtonObjectName);
            }

            if (quitConfirmRoot == null)
            {
                missing = AppendName(missing, QuitConfirmObjectName);
            }

            if (confirmQuitButton == null)
            {
                missing = AppendName(missing, ConfirmQuitButtonObjectName);
            }

            if (cancelQuitButton == null)
            {
                missing = AppendName(missing, CancelQuitButtonObjectName);
            }

            if (missing.Length == 0)
            {
                return;
            }

            missingReferencesReported = true;
            Debug.LogError("StartMenuView on '" + BuildHierarchyPath() + "' could not resolve the UI node(s): "
                + missing + ". Create them with these exact names or assign the fields in the Inspector.",
                this);
        }

        private static string AppendName(string current, string name)
        {
            return current.Length > 0 ? current + ", " + name : name;
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

        private static void ApplyRoundedSprite(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
        }

        private static void ApplyRoundedSprite(Button button)
        {
            if (button == null)
            {
                return;
            }

            ApplyRoundedSprite(button.targetGraphic as Image);
        }

        private static Sprite roundedSprite;

        /// <summary>
        /// One shared 9-sliced rounded rectangle so buttons and panels get soft corners without shipping
        /// a sprite asset. Created once per play session and reused by every element of the menu.
        /// </summary>
        private static Sprite RoundedSprite
        {
            get
            {
                if (roundedSprite == null)
                {
                    roundedSprite = CreateRoundedSprite(64, 20);
                }

                return roundedSprite;
            }
        }

        private static Sprite CreateRoundedSprite(int size, int radius)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "StartMenu Rounded Rect",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float cx = Mathf.Clamp(px, radius, size - radius);
                    float cy = Mathf.Clamp(py, radius, size - radius);
                    float distance = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.name = "StartMenu Rounded Rect";
            return sprite;
        }
    }
}
