using UnityEngine;

namespace GravityPrototype
{
    /// <summary>Flat, hard-edged UI using sampled swatches; never relies on the default rounded skin.</summary>
    public static class PixelUI
    {
        public static readonly Color Ink = Hex("323846");
        public static readonly Color PanelColor = Hex("454B57");
        public static readonly Color Border = Hex("926EB4");
        public static readonly Color TextColor = Hex("FDEBD3");
        public static readonly Color Muted = Hex("C3C7D0");
        public static readonly Color Accent = Hex("20AF8D");
        public static readonly Color Danger = Hex("DE4039");
        private static GUIStyle label, title, button;

        public static Color Hex(string rgb) { ColorUtility.TryParseHtmlString("#" + rgb, out Color value); return value; }
        private static Font font;
        public static Font Font => font != null ? font : (font = Resources.Load<Font>("Fonts/Cubic_11"));
        public static GUIStyle Label => label ?? (label = Make(24, false));
        public static GUIStyle Title => title ?? (title = Make(24, false));
        public static GUIStyle ButtonStyle => button ?? (button = new GUIStyle(Make(24, false)) { alignment = TextAnchor.MiddleCenter, wordWrap = false });
        private static GUIStyle Make(int size, bool bold)
        {
            var style = new GUIStyle { font = Font, fontSize = size, fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft, wordWrap = true, padding = new RectOffset(0,0,0,0) };
            style.normal.textColor = TextColor;
            return style;
        }
        public static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }
        public static void Panel(Rect rect)
        {
            Fill(new Rect(rect.x + 4, rect.y + 4, rect.width, rect.height), Hex("050505"));
            Fill(rect, Border);
            Fill(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), Ink);
            Fill(new Rect(rect.x + 2, rect.y + 2, 4, rect.height - 4), Accent);
        }
        public static bool Button(Rect rect, string text)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Fill(rect, hover ? Accent : Border);
            Fill(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), hover ? PanelColor : Ink);
            return GUI.Button(rect, text, ButtonStyle);
        }
        public static void Controls(Rect rect, bool includePads)
        {
            Panel(rect);
            GUI.Label(new Rect(rect.x + 18, rect.y + 12, rect.width - 36, 30), "操作说明 / CONTROLS", Title);
            string text = "A/D 或方向键：移动\n空格 / W / ↑：跳跃\nQ/E：换装置\n1 重力 · 2 机械臂 · 3 空手\n\n"
                + "重力装置：左键拖动框\n机械臂：左键抓取 · 右键推\n抓取物必须在重力场内\n收起 1.5 秒淡出，3 秒消失\n"
                + (includePads ? "弹射台：接触弹射 · 右键摆放\n" : "")
                + "R：重新开始\n避开尖刺、重击和高处坠落。";
            GUI.Label(new Rect(rect.x + 18, rect.y + 52, rect.width - 36, rect.height - 64), text, Label);
        }
    }
}
