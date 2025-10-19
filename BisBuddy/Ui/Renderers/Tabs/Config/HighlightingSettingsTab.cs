using BisBuddy.Resources;
using BisBuddy.Services.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Numerics;
using static Dalamud.Interface.Windowing.Window;

namespace BisBuddy.Ui.Renderers.Tabs.Config
{
    public class HighlightingSettingsTab(IConfigurationService configurationService) : TabRenderer<ConfigWindowTab>
    {
        private readonly IConfigurationService configurationService = configurationService;

        public WindowSizeConstraints? TabSizeConstraints => null;

        public bool ShouldDraw => true;
        public void Draw()
        {
            // NEED GREED
            var highlightNeedGreed = configurationService.HighlightNeedGreed;
            if (ImGui.Checkbox(Resource.HighlightNeedGreedCheckbox, ref highlightNeedGreed))
                configurationService.HighlightNeedGreed = highlightNeedGreed;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Resource.HighlightNeedGreedHelp);

            // SHOPS
            var highlightShops = configurationService.HighlightShops;
            if (ImGui.Checkbox(Resource.HighlightShopExchangesCheckbox, ref highlightShops))
                configurationService.HighlightShops = highlightShops;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Resource.HighlightShopExchangesHelp);

            // MATERIA MELDING
            // toggle highlighting
            var highlightMateriaMeld = configurationService.HighlightMateriaMeld;
            if (ImGui.Checkbox(Resource.HighlightMateriaMeldingCheckbox, ref highlightMateriaMeld))
                configurationService.HighlightMateriaMeld = highlightMateriaMeld;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Resource.HighlightMateriaMeldingHelp);

            // next materia vs all materia
            using (ImRaii.Disabled(!highlightMateriaMeld))
            {
                // draw a L shape for parent-child relationship
                var drawList = ImGui.GetWindowDrawList();
                var curLoc = ImGui.GetCursorScreenPos();
                var col = ImGui.GetColorU32(new Vector4(1, 1, 1, 1));
                var halfButtonHeight = ImGui.CalcTextSize("HI").Y / 2 + ImGui.GetStyle().FramePadding.Y;
                drawList.AddLine(curLoc + new Vector2(10, 0), curLoc + new Vector2(10, halfButtonHeight * 3 + 5), col, 2);
                drawList.AddLine(curLoc + new Vector2(10, halfButtonHeight), curLoc + new Vector2(20, halfButtonHeight), col, 2);

                using (ImRaii.PushIndent(25.0f, scaled: false))
                {
                    var highlightNextMateria = configurationService.HighlightNextMateria;
                    if (ImGui.Checkbox(Resource.HighlightNextMateriaCheckbox, ref highlightNextMateria))
                        configurationService.HighlightNextMateria = highlightNextMateria;
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(Resource.HighlightNextMateriaHelp);
                }

                drawList = ImGui.GetWindowDrawList();
                curLoc = ImGui.GetCursorScreenPos();
                drawList.AddLine(curLoc + new Vector2(10, halfButtonHeight), curLoc + new Vector2(20, halfButtonHeight), col, 2);

                using (ImRaii.PushIndent(25.0f, scaled: false))
                {
                    var highlightPrerequisiteMateria = configurationService.HighlightPrerequisiteMateria;
                    if (ImGui.Checkbox(Resource.HighlightPrerequisiteMateriaCheckbox, ref highlightPrerequisiteMateria))
                        configurationService.HighlightPrerequisiteMateria = highlightPrerequisiteMateria;
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(Resource.HighlightPrerequisiteMateriaHelp);
                }
            }

            // INVENTORIES
            var highlightInventories = configurationService.HighlightInventories;
            if (ImGui.Checkbox(Resource.HighlightInventoriesCheckbox, ref highlightInventories))
                configurationService.HighlightInventories = highlightInventories;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Resource.HighlightInventoriesHelp);

            using (ImRaii.Disabled(!highlightInventories))
            {
                var drawList = ImGui.GetWindowDrawList();
                var curLoc = ImGui.GetCursorScreenPos();
                var col = ImGui.GetColorU32(Vector4.One);
                var halfButtonHeight = ImGui.GetTextLineHeight() / 2 + ImGui.GetStyle().FramePadding.Y;
                drawList.AddLine(curLoc + new Vector2(10, 0), curLoc + new Vector2(10, halfButtonHeight), col, 2);
                drawList.AddLine(curLoc + new Vector2(10, halfButtonHeight), curLoc + new Vector2(20, halfButtonHeight), col, 2);
                using (ImRaii.PushIndent(25.0f, scaled: false))
                {
                    var highlightCollectedInInventory = configurationService.HighlightCollectedInInventory;
                    if (ImGui.Checkbox(Resource.HighlightCollectedInInventoryCheckbox, ref highlightCollectedInInventory))
                        configurationService.HighlightCollectedInInventory = highlightCollectedInInventory;
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(Resource.HighlightCollectedInInventoryHelp);
                }
            }

            // MARKETBOARD
            var highlightMarketboard = configurationService.HighlightMarketboard;
            if (ImGui.Checkbox(Resource.HighlightMarketboardCheckbox, ref highlightMarketboard))
                configurationService.HighlightMarketboard = highlightMarketboard;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Resource.HighlightMarketboardHelp);

            // ITEM TOOLTIPS
            var annotateTooltips = configurationService.AnnotateTooltips;
            if (ImGui.Checkbox(Resource.HighlightItemTooltipsCheckbox, ref annotateTooltips))
                configurationService.AnnotateTooltips = annotateTooltips;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Resource.HighlightItemTooltipsHelp);
        }

        public void PreDraw() { }

        public void SetTabState(TabState state)
        {
            throw new NotImplementedException();
        }
    }
}
