using System;
using UnityEngine;

namespace GravityPrototype.Progress
{
    /// <summary>
    /// Player settings shared by every scene: master volume, effect volume and fullscreen. Values are
    /// persisted in PlayerPrefs and applied immediately, so the settings popup only has to set a
    /// property. The effect volume is stored for the audio layer to consume; there is no audio mixer
    /// in this prototype yet, so only the master volume changes AudioListener.volume. Fullscreen is
    /// applied when it changes, never while loading a scene, so the editor game view stays untouched.
    /// </summary>
    public static class GameSettingsStore
    {
        public const float DefaultVolume = 0.8f;
        public const bool DefaultFullscreen = true;

        private const string MasterVolumeKey = "GravityPrototype.Settings.MasterVolume";
        private const string EffectVolumeKey = "GravityPrototype.Settings.EffectVolume";
        private const string FullscreenKey = "GravityPrototype.Settings.Fullscreen";

        private static bool loaded;
        private static float masterVolume = DefaultVolume;
        private static float effectVolume = DefaultVolume;
        private static bool fullscreen = DefaultFullscreen;

        /// <summary>Raised after any value or the applied state changed.</summary>
        public static event Action Changed;

        public static float MasterVolume
        {
            get { EnsureLoaded(); return masterVolume; }
            set
            {
                EnsureLoaded();
                masterVolume = Mathf.Clamp01(value);
                AudioListener.volume = masterVolume;
                Persist();
                Changed?.Invoke();
            }
        }

        public static float EffectVolume
        {
            get { EnsureLoaded(); return effectVolume; }
            set
            {
                EnsureLoaded();
                effectVolume = Mathf.Clamp01(value);
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

        public static void Load()
        {
            loaded = true;
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultVolume));
            effectVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectVolumeKey, DefaultVolume));
            fullscreen = PlayerPrefs.GetInt(FullscreenKey, DefaultFullscreen ? 1 : 0) == 1;
            ApplyAll();
            Changed?.Invoke();
        }

        public static void ResetToDefaults()
        {
            loaded = true;
            masterVolume = DefaultVolume;
            effectVolume = DefaultVolume;
            fullscreen = DefaultFullscreen;
            ApplyAll();
            Persist();
            Changed?.Invoke();
        }

        /// <summary>Re-applies the stored volume, e.g. after a scene load replaced the AudioListener.</summary>
        public static void ApplyAll()
        {
            AudioListener.volume = masterVolume;
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
            PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
            PlayerPrefs.SetFloat(EffectVolumeKey, effectVolume);
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
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
