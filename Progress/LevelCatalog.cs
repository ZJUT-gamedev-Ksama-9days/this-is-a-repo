using UnityEngine;

namespace GravityPrototype.Progress
{
    /// <summary>
    /// Ordered level table shared by the HUD, the records popup and the "next level" flow. It is a
    /// ScriptableObject so every level scene references the same list instead of duplicating five
    /// hard-coded ids in each scene.
    /// </summary>
    [CreateAssetMenu(menuName = "Gravity Prototype/Level Catalog", fileName = "LevelCatalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField, Tooltip("按关卡顺序排列；下标即关卡编号。")]
        private LevelDefinition[] levels = new LevelDefinition[0];

        public int Count => levels == null ? 0 : levels.Length;

        public LevelDefinition GetLevel(int index)
        {
            return TryGetLevel(index, out LevelDefinition level) ? level : null;
        }

        public bool TryGetLevel(int index, out LevelDefinition level)
        {
            level = null;
            if (levels == null || index < 0 || index >= levels.Length)
            {
                return false;
            }

            level = levels[index];
            return level != null;
        }

        public bool TryGetIndex(string levelId, out int index)
        {
            index = -1;
            if (levels == null || string.IsNullOrWhiteSpace(levelId))
            {
                return false;
            }

            for (int i = 0; i < levels.Length; i++)
            {
                LevelDefinition level = levels[i];
                if (level != null && string.Equals(level.LevelId, levelId.Trim(), System.StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        public bool IsLastLevel(int index)
        {
            return Count > 0 && index == Count - 1;
        }

        /// <summary>Editor builders use this to (re)fill the asset; runtime code only reads.</summary>
        public void SetLevels(LevelDefinition[] newLevels)
        {
            levels = newLevels ?? new LevelDefinition[0];
        }
    }
}
