using BisBuddy.Services;
using BisBuddy.Services.Addon;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.Linq;
using static Dalamud.Interface.Windowing.Window;
using BisBuddy.Extensions;
using System.Numerics;

using AddonTableColumns = System.Collections.Generic.List<(
    string Name,
    System.Action<string> Init,
    System.Action<(
        BisBuddy.Services.Addon.IAddonEventListener Listener,
        BisBuddy.Services.Addon.NodeHighlightType Type,
        BisBuddy.Gear.HighlightColor Color,
        uint NodeId,
        int Count
    )> Draw,
    System.Action<bool> Sort
    )>;

using NodeHighlightGroup = (
    BisBuddy.Services.Addon.IAddonEventListener Listener,
    BisBuddy.Services.Addon.NodeHighlightType Type,
    BisBuddy.Gear.HighlightColor Color,
    uint NodeId,
    int Count
);
using Dalamud.Interface.Utility;

namespace BisBuddy.Ui.Renderers.Tabs.Debug
{
    public class DebugAddonsTab : TabRenderer<DebugToolTab>
    {
        private readonly IEnumerable<IAddonEventListener> addonListeners;
        private readonly IDebugService debugService;

        private bool firstDraw = true;
        private bool groupByListener = false;
        private bool autoRefresh = true;
        private int sortColumnIdx = 0;
        private bool sortDesc = true;

        private readonly AddonTableColumns columns;

        private List<NodeHighlightGroup> addonNodeHighlights;


        public DebugAddonsTab(
            IEnumerable<IAddonEventListener> addonListeners,
            IDebugService debugService
        )
        {
            this.addonListeners = addonListeners;
            this.debugService = debugService;

            this.addonNodeHighlights = [];
            this.columns = [
                (
                    "Addon Name",
                    (name) => ImGui.TableSetupColumn(name, ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending | ImGuiTableColumnFlags.WidthFixed, 200f),
                    (nodeGroup) => ImGui.Text(nodeGroup.Listener.AddonName),
                    (desc) => addonNodeHighlights = addonNodeHighlights.OrderByDirection(entry => entry.Listener.AddonName, desc).ToList()
                ),
                (
                    "Visible",
                    (name) => ImGui.TableSetupColumn(name, ImGuiTableColumnFlags.WidthFixed, 60f),
                    (nodeGroup) => {
                        var color = new Vector4(0.8f, 0.8f, 0, 0.2f);
                        var isBadEntry = !nodeGroup.Listener.IsAddonVisible;
                        var cursorPos = ImGui.GetCursorPos();
                        if (isBadEntry)
                            using (ImRaii.PushColor(ImGuiCol.Header, color, isBadEntry))
                            using (ImRaii.PushColor(ImGuiCol.HeaderActive, color, isBadEntry))
                            using (ImRaii.PushColor(ImGuiCol.HeaderHovered, color, isBadEntry))
                                ImGui.Selectable("", selected: true, flags: ImGuiSelectableFlags.SpanAllColumns);

                        ImGui.SetCursorPos(cursorPos);
                        ImGui.Text(nodeGroup.Listener.IsAddonVisible ? "Yes" : "No");
                    },
                    (desc) => addonNodeHighlights = addonNodeHighlights.OrderByDirection(entry => entry.Listener.IsAddonVisible, desc).ToList()
                ),
                (
                    "Enabled",
                    (name) => ImGui.TableSetupColumn(name, ImGuiTableColumnFlags.WidthFixed, 60f),
                    (nodeGroup) => {
                        var color = new Vector4(0.8f, 0, 0, 0.2f);
                        var isBadEntry = !nodeGroup.Listener.IsEnabled;
                        var cursorPos = ImGui.GetCursorPos();
                        if (isBadEntry)
                            using (ImRaii.PushColor(ImGuiCol.Header, color, isBadEntry))
                            using (ImRaii.PushColor(ImGuiCol.HeaderActive, color, isBadEntry))
                            using (ImRaii.PushColor(ImGuiCol.HeaderHovered, color, isBadEntry))
                                ImGui.Selectable("", selected: true, flags: ImGuiSelectableFlags.SpanAllColumns);

                        ImGui.SetCursorPos(cursorPos);
                        ImGui.Text(nodeGroup.Listener.IsEnabled ? "Yes" : "No");
                    },
                    (desc) => addonNodeHighlights = addonNodeHighlights.OrderByDirection(entry => entry.Listener.IsEnabled, desc).ToList()
                ),
                (
                    "#",
                    (name) => ImGui.TableSetupColumn(name, ImGuiTableColumnFlags.WidthFixed, 30f),
                    (nodeGroup) => ImGui.Text($"{nodeGroup.Count}"),
                    (desc) => addonNodeHighlights = addonNodeHighlights.OrderByDirection(entry => entry.Count, desc).ToList()
                ),
                (
                    "Node Type",
                    (name) => ImGui.TableSetupColumn(name),
                    (nodeGroup) => ImGui.Text(Enum.GetName(nodeGroup.Type)),
                    (desc) => addonNodeHighlights = addonNodeHighlights.OrderByDirection(entry => entry.Type, desc).ToList()
                ),
                (
                    "Color",
                    (name) => ImGui.TableSetupColumn(name),
                    (nodeGroup) => {
                        var color = nodeGroup.Color.BaseColor;
                        ImGui.TextColored(color with { W = 1.0f }, $"{color}");
                    },
                    (desc) => addonNodeHighlights = addonNodeHighlights.OrderByDirection(entry => $"{entry.Color.BaseColor}", desc).ToList()
                ),
            ];
        }

        public WindowSizeConstraints? TabSizeConstraints => null;

        public bool ShouldDraw => true;

        public void SetTabState(TabState state)
        {
            throw new NotImplementedException();
        }

        private void updateNodeHighlights()
        {
            debugService.AssertMainThreadDebug();

            if (groupByListener)
                addonNodeHighlights = addonListeners
                    .Where(listener => listener.NodeHighlights.Count > 0)
                    .SelectMany(listener => listener
                        .NodeHighlights
                        .GroupBy(node => (node.Type, node.Color.BaseColor))
                        .Select(g => (listener, g.First().Type, g.First().Color, 0u, g.Count())) 
                ).ToList();
            else
                addonNodeHighlights = addonListeners
                    .Where(listener => listener.NodeHighlights.Count > 0)
                    .SelectMany(listener => listener
                        .NodeHighlights
                        .Select(node => (listener, node.Type, node.Color, node.NodeId, 1))
                ).ToList();

            columns[sortColumnIdx].Sort(sortDesc);
        }

        public void PreDraw() {
            if (!firstDraw && !autoRefresh)
                return;

            firstDraw = false;

            updateNodeHighlights();
        }

        public void Draw()
        {
            debugService.AssertMainThreadDebug();

            using (ImRaii.Disabled(autoRefresh))
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, "Refresh"))
                    updateNodeHighlights();

            ImGui.SameLine();

            if (ImGui.Checkbox("Auto Refresh", ref autoRefresh))
                updateNodeHighlights();

            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();

            if (ImGui.Checkbox("Group Nodes", ref groupByListener))
                updateNodeHighlights();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (addonNodeHighlights.Count == 0)
            {
                ImGui.NewLine();
                ImGuiHelpers.CenteredText("No Highlighted Nodes");
                return;
            }

            var tableFlags = (
                ImGuiTableFlags.RowBg
                | ImGuiTableFlags.SizingStretchSame
                | ImGuiTableFlags.Sortable
                | ImGuiTableFlags.SortMulti
                | ImGuiTableFlags.ScrollY
                | ImGuiTableFlags.PadOuterX
                | ImGuiTableFlags.BordersInnerV
                );
            using var table = ImRaii.Table("###item_requirements_table", columns.Count, tableFlags);
            if (!table)
                return;

            foreach (var col in columns)
                col.Init(col.Name);

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            var sortSpecs = ImGui.TableGetSortSpecs();
            if (sortSpecs.SpecsDirty)
            {
                sortSpecs.SpecsDirty = false;
                sortColumnIdx = sortSpecs.Specs.ColumnIndex;
                sortDesc = sortSpecs.Specs.SortDirection == ImGuiSortDirection.Descending;
                columns[sortColumnIdx].Sort(sortDesc);
            }

            var clipper = ImGui.ImGuiListClipper();
            clipper.Begin(addonNodeHighlights.Count);

            while (clipper.Step())
            {
                for (var rowIdx = clipper.DisplayStart; rowIdx < clipper.DisplayEnd; rowIdx++)
                {
                    using var _ = ImRaii.PushId(rowIdx);
                    var nodeGroup = addonNodeHighlights[rowIdx];
                    ImGui.TableNextRow();
                    foreach (var col in columns)
                    {
                        ImGui.TableNextColumn();
                        col.Draw(nodeGroup);
                    }
                }
            }
        }
    }
}
