using BisBuddy.Extensions;
using BisBuddy.Items;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.Linq;
using static Dalamud.Interface.Windowing.Window;
using ItemRelation = (
    uint TargetItemId,
    System.Collections.Generic.List<System.Collections.Generic.List<(
        uint SourceItemId,
        int SourceCount
    )>> Sources
);


namespace BisBuddy.Ui.Renderers.Tabs.Debug
{
    public class DebugPrerequisitesTab(IItemDataService itemDataService) : TabRenderer<DebugToolTab>
    {
        private readonly IItemDataService itemDataService = itemDataService;


        public WindowSizeConstraints? TabSizeConstraints => null;

        private bool firstDraw = true;
        private bool groupByTarget = true;
        private string targetNameFilter = string.Empty;
        private string sourceNameFilter = string.Empty;

        private List<ItemRelation> itemsCoffers = [];
        private List<ItemRelation> itemsPrerequisites = [];

        private List<ItemRelation> itemsCoffersGrouped = [];
        private List<ItemRelation> itemsPrerequisitesGrouped = [];

        public bool ShouldDraw => true;

        public void SetTabState(TabState state)
        {
            throw new NotImplementedException();
        }

        private void updateItemRelations()
        {
            var newCoffers = itemDataService
                .ItemsCoffers
                .Where(entry => itemDataService.GetItemNameById(entry.Key).Contains(targetNameFilter))
                .SelectMany(entry =>
                    entry
                        .Where(val => itemDataService.GetItemNameById(val).Contains(sourceNameFilter))
                        .Select<uint, ItemRelation>(val => (entry.Key, [[(val, 1)]]))
                ).OrderByDescending(entry => entry.TargetItemId);

            var newPrerequisites = itemDataService
                .ItemsPrerequisites
                .Where(entry => itemDataService.GetItemNameById(entry.Key).Contains(targetNameFilter))
                .SelectMany(entry =>
                    entry
                        .Where(vals => vals.Any(val => itemDataService.GetItemNameById(val).Contains(sourceNameFilter)))
                        .Select<List<uint>, ItemRelation>(vals => (
                            entry.Key,
                            [vals.GroupBy(val => val).Select(g => (g.Key, g.Count())).ToList()]
                        ))
                ).OrderByDescending(entry => entry.TargetItemId);

            var newCoffersGrouped = newCoffers
                .GroupBy(relation => relation.TargetItemId)
                .Select(g => (g.Key, g.SelectMany(entry => entry.Sources).ToList()));

            var newPrerequisitesGrouped = newPrerequisites
                .GroupBy(relation => relation.TargetItemId)
                .Select(g => (g.Key, g.SelectMany(entry => entry.Sources).ToList()));

            itemsCoffers = newCoffers.ToList();
            itemsPrerequisites = newPrerequisites.ToList();
            itemsCoffersGrouped = newCoffersGrouped.ToList();
            itemsPrerequisitesGrouped = newPrerequisitesGrouped.ToList();
        }

        private (List<ItemRelation>, List<ItemRelation>) getItemRelations()
        {
            if (groupByTarget)
            {
                return (itemsCoffersGrouped, itemsPrerequisitesGrouped);
            } else
            {
                return (itemsCoffers, itemsPrerequisites);
            }
        }

        public void PreDraw() {
            if (!firstDraw)
                return;

            firstDraw = false;

            updateItemRelations();
        }

        public void Draw()
        {


            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, "Refresh"))
                updateItemRelations();

            ImGui.SameLine();
            ImGui.Checkbox("Group by Target Item", ref groupByTarget);

            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();

            ImGui.SetNextItemWidth(150f);
            ImGui.InputText("###filter_target_input_text", ref targetNameFilter, maxLength: 200);

            ImGui.SameLine();

            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Filter, "Filter Target"))
                updateItemRelations();

            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();

            ImGui.SetNextItemWidth(150f);
            ImGui.InputText("###filter_source_input_text", ref sourceNameFilter, maxLength: 200);

            ImGui.SameLine();

            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Filter, "Filter Source"))
                updateItemRelations();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            using var tabMenu = ImRaii.TabBar("###relations_tab");
            if (!tabMenu)
                return;

            var (coffers, prerequisites) = getItemRelations();

            if (groupByTarget)
            {
                itemsCoffersGrouped = drawItemRelationsTab("Coffers", itemsCoffersGrouped);
                itemsPrerequisitesGrouped = drawItemRelationsTab("Prerequisites", itemsPrerequisitesGrouped);
            }
            else
            {
                itemsCoffers = drawItemRelationsTab("Coffers", itemsCoffers);
                itemsPrerequisites = drawItemRelationsTab("Prerequisites", itemsPrerequisites);
            }
        }

        private List<ItemRelation> drawItemRelationsTab(string relationsName, List<ItemRelation> relations)
        {
            using var id = ImRaii.PushId(relationsName);

            using var tabItem = ImRaii.TabItem($"{relationsName} [x{relations.Count}]###{relationsName}");
            if (!tabItem)
                return relations;

            ImGui.Spacing();

            var tableFlags = (
                ImGuiTableFlags.RowBg
                | ImGuiTableFlags.SizingStretchSame
                | ImGuiTableFlags.ScrollY
                | ImGuiTableFlags.Sortable
                | ImGuiTableFlags.SortMulti
                );
            using var table = ImRaii.Table("##item_relations_table", 3, tableFlags);
            if (!table)
                return relations;

            ImGui.TableSetupColumn("Item Id", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending, 60f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Target Item Name", ImGuiTableColumnFlags.WidthFixed, 400f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Sources", ImGuiTableColumnFlags.NoSort);

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            var sortSpecs = ImGui.TableGetSortSpecs();
            if (sortSpecs.SpecsDirty)
            {
                sortSpecs.SpecsDirty = false;
                var specs = sortSpecs.Specs;
                if (specs.ColumnIndex == 0)
                {
                    relations = relations
                        .OrderByDirection(
                            r => r.TargetItemId,
                            specs.SortDirection == ImGuiSortDirection.Descending
                        ).ToList();
                } else if (specs.ColumnIndex == 1)
                {
                    relations = relations
                        .OrderByDirection(
                            r => itemDataService.GetItemNameById(r.TargetItemId),
                            specs.SortDirection == ImGuiSortDirection.Descending
                        ).ToList();
                }
            }

            var clipper = ImGui.ImGuiListClipper();
            clipper.Begin(relations.Count, ImGui.GetTextLineHeightWithSpacing());

            while (clipper.Step())
            {
                for (var rowIdx = clipper.DisplayStart; rowIdx < clipper.DisplayEnd; rowIdx++)
                {
                    var (targetItemId, sources) = relations[rowIdx];
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    ImGui.Text($"{targetItemId}");
                    ImGui.TableNextColumn();

                    ImGui.Text(itemDataService.GetItemNameById(targetItemId));

                    ImGui.TableNextColumn();

                    foreach (var source in sources)
                    {
                        var itemIdsText = string.Join(
                            ", ",
                            source.Select(g => g.SourceItemId)
                            );

                        var text = string.Join(
                            ", ",
                            source.Select(
                                itemGroup => {
                                    var countText = itemGroup.SourceCount > 1 ? $" x{itemGroup.SourceCount}" : "";
                                    return $"{itemDataService.GetItemNameById(itemGroup.SourceItemId)}{countText}";
                                }
                                )
                            );
                        ImGui.Text(text);
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip(itemIdsText);
                    }

                }
            }
            
            return relations;
        }
    }
}
