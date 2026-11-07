using UnityEngine;

namespace GravityPrototype.UI
{
    /// <summary>References to every view created (or found) by <see cref="LevelUiFactory"/>.</summary>
    public sealed class LevelUiParts
    {
        public RectTransform Root;
        public LevelHudView Hud;
        public SideMenuView SideMenu;
        public PausePopupView PausePopup;
        public RecordsPopupView RecordsPopup;
        public SettingsPopupView SettingsPopup;
        public VictoryEffectView VictoryEffect;
        public ToastView Toast;
    }
}
