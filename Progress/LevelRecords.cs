using System.Globalization;
using UnityEngine;

namespace GravityPrototype.Progress
{
    /// <summary>
    /// Read/format layer for the persisted best times owned by <see cref="LevelRecordStore"/>.
    /// The UI needs the "mm' ss''" style and the "?' ?''" placeholder, while the storage keeps raw
    /// seconds so a record can still be compared and formatted elsewhere.
    /// </summary>
    public static class LevelRecords
    {
        /// <summary>Shown for a level that has never been completed.</summary>
        public const string NoRecordDisplay = "?' ?''";

        public static bool TryGetRecord(string levelId, out float seconds)
        {
            return LevelRecordStore.TryGetBestTime(levelId, out seconds);
        }

        /// <summary>Formats seconds as mm' ss''; zero is valid, invalid input uses the placeholder.</summary>
        public static string FormatRecord(float seconds)
        {
            if (!IsDisplayable(seconds))
            {
                return NoRecordDisplay;
            }

            int totalSeconds = Mathf.FloorToInt(seconds);
            int minutes = totalSeconds / 60;
            int rest = totalSeconds % 60;
            return string.Format(CultureInfo.InvariantCulture, "{0:00}' {1:00}''", minutes, rest);
        }

        /// <summary>Formats the stored record of a level, or the placeholder when there is none.</summary>
        public static string GetRecordDisplay(string levelId)
        {
            return TryGetRecord(levelId, out float seconds) ? FormatRecord(seconds) : NoRecordDisplay;
        }

        /// <summary>Stores the time only when it beats the saved record. Returns true on a new best.</summary>
        public static bool SubmitRecord(string levelId, float seconds)
        {
            return LevelRecordStore.SubmitTime(levelId, seconds);
        }

        public static int CountCompleted(LevelCatalog catalog)
        {
            if (catalog == null)
            {
                return 0;
            }

            int completed = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                LevelDefinition level = catalog.GetLevel(i);
                if (level != null && LevelRecordStore.HasRecord(level.LevelId))
                {
                    completed++;
                }
            }

            return completed;
        }

        /// <summary>
        /// Number of the furthest level the player may start: the first level without a record, or the
        /// last level once everything has been completed. Used as the "current progress" of a save.
        /// </summary>
        public static int FindHighestUnlockedIndex(LevelCatalog catalog)
        {
            if (catalog == null || catalog.Count == 0)
            {
                return 0;
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                LevelDefinition level = catalog.GetLevel(i);
                if (level == null || !LevelRecordStore.HasRecord(level.LevelId))
                {
                    return i;
                }
            }

            return catalog.Count - 1;
        }

        private static bool IsDisplayable(float seconds)
        {
            return seconds >= 0f && !float.IsNaN(seconds) && !float.IsInfinity(seconds);
        }
    }
}
