using BisBuddy.Gear;

namespace BisBuddy.Ui.Renderers.Tabs.Main
{
    public struct UserGearsetsTabState : TabState
    {
        // which gearset is being displayed
        public Gearset? activeGearset;

        // which gearpiece is expanded
        public Gearpiece? activeGearpiece;
    }
}
