using Autofac.Features.Indexed;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Ui.Renderers.Components;
using BisBuddy.Ui.Renderers.Tabs.Config;
using BisBuddy.Ui.Renderers.Tabs.Debug;
using BisBuddy.Ui.Windows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using static Dalamud.Interface.Windowing.Window;

namespace BisBuddy.Ui.Renderers.Tabs.Main
{
    public class DebugTab(
        ITypedLogger<DebugTab> logger,
        IConfigurationService configurationService,
        IIndex<DebugToolTab, TabRenderer<DebugToolTab>> tabRendererIndex,
        IAttributeService attributeService
        ) : TabRenderer<MainWindowTab>
    {
        private readonly ITypedLogger<DebugTab> logger = logger;
        private readonly IAttributeService attributeService = attributeService;

        public WindowSizeConstraints? TabSizeConstraints => null;

        public bool ShouldDraw => configurationService.EnableDebugging;

        private List<DebugToolTab> debugTabsToDraw = Enum
            .GetValues<DebugToolTab>()
            .ToList();

        private bool firstDraw = true;

        private DebugToolTab selectedDebugTab = DebugToolTab.ItemRequirements;

        public void PreDraw() {
            if (firstDraw)
            {
                firstDraw = false;

                debugTabsToDraw = debugTabsToDraw
                    .Where(tab => tabRendererIndex.TryGetValue(tab, out _))
                    .ToList();

                selectedDebugTab = debugTabsToDraw.FirstOrDefault();
            }

            foreach (var tab in debugTabsToDraw)
                if (tabRendererIndex.TryGetValue(tab, out var tabRenderer))
                    tabRenderer.PreDraw();
        }

        public void Draw()
        {
            var tableSize = ImGui.GetContentRegionAvail();
            var flags = ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerV;

            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, Vector2.Zero);
            using var table = ImRaii.Table("debug_menu_table", 2, flags, tableSize);
            ImGui.PopStyleVar();

            if (!table)
                return;

            ImGui.TableSetupColumn("###debug_section_navigation", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("###debug_section_details", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var lineHeight = ImGui.GetTextLineHeightWithSpacing() * 1.3f;
            var selectableSize = new Vector2(0, lineHeight);
            var itemSpacing = ImGui.GetStyle().ItemSpacing.X;
            var rightOffset = 5f * ImGuiHelpers.GlobalScale;
            var navSize = new Vector2(145, 0) * ImGuiHelpers.GlobalScale;
            navSize.X += 10;

            UiComponents.PushTableClipRect();
            try
            {
                using (ImRaii.PushStyle(ImGuiStyleVar.SelectableTextAlign, new Vector2(0, 0.5f)))
                using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(2, 5)))
                using (ImRaii.Child("submenu_options_nav", navSize, false, ImGuiWindowFlags.AlwaysUseWindowPadding))
                {
                    foreach (var tab in debugTabsToDraw)
                    {
                        var tabTitle = attributeService.GetEnumAttribute<DisplayAttribute>(tab)!.GetName()!;
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + rightOffset);
                        if (ImGui.Selectable(tabTitle, selectedDebugTab == tab, size: selectableSize, flags: ImGuiSelectableFlags.SpanAllColumns))
                            selectedDebugTab = tab;
                    }
                }
            }
            finally
            {
                ImGui.PopClipRect();
            }

            ImGui.TableNextColumn();

            var tabPos = ImGui.GetCursorPos();
            var tabContentMax = tabPos + ImGui.GetContentRegionAvail();
            var tabContentsSize = ImGui.GetContentRegionAvail();
            var botRight = ImGui.GetCursorScreenPos() + tabContentsSize;

            UiComponents.PushTableClipRect();
            try
            {
                using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(8)))
                using (ImRaii.Child("##submenus", tabContentsSize, false, ImGuiWindowFlags.AlwaysUseWindowPadding))
                {
                    if (!tabRendererIndex.TryGetValue(selectedDebugTab, out var tabRenderer))
                        throw new ArgumentException($"unknown config menu type: {selectedDebugTab}");

                    tabRenderer.Draw();
                }
            }
            finally
            {
                ImGui.PopClipRect();
            }
        }

        public void SetTabState(TabState state)
        {
            throw new NotImplementedException();
        }
    }
}
