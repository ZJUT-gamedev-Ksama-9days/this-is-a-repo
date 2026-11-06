using UnityEngine;
using UnityEngine.UI;

namespace GravityPrototype.UI
{
    /// <summary>
    /// Supplies a font that can render the Chinese labels of the level UI. The built-in Unity font has
    /// no CJK glyphs, so the runtime prefers a dynamic OS font and only falls back to the built-in one.
    /// Shipping a bundled CJK font (or a TMP font asset) later only needs to change this class.
    /// </summary>
    public static class UiFontProvider
    {
        private const int DefaultFontSize = 32;

        private static readonly string[] SystemFontCandidates =
        {
            "Microsoft YaHei",
            "Microsoft YaHei UI",
            "PingFang SC",
            "Hiragino Sans GB",
            "Noto Sans CJK SC",
            "Source Han Sans SC",
            "SimHei",
            "Arial Unicode MS",
            "Segoe UI"
        };

        private static Font cachedFont;
        private static bool resolved;

        public static Font GetFont()
        {
            if (resolved && cachedFont != null)
            {
                return cachedFont;
            }

            resolved = true;
            cachedFont = CreateSystemFont();
            if (cachedFont == null)
            {
                cachedFont = GetBuiltinFont();
            }

            return cachedFont;
        }

        /// <summary>Replaces the font of every <see cref="Text"/> that still uses a built-in font.</summary>
        public static void Apply(Transform root)
        {
            if (root == null)
            {
                return;
            }

            Font font = GetFont();
            if (font == null)
            {
                return;
            }

            Text[] labels = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (UsesBuiltinFont(labels[i].font))
                {
                    labels[i].font = font;
                }
            }
        }

        public static Font GetBuiltinFont()
        {
            Font font = TryLoadBuiltin("LegacyRuntime.ttf");
            return font != null ? font : TryLoadBuiltin("Arial.ttf");
        }

        private static Font CreateSystemFont()
        {
            try
            {
                return Font.CreateDynamicFontFromOSFont(SystemFontCandidates, DefaultFontSize);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("No dynamic system font could be created: " + exception.Message);
                return null;
            }
        }

        private static Font TryLoadBuiltin(string resourceName)
        {
            try
            {
                return Resources.GetBuiltinResource<Font>(resourceName);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        private static bool UsesBuiltinFont(Font font)
        {
            if (font == null)
            {
                return true;
            }

            return font.name == "LegacyRuntime" || font.name == "Arial";
        }
    }
}
