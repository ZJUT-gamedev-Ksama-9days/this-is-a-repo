using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityPrototype
{
    /// <summary>
    /// Prototype level-select IMGUI. Each entry lists the persisted best time of its level id, or the
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

        [SerializeField] private string headline = "GRAVITY FRAME / LEVEL SELECT";
        [SerializeField] private LevelEntry[] levels = Array.Empty<LevelEntry>();
        [SerializeField, Tooltip("没有历史记录时显示的文本。")]
        private string noRecordText = LevelRecordStore.NoRecordText;
        [SerializeField, Tooltip("允许在菜单里清空某个关卡的最高记录。")]
        private bool allowClearingRecords = true;
        [SerializeField] private string footer = "Reach the goal to stop the clock. Best times are saved with PlayerPrefs.";

        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle buttonStyle;
        private GUIStyle smallStyle;

        private void OnGUI()
        {
            EnsureStyles();
            float width = Mathf.Min(720f, Screen.width - 32f);
            GUILayout.BeginArea(new Rect(16f, 16f, width, Mathf.Min(520f, Screen.height - 32f)), GUI.skin.box);
            GUILayout.Label(headline, titleStyle);
            GUILayout.Space(8f);

            if (levels == null || levels.Length == 0)
            {
                GUILayout.Label("No levels configured.", textStyle);
            }
            else
            {
                for (int i = 0; i < levels.Length; i++)
                {
                    DrawEntry(levels[i]);
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(footer, smallStyle);
            GUILayout.EndArea();
        }

        private void DrawEntry(LevelEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            bool hasRecord = LevelRecordStore.TryGetBestTime(entry.LevelId, out float best);
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.BeginVertical();
            GUILayout.Label(entry.DisplayName, textStyle);
            GUILayout.Label("BEST  " + (hasRecord ? LevelRecordStore.FormatTime(best) : noRecordText), smallStyle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (allowClearingRecords && hasRecord && GUILayout.Button("CLEAR", buttonStyle, GUILayout.Width(90f)))
            {
                LevelRecordStore.ClearRecord(entry.LevelId);
            }

            if (GUILayout.Button("START", buttonStyle, GUILayout.Width(110f)))
            {
                StartLevel(entry.SceneName);
            }

            GUILayout.EndHorizontal();
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

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = Color.white;
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            textStyle.normal.textColor = new Color(0.8f, 0.94f, 1f);
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15 };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            smallStyle.normal.textColor = new Color(0.62f, 0.78f, 0.86f);
        }
    }
}
