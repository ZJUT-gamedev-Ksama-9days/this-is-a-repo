namespace GravityPrototype.UI
{
    /// <summary>Which footer buttons the shared records popup shows.</summary>
    public enum RecordsPopupMode
    {
        /// <summary>Opened from the side menu: records only, closed with "关闭".</summary>
        Browse = 0,

        /// <summary>Opened after one level was completed: "下一关" and "结束游戏".</summary>
        LevelVictory = 1,

        /// <summary>Opened after the last level was completed: "重新开始" and "结束游戏".</summary>
        FinalVictory = 2
    }
}
