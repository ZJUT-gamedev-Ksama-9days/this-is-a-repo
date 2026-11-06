using System;
using UnityEngine;

namespace GravityPrototype.Progress
{
    /// <summary>
    /// One entry of the level table: the id used for records and saves, the label shown in the UI and
    /// the scene that has to be loaded for it. The index inside <see cref="LevelCatalog"/> is the
    /// level number used by "next level" and the records popup, so the array order matters.
    /// </summary>
    [Serializable]
    public sealed class LevelDefinition
    {
        [SerializeField] private string levelId = "Level01";
        [SerializeField] private string displayName = "第 1 关";
        [SerializeField] private string sceneName = "PlatformLevel";

        public LevelDefinition()
        {
        }

        public LevelDefinition(string levelId, string displayName, string sceneName)
        {
            Configure(levelId, displayName, sceneName);
        }

        public string LevelId => levelId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? levelId : displayName;
        public string SceneName => sceneName;

        /// <summary>Editor builders use this to fill the catalog asset; runtime code only reads.</summary>
        public void Configure(string id, string name, string scene)
        {
            levelId = string.IsNullOrWhiteSpace(id) ? "Level01" : id.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? levelId : name.Trim();
            sceneName = string.IsNullOrWhiteSpace(scene) ? "PlatformLevel" : scene.Trim();
        }
    }
}
