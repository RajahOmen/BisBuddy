using BisBuddy.Services.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System;
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
            ImGui.NewLine();
            ImGuiHelpers.CenteredText("Debugging tools coming soon...");
        }

        public void SetTabState(TabState state)
        {
            throw new NotImplementedException();
        }
    }
}
