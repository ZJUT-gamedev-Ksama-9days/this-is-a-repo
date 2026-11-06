using System;
using UnityEngine;
using UnityEngine.UI;

namespace GravityPrototype.UI
{
    /// <summary>Pause popup with "resume" and "quit"; it never touches Time.timeScale itself.</summary>
    [DisallowMultipleComponent]
    public sealed class PausePopupView : MonoBehaviour
    {
        [SerializeField] private Text titleLabel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button quitButton;

        private bool listenersBound;

        public event Action ResumeClicked;
        public event Action QuitClicked;

        public bool IsVisible => gameObject.activeSelf;

        public void Show()
        {
            EnsureReferences();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetTitle(string text)
        {
            EnsureReferences();
            if (titleLabel != null)
            {
                titleLabel.text = string.IsNullOrEmpty(text) ? "已暂停" : text;
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
                titleLabel = UiLookup.FindDeep<Text>(transform, "Pause Title");
            }

            if (resumeButton == null)
            {
                resumeButton = UiLookup.FindDeep<Button>(transform, "Resume Button");
            }

            if (quitButton == null)
            {
                quitButton = UiLookup.FindDeep<Button>(transform, "Quit Button");
            }

            if (listenersBound)
            {
                return;
            }

            listenersBound = true;
            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(HandleResume);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(HandleQuit);
            }
        }

        private void HandleResume()
        {
            ResumeClicked?.Invoke();
        }

        private void HandleQuit()
        {
            QuitClicked?.Invoke();
        }
    }
}
