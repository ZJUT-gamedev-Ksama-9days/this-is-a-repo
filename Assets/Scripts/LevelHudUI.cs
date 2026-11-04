using UnityEngine;

namespace GravityPrototype
{
    /// <summary>Prototype IMGUI HUD: health, current run time, persisted best time and run state.</summary>
    [DisallowMultipleComponent]
    public sealed class LevelHudUI : MonoBehaviour
    {
        [SerializeField] private LevelController level;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private bool showControls = true;
        [SerializeField, Tooltip("没有历史记录时显示的文本。")]
        private string noRecordText = LevelRecordStore.NoRecordText;

        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle barTextStyle;

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
            EnsureStyles();
            float width = Mathf.Min(620f, Screen.width - 32f);
            const float height = 168f;
            GUI.Box(new Rect(16f, 16f, width, height), GUIContent.none);
            GUI.Label(new Rect(32f, 24f, width - 32f, 30f),
                level != null ? "LEVEL " + level.LevelId : "LEVEL", titleStyle);

            Rect bar = new Rect(32f, 62f, width - 64f, 18f);
            float normalized = health != null ? health.NormalizedHealth : 0f;
            float current = health != null ? health.CurrentHealth : 0f;
            float maximum = health != null ? health.MaxHealth : 0f;
            GUI.color = new Color(0.07f, 0.09f, 0.13f, 0.95f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = Color.Lerp(new Color(0.95f, 0.25f, 0.22f), new Color(0.3f, 0.9f, 0.55f), normalized);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * normalized, bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(bar, $"HEALTH {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}", barTextStyle);

            string timeText = LevelRecordStore.FormatTime(DisplayedRunSeconds());
            string bestText = noRecordText;
            if (level != null && level.TryGetBestSeconds(out float best))
            {
                bestText = LevelRecordStore.FormatTime(best);
            }

            GUI.Label(new Rect(32f, 92f, width - 32f, 24f), "TIME " + timeText + "     BEST " + bestText, textStyle);
            GUI.Label(new Rect(32f, 120f, width - 32f, 24f), StatusText(), textStyle);
            if (showControls)
            {
                GUI.Label(new Rect(32f, 146f, width - 32f, 20f),
                    "A / D move     Space jump     RMB drag pad     R restart", textStyle);
            }
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
                return "NO HEALTH LEFT - RESETTING LEVEL";
            }

            return "Reach the goal. Spikes drain health over contact time.";
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = Color.white;
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            textStyle.normal.textColor = new Color(0.65f, 0.88f, 0.95f);
            barTextStyle = new GUIStyle(textStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            barTextStyle.normal.textColor = Color.white;
        }
    }
}
