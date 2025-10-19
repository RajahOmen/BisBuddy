using BisBuddy.Resources;
using BisBuddy.Services.Configuration;
using Dalamud.Bindings.ImGui;
using System;
using static Dalamud.Interface.Windowing.Window;

namespace BisBuddy.Ui.Renderers.Tabs.Config
{
    public class UiThemeSettingsTab(IConfigurationService configurationService) : TabRenderer<ConfigWindowTab>
    {
        private readonly IConfigurationService configurationService = configurationService;

        public WindowSizeConstraints? TabSizeConstraints => null;

        public bool ShouldDraw => true;

        public void Draw()
        {
            if (ImGui.Button(Resource.ResetUiThemeButton))
                configurationService.ResetUiTheme();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Resource.ResetUiThemeHelp);
        }

        public void PreDraw() { }

        public void SetTabState(TabState state)
        {
            throw new NotImplementedException();
        }
    }
}
