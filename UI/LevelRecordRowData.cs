namespace GravityPrototype.UI
{
    /// <summary>One line of the records popup: level number, label, formatted record and completion state.</summary>
    public readonly struct LevelRecordRowData
    {
        public LevelRecordRowData(int levelIndex, string displayName, string timeText, bool hasRecord)
        {
            LevelIndex = levelIndex;
            DisplayName = string.IsNullOrEmpty(displayName) ? "Level " + (levelIndex + 1) : displayName;
            TimeText = string.IsNullOrEmpty(timeText) ? "?' ?''" : timeText;
            HasRecord = hasRecord;
        }

        public int LevelIndex { get; }
        public string DisplayName { get; }
        public string TimeText { get; }
        public bool HasRecord { get; }
    }
}
