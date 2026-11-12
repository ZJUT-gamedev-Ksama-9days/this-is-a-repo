using System;
using UnityEngine;

namespace GravityPrototype.Progress
{
    /// <summary>
    /// 玩家设置（设计文档 2.8.2.1）：窗口大小 3 档、主音量/音效音量 5 档、全屏开关。
    /// 值写入 PlayerPrefs 并立即生效；窗口大小和全屏只在“改变时”应用，不在场景加载时强制套用，
    /// 以免编辑器 Game 视图被意外改尺寸。
    /// </summary>
    public static class GameSettingsStore
    {
        /// <summary>窗口大小 3 档（设计文档 2.8.2.1）。</summary>
        public static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080)
        };

        /// <summary>音量 5 档：0% / 25% / 50% / 75% / 100%。</summary>
        public static readonly float[] VolumeSteps = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        public const int DefaultResolutionIndex = 1;
        public const int DefaultVolumeStep = 3;
        public const bool DefaultFullscreen = true;

        private const string MasterStepKey = "GravityPrototype.Settings.MasterStep";
        private const string EffectStepKey = "GravityPrototype.Settings.EffectStep";
        private const string FullscreenKey = "GravityPrototype.Settings.Fullscreen";
        private const string ResolutionKey = "GravityPrototype.Settings.Resolution";

        private static bool loaded;
        private static int masterStep = DefaultVolumeStep;
        private static int effectStep = DefaultVolumeStep;
        private static bool fullscreen = DefaultFullscreen;
        private static int resolutionIndex = DefaultResolutionIndex;

        /// <summary>Raised after any value or the applied state changed.</summary>
        public static event Action Changed;

        public static float MasterVolume
        {
            get { EnsureLoaded(); return VolumeSteps[masterStep]; }
            set { MasterStep = NearestStep(value); }
        }

        public static float EffectVolume
        {
            get { EnsureLoaded(); return VolumeSteps[effectStep]; }
            set { EffectStep = NearestStep(value); }
        }

        /// <summary>主音量档位 0..4。</summary>
        public static int MasterStep
        {
            get { EnsureLoaded(); return masterStep; }
            set
            {
                EnsureLoaded();
                masterStep = Mathf.Clamp(value, 0, VolumeSteps.Length - 1);
                AudioListener.volume = VolumeSteps[masterStep];
                Persist();
                Changed?.Invoke();
            }
        }

        /// <summary>音效音量档位 0..4。</summary>
        public static int EffectStep
        {
            get { EnsureLoaded(); return effectStep; }
            set
            {
                EnsureLoaded();
                effectStep = Mathf.Clamp(value, 0, VolumeSteps.Length - 1);
                Persist();
                Changed?.Invoke();
            }
        }

        public static bool Fullscreen
        {
            get { EnsureLoaded(); return fullscreen; }
            set
            {
                EnsureLoaded();
                fullscreen = value;
                ApplyFullscreen();
                Persist();
                Changed?.Invoke();
            }
        }

        /// <summary>窗口大小档位 0..2，改变时立即套用。</summary>
        public static int ResolutionIndex
        {
            get { EnsureLoaded(); return resolutionIndex; }
            set
            {
                EnsureLoaded();
                resolutionIndex = Mathf.Clamp(value, 0, Resolutions.Length - 1);
                ApplyResolution();
                Persist();
                Changed?.Invoke();
            }
        }

        public static Vector2Int CurrentResolution => Resolutions[Mathf.Clamp(ResolutionIndex, 0, Resolutions.Length - 1)];

        public static void Load()
        {
            loaded = true;
            masterStep = Mathf.Clamp(PlayerPrefs.GetInt(MasterStepKey, DefaultVolumeStep), 0, VolumeSteps.Length - 1);
            effectStep = Mathf.Clamp(PlayerPrefs.GetInt(EffectStepKey, DefaultVolumeStep), 0, VolumeSteps.Length - 1);
            fullscreen = PlayerPrefs.GetInt(FullscreenKey, DefaultFullscreen ? 1 : 0) == 1;
            resolutionIndex = Mathf.Clamp(PlayerPrefs.GetInt(ResolutionKey, DefaultResolutionIndex), 0, Resolutions.Length - 1);
            ApplyAll();
            Changed?.Invoke();
        }

        public static void ResetToDefaults()
        {
            loaded = true;
            masterStep = DefaultVolumeStep;
            effectStep = DefaultVolumeStep;
            fullscreen = DefaultFullscreen;
            resolutionIndex = DefaultResolutionIndex;
            ApplyAll();
            Persist();
            Changed?.Invoke();
        }

        /// <summary>重新套用音量（例如换场景后 AudioListener 被替换）。窗口/全屏不在这里强制套用。</summary>
        public static void ApplyAll()
        {
            AudioListener.volume = VolumeSteps[masterStep];
        }

        private static int NearestStep(float value)
        {
            float clamped = Mathf.Clamp01(value);
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < VolumeSteps.Length; i++)
            {
                float distance = Mathf.Abs(VolumeSteps[i] - clamped);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        private static void ApplyResolution()
        {
            Vector2Int size = Resolutions[Mathf.Clamp(resolutionIndex, 0, Resolutions.Length - 1)];
            if (Screen.width == size.x && Screen.height == size.y && !Screen.fullScreen) return;
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
        }

        private static void ApplyFullscreen()
        {
            if (Screen.fullScreen != fullscreen)
            {
                Screen.fullScreen = fullscreen;
            }
        }

        private static void Persist()
        {
            PlayerPrefs.SetInt(MasterStepKey, masterStep);
            PlayerPrefs.SetInt(EffectStepKey, effectStep);
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(ResolutionKey, resolutionIndex);
            PlayerPrefs.Save();
        }

        private static void EnsureLoaded()
        {
            if (!loaded)
            {
                Load();
            }
        }
    }
}
