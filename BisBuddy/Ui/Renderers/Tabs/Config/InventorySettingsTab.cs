using BisBuddy.Resources;
using BisBuddy.Services.Configuration;
using BisBuddy.Util;
using Dalamud.Bindings.ImGui;
using System;
using static Dalamud.Interface.Windowing.Window;

namespace BisBuddy.Ui.Renderers.Tabs.Config
{
    public class InventorySettingsTab(IConfigurationService configurationService) : TabRenderer<ConfigWindowTab>
    {
        private readonly IConfigurationService configurationService = configurationService;

        public WindowSizeConstraints? TabSizeConstraints => null;

        public bool ShouldDraw => true;
        public void Draw()
        {
            // ITEM COLLECTION
            var enableAutoComplete = configurationService.AutoCompleteItems;
            if (ImGui.Checkbox(Resource.UpdateOnItemChangeCheckbox, ref enableAutoComplete))
                configurationService.AutoCompleteItems = enableAutoComplete;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Resource.UpdateOnItemChangeHelp);

            // INVENTORY SCAN ON LOGIN/LOAD
            var enableAutoScan = configurationService.AutoScanInventory;
            if (ImGui.Checkbox(Resource.UpdateOnLoginLoadCheckbox, ref enableAutoScan))
                configurationService.AutoScanInventory = enableAutoScan;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(string.Format(Resource.UpdateOnLoginLoadHelp, Constants.PluginName));

            // INVENTORY SCAN ON PLUGIN UPDATES
            var enablePluginUpdateScan = configurationService.PluginUpdateInventoryScan;
            if (ImGui.Checkbox(Resource.UpdateOnPluginChangesCheckbox, ref enablePluginUpdateScan))
                configurationService.PluginUpdateInventoryScan = enablePluginUpdateScan;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(string.Format(Resource.UpdateOnPluginChangesHelp, Constants.PluginName));
        }

        public void PreDraw() { }

        public void SetTabState(TabState state)
        {
            throw new NotImplementedException();
        }
    }
}
