using System;
using UnityEngine;
using UnityEngine.UI;

namespace GravityPrototype.UI
{
    /// <summary>
    /// Top bar of the level screen: level name, live run time, and the "暂停" / "菜单" buttons that sit
    /// next to each other. <see cref="LevelUiFactory.BuildHud"/> adds this component to the code-built
    /// "Top Bar", and the references resolve by child name, so an authored prefab using the same node
    /// names also works. The view only raises <see cref="MenuClicked"/> / <see cref="PauseClicked"/>;
    /// LevelFlowController decides what they mean.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelHudView : MonoBehaviour
    {
        /// <summary>Child names created by <see cref="LevelUiFactory.BuildHud"/>; keep both in sync.</summary>
        public const string LevelLabelObjectName = "Level Label";
        public const string TimeLabelObjectName = "Time Label";
        public const string MenuButtonObjectName = "Menu Button";
        public const string PauseButtonObjectName = "Pause Button";

        [Header("Bar widgets (leave empty to resolve by child name)")]
        [SerializeField] private Text levelLabel;
        [SerializeField] private Text timeLabel;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button pauseButton;

        private bool buttonsBound;
        private bool missingReferencesReported;

        /// <summary>Raised when the player presses the menu button.</summary>
        public event Action MenuClicked;

        /// <summary>Raised when the player presses the pause button.</summary>
        public event Action PauseClicked;

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

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            BindButtons();
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        private void EnsureReferences()
        {
            if (levelLabel == null)
            {
                levelLabel = UiLookup.FindDeep<Text>(transform, LevelLabelObjectName);
            }

            if (timeLabel == null)
            {
                timeLabel = UiLookup.FindDeep<Text>(transform, TimeLabelObjectName);
            }

            if (menuButton == null)
            {
                menuButton = UiLookup.FindDeep<Button>(transform, MenuButtonObjectName);
            }

            if (pauseButton == null)
            {
                pauseButton = UiLookup.FindDeep<Button>(transform, PauseButtonObjectName);
            }

            if (missingReferencesReported
                || (levelLabel != null && timeLabel != null && menuButton != null
                    && pauseButton != null))
            {
                return;
            }

            // Report once: a missing node would otherwise spam the console on every HUD refresh.
            missingReferencesReported = true;
            Debug.LogError(BuildMissingReferenceMessage(), this);
        }

        private void BindButtons()
        {
            if (buttonsBound)
            {
                return;
            }

            buttonsBound = true;
            if (menuButton != null)
            {
                menuButton.onClick.AddListener(HandleMenuClicked);
            }

            if (pauseButton != null)
            {
                pauseButton.onClick.AddListener(HandlePauseClicked);
            }
        }

        private void UnbindButtons()
        {
            if (!buttonsBound)
            {
                return;
            }

            buttonsBound = false;
            if (menuButton != null)
            {
                menuButton.onClick.RemoveListener(HandleMenuClicked);
            }

            if (pauseButton != null)
            {
                pauseButton.onClick.RemoveListener(HandlePauseClicked);
            }
        }

        private void HandleMenuClicked()
        {
            MenuClicked?.Invoke();
        }

        private void HandlePauseClicked()
        {
            PauseClicked?.Invoke();
        }

        private string BuildMissingReferenceMessage()
        {
            string missing = string.Empty;
            if (levelLabel == null)
            {
                missing = LevelLabelObjectName;
            }

            if (timeLabel == null)
            {
                missing = AppendName(missing, TimeLabelObjectName);
            }

            if (menuButton == null)
            {
                missing = AppendName(missing, MenuButtonObjectName);
            }

            if (pauseButton == null)
            {
                missing = AppendName(missing, PauseButtonObjectName);
            }

            return "LevelHudView on '" + BuildHierarchyPath()
                + "' is missing the UI node(s): " + missing
                + ". Create those children with these exact names (LevelUiFactory.BuildHud does this)"
                + " or assign the fields in the Inspector.";
        }

        private static string AppendName(string current, string name)
        {
            return current.Length > 0 ? current + ", " + name : name;
        }

        private string BuildHierarchyPath()
        {
            string path = name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }
    }
}
