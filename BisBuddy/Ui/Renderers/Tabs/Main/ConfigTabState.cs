using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Ui.Renderers.Tabs.Main
{
    public struct ConfigTabState : TabState
    {
        // if the config tab is being rendered in a separate config window
        public bool ExternalWindow;
    }
}
