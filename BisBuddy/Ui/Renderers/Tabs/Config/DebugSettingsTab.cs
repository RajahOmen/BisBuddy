using BisBuddy.Services.Configuration;
using BisBuddy.Ui.Renderers.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System;
using static Dalamud.Interface.Windowing.Window;

namespace BisBuddy.Ui.Renderers.Tabs.Config
{
    public class DebugSettingsTab(
        IConfigurationService configurationService,
        UiComponents uiComponents
        ) : TabRenderer<ConfigWindowTab>
    {
        private readonly IConfigurationService configurationService = configurationService;
        private readonly UiComponents uiComponents = uiComponents;

        public WindowSizeConstraints? TabSizeConstraints => null;

        public bool ShouldDraw => true;

        public void Draw()
        {
            var debuggingEnabled = configurationService.EnableDebugging;
            if (ImGui.Checkbox("Enable Debugging", ref debuggingEnabled))
                configurationService.EnableDebugging = debuggingEnabled;

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            using var _ = ImRaii.Disabled(!debuggingEnabled);

            ImGui.Text("Framework Thread Checks");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(
                    "Diagnosis tool for illegal game data access/manipulation off the main thread"
                );

            if (uiComponents.DrawCachedEnumComboDropdown(
                configurationService.DebugFrameworkThreadBehavior,
                out var newEnumValue))
                configurationService.DebugFrameworkThreadBehavior = newEnumValue;
        }

        public void PreDraw() { }

        public void SetTabState(TabState state)
        {
            throw new NotImplementedException();
        }
    }
}
