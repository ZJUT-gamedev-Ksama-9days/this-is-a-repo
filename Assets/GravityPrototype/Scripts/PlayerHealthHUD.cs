using UnityEngine;

namespace GravityPrototype
{
    /// <summary>Prototype HUD using IMGUI, matching the sandbox hints without extra UI packages.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerHealthHUD : MonoBehaviour
    {
        private PlayerHealth health;
        private PlayerFallDamage2D fallDamage;
        private GUIStyle labelStyle;
        private GUIStyle detailStyle;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
            fallDamage = GetComponent<PlayerFallDamage2D>();
        }

        private void OnGUI()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
                labelStyle.normal.textColor = Color.white;
                detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
                detailStyle.normal.textColor = new Color(0.8f, 0.9f, 1f);
            }

            float width = Mathf.Min(280f, Screen.width - 32f);
            float left = Screen.width - width - 16f;
            // Keep clear of the existing top-left operation panel at narrow resolutions.
            float top = Screen.width >= 1080 ? 16f : 212f;
            GUI.Box(new Rect(left, top, width, 140f), GUIContent.none);
            GUI.Label(new Rect(left + 12f, top + 7f, width - 24f, 26f),
                $"PLAYER HP  {health.CurrentHealth:0.#} / {health.MaxHealth:0.#}", labelStyle);

            Rect bar = new Rect(left + 12f, top + 40f, width - 24f, 22f);
            Color previousColor = GUI.color;
            GUI.color = new Color(0.09f, 0.12f, 0.17f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            Color fill = Color.Lerp(new Color(0.95f, 0.22f, 0.24f),
                new Color(0.18f, 0.85f, 0.55f), health.Fraction);
            if (Time.time - health.LastDamageTime < 0.2f)
            {
                fill = Color.white;
            }

            GUI.color = fill;
            GUI.DrawTexture(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * health.Fraction,
                bar.height - 4f), Texture2D.whiteTexture);
            GUI.color = previousColor;

            string message = "Avoid fast-moving balls";
            if (health.IsDead)
            {
                message = "DEAD - Press R to restart";
            }
            else if (health.LastDamageSource == PlayerDamageSource.BallImpact)
            {
                message = $"Head hit  /  -{health.LastDamage:0.#} HP";
            }
            else if (health.LastDamageSource == PlayerDamageSource.Fall && fallDamage != null)
            {
                message = $"Fall {fallDamage.LastDamageFallHeight:0.0} m  /  -{fallDamage.LastDamage:0.#} HP";
            }

            GUI.Label(new Rect(left + 12f, top + 72f, width - 24f, 28f), message, detailStyle);
        }
    }
}
