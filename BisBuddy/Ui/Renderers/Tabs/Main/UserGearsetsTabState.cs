using BisBuddy.Gear;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
