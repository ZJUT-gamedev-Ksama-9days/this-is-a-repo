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
        [SerializeField] private Slider resolutionSlider;
        [SerializeField] private Text resolutionValueLabel;
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

            if (resolutionSlider == null)
            {
                resolutionSlider = UiLookup.FindDeep<Slider>(transform, "Resolution Slider");
            }

            if (resolutionValueLabel == null)
            {
                resolutionValueLabel = UiLookup.FindDeep<Text>(transform, "Resolution Value");
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
                masterSlider.maxValue = GameSettingsStore.VolumeSteps.Length - 1;
                masterSlider.wholeNumbers = true;
                masterSlider.onValueChanged.AddListener(HandleMasterChanged);
            }

            if (effectSlider != null)
            {
                effectSlider.minValue = 0f;
                effectSlider.maxValue = GameSettingsStore.VolumeSteps.Length - 1;
                effectSlider.wholeNumbers = true;
                effectSlider.onValueChanged.AddListener(HandleEffectChanged);
            }

            if (resolutionSlider != null)
            {
                resolutionSlider.minValue = 0f;
                resolutionSlider.maxValue = GameSettingsStore.Resolutions.Length - 1;
                resolutionSlider.wholeNumbers = true;
                resolutionSlider.onValueChanged.AddListener(HandleResolutionChanged);
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
                    masterSlider.SetValueWithoutNotify(GameSettingsStore.MasterStep);
                }

                if (effectSlider != null)
                {
                    effectSlider.SetValueWithoutNotify(GameSettingsStore.EffectStep);
                }

                if (resolutionSlider != null)
                {
                    resolutionSlider.SetValueWithoutNotify(GameSettingsStore.ResolutionIndex);
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

            GameSettingsStore.MasterStep = Mathf.RoundToInt(value);
            UpdateValueLabels();
        }

        private void HandleEffectChanged(float value)
        {
            if (syncing)
            {
                return;
            }

            GameSettingsStore.EffectStep = Mathf.RoundToInt(value);
            UpdateValueLabels();
        }

        private void HandleResolutionChanged(float value)
        {
            if (syncing)
            {
                return;
            }

            GameSettingsStore.ResolutionIndex = Mathf.RoundToInt(value);
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
                masterValueLabel.text = "档 " + (GameSettingsStore.MasterStep + 1) + " / " + GameSettingsStore.VolumeSteps.Length;
            }

            if (effectValueLabel != null)
            {
                effectValueLabel.text = "档 " + (GameSettingsStore.EffectStep + 1) + " / " + GameSettingsStore.VolumeSteps.Length;
            }

            if (resolutionValueLabel != null)
            {
                Vector2Int size = GameSettingsStore.CurrentResolution;
                resolutionValueLabel.text = size.x + " × " + size.y;
            }
        }
    }
}
