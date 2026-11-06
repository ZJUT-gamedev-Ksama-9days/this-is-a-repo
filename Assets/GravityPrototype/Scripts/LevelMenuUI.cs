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

        private void OnGUI()
        {
            float width = Mathf.Min(650f, Screen.width - 40f);
            float left = (Screen.width - width) * 0.5f;
            float top = Mathf.Max(24f, Screen.height * 0.22f);
            PixelUI.Panel(new Rect(left, top, width, 310f));
            GUI.Label(new Rect(left + 28, top + 20, width - 56, 32), headline, PixelUI.Title);
            GUI.Label(new Rect(left + 28, top + 61, width - 56, 28), "SELECT SECTOR   /   选择关卡", PixelUI.Label);
            if (levels == null || levels.Length == 0)
                GUI.Label(new Rect(left + 28, top + 110, width - 56, 32), "暂无关卡", PixelUI.Label);
            else
                for (int i = 0; i < levels.Length; i++) DrawPixelEntry(levels[i], new Rect(left + 28, top + 110 + i * 78, width - 56, 66));
            if (Progress.SaveGameStore.TryLoad(out var save) && PixelUI.Button(new Rect(left+28,top+198,width-56,38),"继续存档 / CONTINUE · "+save.levelId))
            {
                foreach(var entry in levels)
                    if(entry != null && entry.LevelId == save.levelId && SceneLoadUtility.CanLoadScene(entry.SceneName))
                    { Progress.SaveGameStore.RequestResumeOnNextLevel(); SceneManager.LoadScene(entry.SceneName); break; }
            }
            GUI.Label(new Rect(left + 28, top + 257, width - 56, 36), footer, PixelUI.Label);
        }

        private void DrawPixelEntry(LevelEntry entry, Rect rect)
        {
            if (entry == null) return;
            PixelUI.Fill(rect, PixelUI.PanelColor);
            GUI.Label(new Rect(rect.x + 12, rect.y + 4, rect.width - 142, 28), entry.DisplayName, PixelUI.Label);
            bool hasRecord = LevelRecordStore.TryGetBestTime(entry.LevelId, out float best);
            GUI.Label(new Rect(rect.x + 12, rect.y + 32, rect.width - 142, 26), "BEST " + (hasRecord ? LevelRecordStore.FormatTime(best) : noRecordText), PixelUI.Label);
            if (PixelUI.Button(new Rect(rect.xMax - 114, rect.y + 12, 102, 40), "开始 >")) StartLevel(entry.SceneName);
            if (allowClearingRecords && hasRecord && PixelUI.Button(new Rect(rect.xMax - 238, rect.y + 30, 114, 30), "清除记录"))
                LevelRecordStore.ClearRecord(entry.LevelId);
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
