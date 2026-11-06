using UnityEngine;

namespace GravityPrototype
{
    /// <summary>Prototype HUD using IMGUI, matching the sandbox hints without extra UI packages.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerHealthHUD : MonoBehaviour
    {
        private PlayerHealth health;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
        }

        private void OnGUI()
        {
            if (!health.IsDead) return;
            float width = Mathf.Min(340f, Screen.width - 32f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, 24f, width, 52f);
            PixelUI.Panel(panel);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 10f, width - 24f, 32f),
                "死亡 — 按 R 重新开始", PixelUI.Label);
        }
    }
}
