using BisBuddy.Services.Configuration;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Dalamud.Interface.Windowing.Window;

namespace BisBuddy.Ui.Renderers.Tabs.Main
{
    public class DebugTab(IConfigurationService configurationService) : TabRenderer<MainWindowTab>
    {
        public WindowSizeConstraints? TabSizeConstraints => null;

        public bool ShouldDraw => configurationService.EnableDebugging;

        public void PreDraw() { }

        public void Draw()
        {
            ImGui.Text("Debugging coming soon...");
        }

        public void SetTabState(TabState state)
        {
            throw new NotImplementedException();
        }
    }
}
