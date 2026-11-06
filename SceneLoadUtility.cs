using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityPrototype
{
    /// <summary>
    /// Scene name/path helpers built on UnityEngine.SceneManagement.
    /// The prototype used Application.CanStreamedLevelBeLoaded for its "can this scene be loaded"
    /// check; that is a legacy loading API, so the check now walks the build settings through
    /// SceneUtility/SceneManager, which is what Unity 6 keeps supporting.
    /// </summary>
    public static class SceneLoadUtility
    {
        /// <summary>True when a scene with that name is enabled in Build Settings.</summary>
        public static bool CanLoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (string.Equals(GetSceneName(path), sceneName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Loads the scene by name and reports why nothing happened instead of throwing.</summary>
        public static bool TryLoadScene(string sceneName)
        {
            if (!CanLoadScene(sceneName))
            {
                Debug.LogWarning("Scene '" + sceneName
                    + "' is not in Build Settings; add it before starting the level.");
                return false;
            }

            SceneManager.LoadScene(sceneName);
            return true;
        }

        /// <summary>Reloads the scene the given component lives in; false when it is not loadable.</summary>
        public static bool TryReloadScene(Scene scene)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.name))
            {
                return false;
            }

            return TryLoadScene(scene.name);
        }

        /// <summary>Scene name without folder and extension, e.g. "PlatformLevel".</summary>
        public static string GetSceneName(string scenePath)
        {
            return string.IsNullOrEmpty(scenePath) ? string.Empty : Path.GetFileNameWithoutExtension(scenePath);
        }

        /// <summary>Name of the currently active scene; replaces Application.loadedLevelName.</summary>
        public static string GetActiveSceneName()
        {
            Scene active = SceneManager.GetActiveScene();
            return active.IsValid() ? active.name : string.Empty;
        }
    }
}
