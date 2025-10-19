using BisBuddy.Services.Configuration;
using Dalamud.Bindings.ImGui;
using System;
using static Dalamud.Interface.Windowing.Window;

namespace BisBuddy.Ui.Renderers.Tabs.Config
{
    public class DebugSettingsTab(IConfigurationService configurationService) : TabRenderer<ConfigWindowTab>
    {
        private readonly IConfigurationService configurationService = configurationService;

        public WindowSizeConstraints? TabSizeConstraints => null;

        public bool ShouldDraw => true;

        public void Draw()
        {
            ImGui.Text("DEBUG");

            var debuggingEnabled = configurationService.EnableDebugging;
            if (ImGui.Checkbox("Enable Debugging", ref debuggingEnabled))
                configurationService.EnableDebugging = debuggingEnabled;
        }

        public void PreDraw() { }

        public void SetTabState(TabState state)
        {
            throw new NotImplementedException();
        }
    }
}
