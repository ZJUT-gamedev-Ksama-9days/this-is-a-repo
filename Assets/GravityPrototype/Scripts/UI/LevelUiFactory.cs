using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GravityPrototype.UI
{
    /// <summary>
    /// Builds the whole level screen with Canvas + Panel + Button + Popup objects: top bar, side menu,
    /// pause popup, shared records popup, settings popup, victory effect and toast. Building it from
    /// code keeps the layout version-controlled and lets any level scene get the UI without wiring a
    /// prefab, while an authored prefab can still be dropped in later (the views resolve by name).
    /// </summary>
    public static class LevelUiFactory
    {
        public const string RootObjectName = "Level UI";

        /// <summary>Container of the standalone top bar created by <see cref="CreateHud"/>.</summary>
        public const string HudRootObjectName = "HUD Root";

        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        public const float TopBarHeight = 92f;
        public const float SideMenuWidth = 460f;

        internal static readonly Color PanelColor = PixelUI.Hex("323846");
        internal static readonly Color WindowColor = PixelUI.Hex("323846");
        internal static readonly Color RowColor = PixelUI.Hex("454B57");
        internal static readonly Color ButtonColor = PixelUI.Hex("653791");
        internal static readonly Color ButtonAccentColor = PixelUI.Hex("76499E");
        internal static readonly Color AccentColor = PixelUI.Hex("20AF8D");
        internal static readonly Color TextColor = PixelUI.Hex("FDEBD3");
        internal static readonly Color MutedTextColor = PixelUI.Hex("C3C7D0");
        internal static readonly Color DimColor = new Color(0f, 0f, 0f, 0.72f);
        internal static readonly Color TrackColor = PixelUI.Hex("454B57");
        internal static readonly Color HandleColor = PixelUI.Hex("BCA5D5");

        /// <summary>Returns the scene canvas, creating a scaled overlay canvas and an event system if needed.</summary>
        public static Canvas EnsureCanvas(UnityEngine.SceneManagement.Scene scene = default)
        {
            Canvas canvas = null;
            if (scene.IsValid())
            {
                foreach (var root in scene.GetRootGameObjects())
                    if ((canvas = root.GetComponentInChildren<Canvas>(true)) != null) break;
            }
            else canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("UI Canvas",
                    typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                if (scene.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasObject, scene);
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = ReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.transform.position = Vector3.zero;
            eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_LEGACY_INPUT_MANAGER
            // Active Input Handling is "Both" in this project, so the legacy pointer module is valid.
            // With "Input System Package (New)" only, add InputSystemUIInputModule instead.
            eventSystemObject.AddComponent<StandaloneInputModule>();
#else
            Debug.LogError("Active Input Handling is set to the Input System package, so the UI has no "
                + "pointer module. Add an InputSystemUIInputModule to the EventSystem.");
#endif
        }

        /// <summary>Creates the level screen hierarchy under <paramref name="parent"/> and returns its views.</summary>
        public static LevelUiParts Build(Transform parent)
        {
            RectTransform root = CreateRect(RootObjectName, parent);
            SetStretch(root, 0f, 0f, 0f, 0f);

            LevelUiParts parts = new LevelUiParts
            {
                Root = root,
                Hud = BuildHud(root),
                SideMenu = BuildSideMenu(root),
                PausePopup = BuildPausePopup(root),
                RecordsPopup = BuildRecordsPopup(root),
                SettingsPopup = BuildSettingsPopup(root),
                VictoryEffect = BuildVictoryEffect(root),
                Toast = BuildToast(root)
            };

            parts.SideMenu.Show(false);
            parts.PausePopup.Hide();
            parts.RecordsPopup.Hide();
            parts.SettingsPopup.Hide();
            parts.VictoryEffect.Hide();
            parts.Toast.Hide();

            UiFontProvider.Apply(root);
            return parts;
        }

        /// <summary>Creates only the top bar; used by scenes that have no LevelFlowController.</summary>
        public static LevelHudView CreateHud(Transform parent)
        {
            RectTransform root = CreateRect(HudRootObjectName, parent);
            SetStretch(root, 0f, 0f, 0f, 0f);
            LevelHudView hud = BuildHud(root);
            UiFontProvider.Apply(hud.transform);
            return hud;
        }

        private static LevelHudView BuildHud(RectTransform root)
        {
            Image bar = CreatePanel("Top Bar", root, PanelColor);
            SetTopStretch(bar.rectTransform, TopBarHeight);

            Text levelLabel = CreateText("Level Label", bar.rectTransform, "第 1 关", 36,
                TextAnchor.MiddleLeft, TextColor, FontStyle.Bold);
            SetAnchored(levelLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(32f, 0f), new Vector2(760f, 64f));

            Text timeLabel = CreateText("Time Label", bar.rectTransform, "00' 00''", 34,
                TextAnchor.MiddleRight, AccentColor, FontStyle.Bold);
            SetAnchored(timeLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-212f, 0f), new Vector2(380f, 64f));

            Text menuLabel;
            Button menuButton = CreateButton("Menu Button", bar.rectTransform, "菜单",
                new Vector2(170f, 64f), ButtonAccentColor, out menuLabel);
            SetAnchored(menuButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(170f, 64f));

            // Single-life gameplay deliberately has no health bar or numeric HP.

            return bar.gameObject.AddComponent<LevelHudView>();
        }

        private static SideMenuView BuildSideMenu(RectTransform root)
        {
            Image panel = CreatePanel("Side Menu", root, PanelColor);
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(SideMenuWidth, 0f);
            rect.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 28, 28);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Text title = CreateText("Side Menu Title", rect, "关卡菜单", 34,
                TextAnchor.MiddleLeft, AccentColor, FontStyle.Bold);
            AddLayoutHeight(title.gameObject, 56f);

            Text label;
            Button settings = CreateButton("Settings Button", rect, "设置",
                new Vector2(SideMenuWidth - 56f, 76f), ButtonColor, out label);
            AddLayoutHeight(settings.gameObject, 76f);

            Button pause = CreateButton("Pause Button", rect, "暂停",
                new Vector2(SideMenuWidth - 56f, 76f), ButtonColor, out label);
            AddLayoutHeight(pause.gameObject, 76f);

            Button records = CreateButton("Records Button", rect, "查看当前记录",
                new Vector2(SideMenuWidth - 56f, 76f), ButtonColor, out label);
            AddLayoutHeight(records.gameObject, 76f);

            Button save = CreateButton("Save Button", rect, "存档",
                new Vector2(SideMenuWidth - 56f, 76f), ButtonAccentColor, out label);
            AddLayoutHeight(save.gameObject, 76f);

            Button quit = CreateButton("Quit Button", rect, "结束游戏",
                new Vector2(SideMenuWidth - 56f, 76f), ButtonColor, out label);
            AddLayoutHeight(quit.gameObject, 76f);

            Button close = CreateButton("Close Button", rect, "关闭",
                new Vector2(SideMenuWidth - 56f, 64f), ButtonColor, out label);
            AddLayoutHeight(close.gameObject, 64f);

            return panel.gameObject.AddComponent<SideMenuView>();
        }

        private static PausePopupView BuildPausePopup(RectTransform root)
        {
            Image dim = CreatePopupRoot("Pause Popup", root, out RectTransform window,
                new Vector2(760f, 380f));

            Text title = CreateText("Pause Title", window, "已暂停", 46, TextAnchor.MiddleCenter,
                AccentColor, FontStyle.Bold);
            SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -44f), new Vector2(680f, 80f));

            Text label;
            Button resume = CreateButton("Resume Button", window, "继续游戏",
                new Vector2(300f, 84f), ButtonAccentColor, out label);
            SetAnchored(resume.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-170f, -50f), new Vector2(300f, 84f));

            Button quit = CreateButton("Quit Button", window, "结束游戏",
                new Vector2(300f, 84f), ButtonColor, out label);
            SetAnchored(quit.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(170f, -50f), new Vector2(300f, 84f));

            return dim.gameObject.AddComponent<PausePopupView>();
        }

        internal static RecordsPopupView BuildRecordsPopup(RectTransform root)
        {
            Image dim = CreatePopupRoot("Records Popup", root, out RectTransform window,
                new Vector2(1100f, 700f));

            Text title = CreateText("Records Title", window, "当前记录", 44, TextAnchor.MiddleLeft,
                AccentColor, FontStyle.Bold);
            SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -28f), new Vector2(1020f, 72f));

            RectTransform rows = CreateRect("Rows", window);
            SetStretch(rows, 40f, 150f, 40f, 120f);
            VerticalLayoutGroup rowsLayout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 10f;
            rowsLayout.padding = new RectOffset(0, 0, 0, 0);
            rowsLayout.childAlignment = TextAnchor.UpperCenter;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;

            CreateRecordRowTemplate(rows);

            Text label;
            Button primary = CreateButton("Primary Button", window, "下一关",
                new Vector2(280f, 76f), ButtonAccentColor, out label);
            SetAnchored(primary.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(-300f, 82f), new Vector2(280f, 76f));

            Button quit = CreateButton("Quit Button", window, "结束游戏",
                new Vector2(280f, 76f), ButtonColor, out label);
            SetAnchored(quit.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(300f, 82f), new Vector2(280f, 76f));

            Button close = CreateButton("Close Button", window, "关闭",
                new Vector2(280f, 76f), ButtonColor, out label);
            SetAnchored(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 82f), new Vector2(280f, 76f));

            return dim.gameObject.AddComponent<RecordsPopupView>();
        }

        private static void CreateRecordRowTemplate(RectTransform rows)
        {
            Image background = CreatePanel("Row Template", rows, RowColor);
            background.transform.localScale = Vector3.one;
            LayoutElement element = background.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 76f;
            element.minHeight = 76f;

            Text index = CreateText("Index Label", background.rectTransform, "01", 30,
                TextAnchor.MiddleLeft, AccentColor, FontStyle.Bold);
            SetAnchored(index.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(24f, 0f), new Vector2(80f, 60f));

            Text name = CreateText("Name Label", background.rectTransform, "第 1 关", 30,
                TextAnchor.MiddleLeft, TextColor);
            SetAnchored(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(120f, 0f), new Vector2(700f, 60f));

            Text time = CreateText("Time Label", background.rectTransform, "?' ?''", 30,
                TextAnchor.MiddleRight, MutedTextColor, FontStyle.Bold);
            SetAnchored(time.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-24f, 0f), new Vector2(320f, 60f));

            background.gameObject.AddComponent<LevelRecordRowView>();
            background.gameObject.SetActive(false);
        }

        internal static SettingsPopupView BuildSettingsPopup(RectTransform root)
        {
            Image dim = CreatePopupRoot("Settings Popup", root, out RectTransform window,
                new Vector2(900f, 600f));

            Text title = CreateText("Settings Title", window, "设置", 44, TextAnchor.MiddleLeft,
                AccentColor, FontStyle.Bold);
            SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -28f), new Vector2(820f, 72f));

            Text masterLabel = CreateText("Master Label", window, "主音量", 30, TextAnchor.MiddleLeft,
                TextColor);
            SetAnchored(masterLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(-400f, 90f), new Vector2(200f, 56f));

            Text masterValue;
            Slider masterSlider = CreateSlider("Master Slider", window, "Master Value", out masterValue);
            SetAnchored(masterSlider.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-90f, 90f), new Vector2(420f, 48f));
            SetAnchored(masterValue.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(180f, 90f), new Vector2(120f, 56f));

            Text effectLabel = CreateText("Effect Label", window, "音效音量", 30, TextAnchor.MiddleLeft,
                TextColor);
            SetAnchored(effectLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(-400f, 0f), new Vector2(200f, 56f));

            Text effectValue;
            Slider effectSlider = CreateSlider("Effect Slider", window, "Effect Value", out effectValue);
            SetAnchored(effectSlider.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-90f, 0f), new Vector2(420f, 48f));
            SetAnchored(effectValue.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(180f, 0f), new Vector2(120f, 56f));

            Text fullscreenLabel = CreateText("Fullscreen Label", window, "全屏显示", 30,
                TextAnchor.MiddleLeft, TextColor);
            SetAnchored(fullscreenLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(-400f, -90f), new Vector2(260f, 56f));

            Toggle fullscreenToggle = CreateToggle("Fullscreen Toggle", window, new Vector2(52f, 52f));
            SetAnchored(fullscreenToggle.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(-90f, -90f), new Vector2(52f, 52f));

            Text label;
            Button reset = CreateButton("Reset Button", window, "恢复默认",
                new Vector2(280f, 76f), ButtonColor, out label);
            SetAnchored(reset.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(-160f, 84f), new Vector2(280f, 76f));

            Button close = CreateButton("Close Button", window, "完成",
                new Vector2(280f, 76f), ButtonAccentColor, out label);
            SetAnchored(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(160f, 84f), new Vector2(280f, 76f));

            return dim.gameObject.AddComponent<SettingsPopupView>();
        }

        private static VictoryEffectView BuildVictoryEffect(RectTransform root)
        {
            Image block = CreatePanel("Victory Effect", root, new Color(0f, 0f, 0f, 0.35f));
            SetStretch(block.rectTransform, 0f, 0f, 0f, 0f);

            RectTransform banner = CreateRect("Banner", block.rectTransform);
            SetAnchored(banner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1200f, 260f));
            CanvasGroup group = banner.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Text title = CreateText("Victory Title", banner, "关卡完成", 76, TextAnchor.MiddleCenter,
                AccentColor, FontStyle.Bold);
            SetAnchored(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 44f), new Vector2(1160f, 100f));

            Text subtitle = CreateText("Victory Subtitle", banner, string.Empty, 36,
                TextAnchor.MiddleCenter, MutedTextColor);
            SetAnchored(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -46f), new Vector2(1160f, 64f));

            RectTransform sparks = CreateRect("Sparks", block.rectTransform);
            SetAnchored(sparks, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(4f, 4f));

            Image sparkTemplate = CreatePanel("Spark Template", sparks, AccentColor, false);
            SetAnchored(sparkTemplate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(20f, 20f));
            sparkTemplate.gameObject.SetActive(false);

            return block.gameObject.AddComponent<VictoryEffectView>();
        }

        private static ToastView BuildToast(RectTransform root)
        {
            Image panel = CreatePanel("Toast", root, new Color(0.05f, 0.09f, 0.14f, 0.92f), false);
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -120f);
            rect.sizeDelta = new Vector2(900f, 72f);

            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Text message = CreateText("Toast Message", rect, string.Empty, 28, TextAnchor.MiddleCenter,
                TextColor);
            SetStretch(message.rectTransform, 24f, 8f, 24f, 8f);

            return panel.gameObject.AddComponent<ToastView>();
        }

        private static Slider CreateSlider(string name, Transform parent, string valueName, out Text valueLabel)
        {
            RectTransform rect = CreateRect(name, parent);
            Slider slider = rect.gameObject.AddComponent<Slider>();

            Image background = CreatePanel("Background", rect, TrackColor, false);
            SetStretch(background.rectTransform, 0f, 18f, 0f, 18f);

            RectTransform fillArea = CreateRect("Fill Area", rect);
            SetStretch(fillArea, 12f, 18f, 12f, 18f);

            Image fill = CreatePanel("Fill", fillArea, AccentColor, false);
            SetStretch(fill.rectTransform, 0f, 0f, 0f, 0f);

            RectTransform handleArea = CreateRect("Handle Slide Area", rect);
            SetStretch(handleArea, 12f, 0f, 12f, 0f);

            Image handle = CreatePanel("Handle", handleArea, HandleColor, true);
            SetAnchored(handle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(28f, 0f));

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            valueLabel = CreateText(valueName, parent, "80%", 28, TextAnchor.MiddleLeft, MutedTextColor);
            return slider;
        }

        private static Toggle CreateToggle(string name, Transform parent, Vector2 size)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = TrackColor;

            Toggle toggle = rect.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;

            Image checkmark = CreatePanel("Checkmark", rect, AccentColor, false);
            SetStretch(checkmark.rectTransform, 6f, 6f, 6f, 6f);

            toggle.graphic = checkmark;
            toggle.isOn = false;
            return toggle;
        }

        private static Image CreatePopupRoot(string name, RectTransform parent, out RectTransform window,
            Vector2 windowSize)
        {
            Image dim = CreatePanel(name, parent, DimColor);
            SetStretch(dim.rectTransform, 0f, 0f, 0f, 0f);

            Image windowImage = CreatePanel("Window", dim.rectTransform, WindowColor);
            window = windowImage.rectTransform;
            SetAnchored(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                windowSize);
            return dim;
        }

        internal static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        internal static Image CreatePanel(string name, Transform parent, Color color,
            bool raycastTarget = true)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        internal static Text CreateText(string name, Transform parent, string content, int fontSize,
            TextAnchor alignment, Color color, FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = CreateRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = UiFontProvider.GetFont();
            text.text = content;
            text.fontSize = fontSize >= 32 ? 36 : 24;
            text.alignment = alignment;
            text.color = color;
            text.fontStyle = FontStyle.Normal;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        internal static Button CreateButton(string name, Transform parent, string label, Vector2 size,
            Color color, out Text labelText)
        {
            Image image = CreatePanel(name, parent, color);
            RectTransform rect = image.rectTransform;
            rect.sizeDelta = size;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            labelText = CreateText("Label", rect, label, 30, TextAnchor.MiddleCenter, TextColor,
                FontStyle.Bold);
            SetStretch(labelText.rectTransform, 12f, 6f, 12f, 6f);
            return button;
        }

        internal static void AddLayoutWidth(GameObject target, float width)
        {
            LayoutElement element = target.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = target.AddComponent<LayoutElement>();
            }

            element.preferredWidth = width;
            element.minWidth = width;
        }

        internal static void AddLayoutHeight(GameObject target, float height)
        {
            LayoutElement element = target.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = target.AddComponent<LayoutElement>();
            }

            element.preferredHeight = height;
            element.minHeight = height;
        }

        internal static void SetStretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        internal static void SetTopStretch(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -height);
            rect.offsetMax = Vector2.zero;
        }

        internal static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
