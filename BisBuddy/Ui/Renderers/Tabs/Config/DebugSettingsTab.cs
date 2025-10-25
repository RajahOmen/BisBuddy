using BisBuddy.Services.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
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
            var debuggingEnabled = configurationService.EnableDebugging;
            if (ImGui.Checkbox("Enable Debugging", ref debuggingEnabled))
                configurationService.EnableDebugging = debuggingEnabled;

            using var _ = ImRaii.Disabled(!debuggingEnabled);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var frameworkAsserts = configurationService.DebugFrameworkAsserts;
            if (ImGui.Checkbox("Enable Framework Asserts", ref frameworkAsserts))
                configurationService.DebugFrameworkAsserts = frameworkAsserts;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Sends errors to log if internal logic is corrupted. May cause features to break");
        }

        public void PreDraw() { }

        public void SetTabState(TabState state)
        {
            throw new NotImplementedException();
        }
    }
}
