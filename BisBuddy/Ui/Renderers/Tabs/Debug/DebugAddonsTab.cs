using BisBuddy.Extensions;
using BisBuddy.Gear;
using BisBuddy.Services;
using BisBuddy.Services.Addon;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static Dalamud.Interface.Windowing.Window;
using AddonTableColumns = System.Collections.Generic.List<(
    string Name,
    System.Action<string> Init,
    System.Action<(
        BisBuddy.Services.Addon.IAddonEventListener Listener,
        BisBuddy.Services.Addon.NodeHighlightType Type,
        BisBuddy.Gear.HighlightColor Color,
        uint NodeId,
        int Count,
        bool Disposed
    )> Draw,
    System.Action<bool> Sort
    )>;
using NodeHighlightGroup = (
    BisBuddy.Services.Addon.IAddonEventListener Listener,
    BisBuddy.Services.Addon.NodeHighlightType Type,
    BisBuddy.Gear.HighlightColor Color,
    uint NodeId,
    int Count,
    bool Disposed
);

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
        private bool storeDisposed = true;

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
                    (name) => ImGui.TableSetupColumn(name, ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending, 2),
                    (nodeGroup) => ImGui.Text(nodeGroup.Listener.AddonName),
                    (desc) => addonNodeHighlights = addonNodeHighlights.OrderByDirection(entry => entry.Listener.AddonName, desc).ToList()
                ),
                (
                    "Addon Active",
                    (name) => ImGui.TableSetupColumn(name),
                    (nodeGroup) => {
                        var color = new Vector4(1f, 0, 0, 0.5f);
                        var addonNull = nodeGroup.Listener.IsAddonNull;
                        var isBadEntry = addonNull && !nodeGroup.Disposed;
                        var cursorPos = ImGui.GetCursorPos();
                        if (isBadEntry)
                            using (ImRaii.PushColor(ImGuiCol.Header, color, isBadEntry))
                            using (ImRaii.PushColor(ImGuiCol.HeaderActive, color, isBadEntry))
                            using (ImRaii.PushColor(ImGuiCol.HeaderHovered, color, isBadEntry))
                                ImGui.Selectable("", selected: true, flags: ImGuiSelectableFlags.SpanAllColumns);

                        ImGui.SetCursorPos(cursorPos);
                        ImGui.Text(addonNull ? "No" : "Yes");
                    },
                    (desc) => addonNodeHighlights = addonNodeHighlights.OrderByDirection(entry => !entry.Listener.IsAddonNull, desc).ToList()
                ),
                (
                    "Addon Visible",
                    (name) => ImGui.TableSetupColumn(name),
                    (nodeGroup) => {
                        var color = new Vector4(0.8f, 0.8f, 0, 0.2f);
                        var isBadEntry = !nodeGroup.Listener.IsAddonVisible && !nodeGroup.Disposed;
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
                    "Listener On",
                    (name) => ImGui.TableSetupColumn(name),
                    (nodeGroup) => {
                        var color = new Vector4(0.8f, 0, 0, 0.2f);
                        var isBadEntry = !nodeGroup.Listener.IsEnabled && !nodeGroup.Disposed;
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
                    "Node Active",
                    (name) => ImGui.TableSetupColumn(name),
                    (nodeGroup) => ImGui.Text(nodeGroup.Disposed ? "No" : "Yes"),
                    (desc) => addonNodeHighlights = addonNodeHighlights.OrderByDirection(entry => entry.Disposed, desc).ToList()
                ),
                (
                    "Node Count",
                    (name) => ImGui.TableSetupColumn(name),
                    (nodeGroup) => ImGui.Text($"{nodeGroup.Count}"),
                    (desc) => addonNodeHighlights = addonNodeHighlights.OrderByDirection(entry => entry.Count, desc).ToList()
                ),
                (
                    "Node ID",
                    (name) => ImGui.TableSetupColumn(name),
                    (nodeGroup) => ImGui.Text($"{nodeGroup.NodeId}"),
                    (desc) => addonNodeHighlights = addonNodeHighlights.OrderByDirection(entry => entry.NodeId, desc).ToList()
                ),
                (
                    "Node Type",
                    (name) => ImGui.TableSetupColumn(name),
                    (nodeGroup) => ImGui.Text(Enum.GetName(nodeGroup.Type)),
                    (desc) => addonNodeHighlights = addonNodeHighlights.OrderByDirection(entry => entry.Type, desc).ToList()
                ),
                (
                    "Color",
                    (name) => ImGui.TableSetupColumn(name, initWidthOrWeight: 2),
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

            if (storeDisposed)
            {
                // make these count as disposed unless otherwise cleared
                for (var idx = 0; idx < addonNodeHighlights.Count; idx++)
                {
                    var nodeGroup = addonNodeHighlights[idx];
                    nodeGroup.Disposed = true;
                    addonNodeHighlights[idx] = nodeGroup;
                }
            } else
            {
                addonNodeHighlights.Clear();
            }


            if (groupByListener)
                addonNodeHighlights.AddRange(addonListeners
                    .Where(listener => listener.NodeHighlights.Count > 0)
                    .SelectMany(listener => listener
                        .NodeHighlights
                        .GroupBy(node => (node.Type, node.Color.BaseColor))
                        .Select(g => (listener, g.First().Type, g.First().Color, 0u, g.Count(), false))
                ));
            else
                addonNodeHighlights.AddRange(addonListeners
                    .Where(listener => listener.NodeHighlights.Count > 0)
                    .SelectMany(listener => listener
                        .NodeHighlights
                        .Select(node => (listener, node.Type, node.Color, node.NodeId, 1, false))
                ));


            addonNodeHighlights = addonNodeHighlights
                .GroupBy(group => (group.Listener, group.Type, group.NodeId, group.Color, group.Count))
                .Select(g => g.OrderBy(g => g.Disposed).First())
                .ToList();

            columns[sortColumnIdx].Sort(sortDesc);
        }

        public void PreDraw()
        {
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

            if (ImGui.Checkbox("List Disposed Nodes", ref storeDisposed))
            {
                if (storeDisposed)
                    groupByListener = false;
                updateNodeHighlights();
            }

            ImGui.SameLine();

            using (ImRaii.Disabled(!storeDisposed))
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Trash, "Clear Current Disposed"))
                    addonNodeHighlights = addonNodeHighlights.Where(group => !group.Disposed).ToList();

            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();

            if (ImGui.Checkbox("Group Nodes", ref groupByListener))
            {
                if (groupByListener)
                    storeDisposed = false;

                addonNodeHighlights.Clear();
                updateNodeHighlights();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (addonNodeHighlights.Count == 0)
            {
                ImGui.NewLine();
                ImGuiHelpers.CenteredText("No Highlighted Nodes");
                return;
            }

            string badColumn;
            if (groupByListener)
                badColumn = "Node ID";
            else
                badColumn = "Node Count";

            var columnsToDraw = columns
                .Where(c => c.Name != badColumn)
                .ToList();

            var tableFlags = (
                ImGuiTableFlags.RowBg
                | ImGuiTableFlags.SizingStretchProp
                | ImGuiTableFlags.Sortable
                | ImGuiTableFlags.SortMulti
                | ImGuiTableFlags.ScrollY
                | ImGuiTableFlags.PadOuterX
                | ImGuiTableFlags.BordersInnerV
                );
            using var table = ImRaii.Table("###item_requirements_table", columnsToDraw.Count, tableFlags);
            if (!table)
                return;

            foreach (var col in columnsToDraw)
                col.Init(col.Name);

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            var sortSpecs = ImGui.TableGetSortSpecs();
            if (sortSpecs.SpecsDirty)
            {
                sortSpecs.SpecsDirty = false;
                sortColumnIdx = sortSpecs.Specs.ColumnIndex;
                sortDesc = sortSpecs.Specs.SortDirection == ImGuiSortDirection.Descending;
                columnsToDraw[sortColumnIdx].Sort(sortDesc);
            }

            var clipper = ImGui.ImGuiListClipper();
            clipper.Begin(addonNodeHighlights.Count);

            while (clipper.Step())
            {
                for (var rowIdx = clipper.DisplayStart; rowIdx < clipper.DisplayEnd; rowIdx++)
                {
                    var nodeGroup = addonNodeHighlights[rowIdx];
                    using var id = ImRaii.PushId(rowIdx);
                    using var style = ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.5f, nodeGroup.Disposed);
                    ImGui.TableNextRow();
                    foreach (var col in columnsToDraw)
                    {
                        ImGui.TableNextColumn();
                        col.Draw(nodeGroup);
                    }
                }
            }
        }
    }
}
