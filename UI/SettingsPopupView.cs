using System;
using GravityPrototype.Progress;
using UnityEngine;
using UnityEngine.UI;

namespace GravityPrototype.UI
{
    /// <summary>
    /// Settings popup shared by the level screen (and reachable again from the pause popup). Values are
    /// written straight into <see cref="GameSettingsStore"/>, which persists and applies them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsPopupView : MonoBehaviour
    {
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Text masterValueLabel;
        [SerializeField] private Slider effectSlider;
        [SerializeField] private Text effectValueLabel;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button closeButton;

        private bool listenersBound;
        private bool syncing;

        public event Action Closed;

        public bool IsVisible => gameObject.activeSelf;

        public void Show()
        {
            EnsureReferences();
            gameObject.SetActive(true);
            SyncFromStore();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void EnsureReferences()
        {
            if (masterSlider == null)
            {
                masterSlider = UiLookup.FindDeep<Slider>(transform, "Master Slider");
            }

            if (masterValueLabel == null)
            {
                masterValueLabel = UiLookup.FindDeep<Text>(transform, "Master Value");
            }

            if (effectSlider == null)
            {
                effectSlider = UiLookup.FindDeep<Slider>(transform, "Effect Slider");
            }

            if (effectValueLabel == null)
            {
                effectValueLabel = UiLookup.FindDeep<Text>(transform, "Effect Value");
            }

            if (fullscreenToggle == null)
            {
                fullscreenToggle = UiLookup.FindDeep<Toggle>(transform, "Fullscreen Toggle");
            }

            if (resetButton == null)
            {
                resetButton = UiLookup.FindDeep<Button>(transform, "Reset Button");
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
            if (masterSlider != null)
            {
                masterSlider.minValue = 0f;
                masterSlider.maxValue = 1f;
                masterSlider.onValueChanged.AddListener(HandleMasterChanged);
            }

            if (effectSlider != null)
            {
                effectSlider.minValue = 0f;
                effectSlider.maxValue = 1f;
                effectSlider.onValueChanged.AddListener(HandleEffectChanged);
            }

            if (fullscreenToggle != null)
            {
                fullscreenToggle.onValueChanged.AddListener(HandleFullscreenChanged);
            }

            if (resetButton != null)
            {
                resetButton.onClick.AddListener(HandleReset);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleClose);
            }
        }

        private void SyncFromStore()
        {
            syncing = true;
            try
            {
                if (masterSlider != null)
                {
                    masterSlider.SetValueWithoutNotify(GameSettingsStore.MasterVolume);
                }

                if (effectSlider != null)
                {
                    effectSlider.SetValueWithoutNotify(GameSettingsStore.EffectVolume);
                }

                if (fullscreenToggle != null)
                {
                    fullscreenToggle.SetIsOnWithoutNotify(GameSettingsStore.Fullscreen);
                }

                UpdateValueLabels();
            }
            finally
            {
                syncing = false;
            }
        }

        private void HandleMasterChanged(float value)
        {
            if (syncing)
            {
                return;
            }

            GameSettingsStore.MasterVolume = value;
            UpdateValueLabels();
        }

        private void HandleEffectChanged(float value)
        {
            if (syncing)
            {
                return;
            }

            GameSettingsStore.EffectVolume = value;
            UpdateValueLabels();
        }

        private void HandleFullscreenChanged(bool value)
        {
            if (syncing)
            {
                return;
            }

            GameSettingsStore.Fullscreen = value;
        }

        private void HandleReset()
        {
            GameSettingsStore.ResetToDefaults();
            SyncFromStore();
        }

        private void HandleClose()
        {
            Closed?.Invoke();
        }

        private void UpdateValueLabels()
        {
            if (masterValueLabel != null)
            {
                masterValueLabel.text = Mathf.RoundToInt(GameSettingsStore.MasterVolume * 100f) + "%";
            }

            if (effectValueLabel != null)
            {
                effectValueLabel.text = Mathf.RoundToInt(GameSettingsStore.EffectVolume * 100f) + "%";
            }
        }
    }
}
