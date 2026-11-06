using UnityEngine;
using UnityEngine.UI;

namespace GravityPrototype.UI
{
    /// <summary>One record line: level number, level name, best time and the completed/uncompleted look.</summary>
    [DisallowMultipleComponent]
    public sealed class LevelRecordRowView : MonoBehaviour
    {
        private static readonly Color CompletedColor = new Color(0.65f, 0.93f, 1f);
        private static readonly Color PendingColor = new Color(0.62f, 0.68f, 0.76f);
        private static readonly Color PendingBackground = new Color(0.12f, 0.16f, 0.22f, 0.9f);
        private static readonly Color CompletedBackground = new Color(0.15f, 0.25f, 0.32f, 0.95f);

        [SerializeField] private Text indexLabel;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text timeLabel;
        [SerializeField] private Image background;

        public void SetData(LevelRecordRowData data)
        {
            EnsureReferences();
            if (indexLabel != null)
            {
                indexLabel.text = (data.LevelIndex + 1).ToString("00");
            }

            if (nameLabel != null)
            {
                nameLabel.text = data.DisplayName;
            }

            if (timeLabel != null)
            {
                timeLabel.text = data.TimeText;
                timeLabel.color = data.HasRecord ? CompletedColor : PendingColor;
            }

            if (background != null)
            {
                background.color = data.HasRecord ? CompletedBackground : PendingBackground;
            }
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void EnsureReferences()
        {
            if (indexLabel == null)
            {
                indexLabel = UiLookup.FindDeep<Text>(transform, "Index Label");
            }

            if (nameLabel == null)
            {
                nameLabel = UiLookup.FindDeep<Text>(transform, "Name Label");
            }

            if (timeLabel == null)
            {
                timeLabel = UiLookup.FindDeep<Text>(transform, "Time Label");
            }

            if (background == null)
            {
                background = GetComponent<Image>();
            }
        }
    }
}
