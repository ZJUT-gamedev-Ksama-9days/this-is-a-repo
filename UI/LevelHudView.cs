using System;
using UnityEngine;
using UnityEngine.UI;

namespace GravityPrototype.UI
{
    /// <summary>Top bar of the level screen: level name, live run time and the side-menu button.</summary>
    [DisallowMultipleComponent]
    public sealed class LevelHudView : MonoBehaviour
    {
        [SerializeField] private Text levelLabel;
        [SerializeField] private Text timeLabel;
        [SerializeField] private Text healthLabel;
        [SerializeField] private Image healthFill;
        [SerializeField] private Button menuButton;

        private bool listenersBound;
        private float lastHealthCurrent = -1f;
        private float lastHealthMaximum = -1f;

        public event Action MenuClicked;

        public void SetLevelText(string text)
        {
            EnsureReferences();
            if (levelLabel != null)
            {
                levelLabel.text = text;
            }
        }

        public void SetTimeText(string text)
        {
            EnsureReferences();
            if (timeLabel != null)
            {
                timeLabel.text = text;
            }
        }

        public void SetMenuInteractable(bool interactable)
        {
            EnsureReferences();
            if (menuButton != null)
            {
                menuButton.interactable = interactable;
            }
        }

        public void SetHealth(float current, float maximum)
        {
            if (Mathf.Approximately(current, lastHealthCurrent)
                && Mathf.Approximately(maximum, lastHealthMaximum))
            {
                return;
            }

            lastHealthCurrent = current;
            lastHealthMaximum = maximum;
            EnsureReferences();

            float normalized = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
            if (healthFill != null)
            {
                Vector2 anchorMax = healthFill.rectTransform.anchorMax;
                anchorMax.x = normalized;
                healthFill.rectTransform.anchorMax = anchorMax;
                healthFill.rectTransform.offsetMin = Vector2.zero;
                healthFill.rectTransform.offsetMax = Vector2.zero;
                healthFill.color = Color.Lerp(new Color(0.95f, 0.3f, 0.28f, 1f),
                    new Color(0.35f, 0.9f, 0.6f, 1f), normalized);
            }

            if (healthLabel != null)
            {
                healthLabel.text = "生命 " + Mathf.CeilToInt(current).ToString() + " / "
                    + Mathf.CeilToInt(maximum).ToString();
            }
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void EnsureReferences()
        {
            if (levelLabel == null)
            {
                levelLabel = UiLookup.FindDeep<Text>(transform, "Level Label");
            }

            if (timeLabel == null)
            {
                timeLabel = UiLookup.FindDeep<Text>(transform, "Time Label");
            }

            if (healthLabel == null)
            {
                healthLabel = UiLookup.FindDeep<Text>(transform, "Health Label");
            }

            if (healthFill == null)
            {
                healthFill = UiLookup.FindDeep<Image>(transform, "Health Fill");
            }

            if (menuButton == null)
            {
                menuButton = UiLookup.FindDeep<Button>(transform, "Menu Button");
            }

            if (!listenersBound && menuButton != null)
            {
                menuButton.onClick.AddListener(HandleMenuClicked);
                listenersBound = true;
            }
        }

        private void HandleMenuClicked()
        {
            MenuClicked?.Invoke();
        }
    }
}
