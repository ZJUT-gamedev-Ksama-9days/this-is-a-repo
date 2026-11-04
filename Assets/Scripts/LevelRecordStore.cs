using System.Globalization;
using UnityEngine;

namespace GravityPrototype
{
    /// <summary>
    /// Persistent per-level best times. PlayerPrefs keeps the record across sessions without adding
    /// an asset or save-file dependency; the key is derived from the level id.
    /// </summary>
    public static class LevelRecordStore
    {
        public const string KeyPrefix = "GravityPrototype.BestTime.";
        public const string NoRecordText = "暂无记录";

        public static string BuildKey(string levelId)
        {
            string id = string.IsNullOrWhiteSpace(levelId) ? "Default" : levelId.Trim();
            return KeyPrefix + id;
        }

        public static bool TryGetBestTime(string levelId, out float seconds)
        {
            seconds = 0f;
            string key = BuildKey(levelId);
            if (!PlayerPrefs.HasKey(key))
            {
                return false;
            }

            float stored = PlayerPrefs.GetFloat(key, 0f);
            if (!IsValidTime(stored))
            {
                return false;
            }

            seconds = stored;
            return true;
        }

        public static bool HasRecord(string levelId)
        {
            return TryGetBestTime(levelId, out _);
        }

        /// <summary>Stores the time only when it beats the saved record. Returns true on a new best.</summary>
        public static bool SubmitTime(string levelId, float seconds)
        {
            if (!IsValidTime(seconds))
            {
                return false;
            }

            if (TryGetBestTime(levelId, out float best) && best <= seconds)
            {
                return false;
            }

            PlayerPrefs.SetFloat(BuildKey(levelId), seconds);
            PlayerPrefs.Save();
            return true;
        }

        public static string DescribeBestTime(string levelId)
        {
            return TryGetBestTime(levelId, out float seconds) ? FormatTime(seconds) : NoRecordText;
        }

        /// <summary>Formats seconds as mm:ss.mmm; negative or non-finite values use a placeholder.</summary>
        public static string FormatTime(float seconds)
        {
            if (seconds < 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                return "--:--.---";
            }

            int minutes = Mathf.FloorToInt(seconds / 60f);
            float remainder = seconds - minutes * 60f;
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00.000}", minutes, remainder);
        }

        public static void ClearRecord(string levelId)
        {
            PlayerPrefs.DeleteKey(BuildKey(levelId));
            PlayerPrefs.Save();
        }

        private static bool IsValidTime(float seconds)
        {
            return seconds > 0f && !float.IsNaN(seconds) && !float.IsInfinity(seconds);
        }
    }
}
