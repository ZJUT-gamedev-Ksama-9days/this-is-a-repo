using System;
using UnityEngine;
using UnityEngine.UI;

namespace GravityPrototype.UI
{
    /// <summary>
    /// Side menu of the level screen. It only raises intent events; the level flow controller decides
    /// what "save" or "quit" means, so the view stays reusable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SideMenuView : MonoBehaviour
    {
        [SerializeField] private Text titleLabel;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button recordsButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button closeButton;

        private bool listenersBound;

        public event Action SettingsClicked;
        public event Action PauseClicked;
        public event Action RecordsClicked;
        public event Action SaveClicked;
        public event Action QuitClicked;
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

            if (pauseButton == null)
            {
                pauseButton = UiLookup.FindDeep<Button>(transform, "Pause Button");
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

            if (pauseButton != null)
            {
                pauseButton.onClick.AddListener(HandlePause);
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

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleClose);
            }
        }

        private void HandleSettings()
        {
            SettingsClicked?.Invoke();
        }

        private void HandlePause()
        {
            PauseClicked?.Invoke();
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

        private void HandleClose()
        {
            CloseClicked?.Invoke();
        }
    }
}
