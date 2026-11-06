using UnityEngine;

namespace GravityPrototype.Progress
{
    /// <summary>
    /// Single save slot: the JSON payload lives in one PlayerPrefs key, so there is no file IO, no
    /// path handling and no chance of half-written files. Writing the slot again overwrites it, which
    /// is the "click save a second time to update" requirement.
    /// </summary>
    public static class SaveGameStore
    {
        public const string SlotKey = "GravityPrototype.Save.Slot0";

        private static bool resumeRequested;

        public static bool HasSave => PlayerPrefs.HasKey(SlotKey);

        public static bool TryLoad(out SaveGameData data)
        {
            data = null;
            if (!HasSave)
            {
                return false;
            }

            string json = PlayerPrefs.GetString(SlotKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<SaveGameData>(json);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Save slot could not be parsed and will be ignored: " + exception.Message);
                data = null;
                return false;
            }

            if (data == null || !data.IsUsable)
            {
                data = null;
                return false;
            }

            return true;
        }

        /// <summary>Overwrites the only slot and flushes PlayerPrefs immediately.</summary>
        public static void Write(SaveGameData data)
        {
            if (data == null || !data.IsUsable)
            {
                Debug.LogWarning("Refusing to write an unusable save payload.");
                return;
            }

            PlayerPrefs.SetString(SlotKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(SlotKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Carries the "continue from save" intent across the scene load that the level menu starts;
        /// the flag is consumed by the level flow controller of the next loaded scene.
        /// </summary>
        public static void RequestResumeOnNextLevel()
        {
            resumeRequested = true;
        }

        public static bool ConsumeResumeRequest()
        {
            bool requested = resumeRequested;
            resumeRequested = false;
            return requested;
        }
    }
}
