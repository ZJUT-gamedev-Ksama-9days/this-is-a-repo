using System;

namespace GravityPrototype.Progress
{
    /// <summary>
    /// Payload of the single save slot. It is a plain [Serializable] class so it round-trips through
    /// JsonUtility without pulling in a serializer dependency.
    /// </summary>
    [Serializable]
    public sealed class SaveGameData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int levelIndex;
        public string levelId = string.Empty;
        public float elapsedSeconds;
        public int highestUnlockedIndex;
        public string savedAtUtc = string.Empty;

        public bool IsUsable =>
            version > 0
            && levelIndex >= 0
            && !string.IsNullOrWhiteSpace(levelId)
            && elapsedSeconds >= 0f
            && !float.IsNaN(elapsedSeconds)
            && !float.IsInfinity(elapsedSeconds);

        public static SaveGameData Create(int levelIndex, string levelId, float elapsedSeconds,
            int highestUnlockedIndex)
        {
            return new SaveGameData
            {
                version = CurrentVersion,
                levelIndex = Math.Max(0, levelIndex),
                levelId = levelId ?? string.Empty,
                elapsedSeconds = elapsedSeconds > 0f ? elapsedSeconds : 0f,
                highestUnlockedIndex = Math.Max(0, highestUnlockedIndex),
                savedAtUtc = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture)
            };
        }
    }
}
