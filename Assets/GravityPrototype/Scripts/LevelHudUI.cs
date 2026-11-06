using UnityEngine;

namespace GravityPrototype
{
    /// <summary>Prototype IMGUI HUD: health, current run time, persisted best time and run state.</summary>
    [DisallowMultipleComponent]
    public sealed class LevelHudUI : MonoBehaviour
    {
        [SerializeField] private LevelController level;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private bool showControls = false;
        private bool controlsMenuOpen;
        [SerializeField, Tooltip("没有历史记录时显示的文本。")]
        private string noRecordText = LevelRecordStore.NoRecordText;

        private void Awake()
        {
            if (level == null)
            {
                level = FindFirstObjectByType<LevelController>();
            }

            if (health == null && level != null)
            {
                health = level.Player;
            }

            if (health == null)
            {
                health = FindFirstObjectByType<PlayerHealth>();
            }
        }

        private void OnGUI()
        {
            float width = Mathf.Min(500f, Screen.width - 32f);
            PixelUI.Panel(new Rect(16, 16, width, 108));
            GUI.Label(new Rect(32, 24, width - 32, 28), "SPIKE ASCENT / " + (level != null ? level.LevelId : ""), PixelUI.Title);
            string best = level != null && level.TryGetBestSeconds(out float seconds)
                ? LevelRecordStore.FormatTime(seconds) : noRecordText;
            GUI.Label(new Rect(32, 56, width - 32, 24), "TIME " + LevelRecordStore.FormatTime(DisplayedRunSeconds()) + "   BEST " + best, PixelUI.Label);
            GUI.Label(new Rect(32, 83, width - 32, 26), StatusText(), PixelUI.Label);
            if (PixelUI.Button(new Rect(16, 136, 180, 38), controlsMenuOpen ? "关闭说明" : "操作说明"))
                controlsMenuOpen = !controlsMenuOpen;
            if (showControls || controlsMenuOpen)
                PixelUI.Controls(new Rect(16, 186, Mathf.Min(600, Screen.width - 32), 480), true);
        }

        private float DisplayedRunSeconds()
        {
            if (level == null)
            {
                return 0f;
            }

            if (level.IsCompleted)
            {
                return level.LastRunSeconds;
            }

            return level.Timer != null ? level.Timer.ElapsedSeconds : 0f;
        }

        private string StatusText()
        {
            if (level == null)
            {
                return "No LevelController in scene";
            }

            if (level.IsCompleted)
            {
                return level.LastRunWasBest
                    ? "GOAL REACHED - NEW BEST TIME SAVED"
                    : "GOAL REACHED - press R to beat the record";
            }

            if (level.IsDeathPending)
            {
                return "死亡 — 即将重新开始";
            }

            return "抵达终点，避开致命危险。";
        }

    }
}
