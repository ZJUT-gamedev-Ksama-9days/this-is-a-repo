using System;
using UnityEngine;
using UnityEngine.UI;

namespace GravityPrototype.UI
{
    /// <summary>
    /// Side menu of the level screen. It only raises intent events; the level flow controller decides
    /// what "save" or "quit" means, so the view stays reusable. "暂停" is not part of this menu: it
    /// lives in the top bar next to "菜单" and is handled by <see cref="LevelHudView"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SideMenuView : MonoBehaviour
    {
        [SerializeField] private Text titleLabel;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button recordsButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button closeButton;

        private bool listenersBound;

        public event Action SettingsClicked;
        public event Action RecordsClicked;
        public event Action SaveClicked;
        public event Action QuitClicked;

        /// <summary>Raised by "返回": go back to the start menu scene.</summary>
        public event Action HomeClicked;

        public event Action CloseClicked;

        public bool IsVisible => gameObject.activeSelf;

        public void Show(bool visible)
        {
            EnsureReferences();
            gameObject.SetActive(visible);
        }

        public void SetTitle(string text)
        {
            EnsureReferences();
            if (titleLabel != null)
            {
                titleLabel.text = text;
            }
        }

        public void SetSaveInteractable(bool interactable)
        {
            EnsureReferences();
            if (saveButton != null)
            {
                saveButton.interactable = interactable;
            }
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void EnsureReferences()
        {
            if (titleLabel == null)
            {
                titleLabel = UiLookup.FindDeep<Text>(transform, "Side Menu Title");
            }

            if (settingsButton == null)
            {
                settingsButton = UiLookup.FindDeep<Button>(transform, "Settings Button");
            }

            if (recordsButton == null)
            {
                recordsButton = UiLookup.FindDeep<Button>(transform, "Records Button");
            }

            if (saveButton == null)
            {
                saveButton = UiLookup.FindDeep<Button>(transform, "Save Button");
            }

            if (quitButton == null)
            {
                quitButton = UiLookup.FindDeep<Button>(transform, "Quit Button");
            }

            if (homeButton == null)
            {
                homeButton = UiLookup.FindDeep<Button>(transform, "Home Button");
            }

            if (closeButton == null)
            {
                closeButton = UiLookup.FindDeep<Button>(transform, "Close Button");
            }

            if (listenersBound)
            {
                return;
            }

            listenersBound = true;
            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(HandleSettings);
            }

            if (recordsButton != null)
            {
                recordsButton.onClick.AddListener(HandleRecords);
            }

            if (saveButton != null)
            {
                saveButton.onClick.AddListener(HandleSave);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(HandleQuit);
            }

            if (homeButton != null)
            {
                homeButton.onClick.AddListener(HandleHome);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleClose);
            }
        }

        private void HandleSettings()
        {
            SettingsClicked?.Invoke();
        }

        private void HandleRecords()
        {
            RecordsClicked?.Invoke();
        }

        private void HandleSave()
        {
            SaveClicked?.Invoke();
        }

        private void HandleQuit()
        {
            QuitClicked?.Invoke();
        }

        private void HandleHome()
        {
            HomeClicked?.Invoke();
        }

        private void HandleClose()
        {
            CloseClicked?.Invoke();
        }
    }
}
