using BisBuddy.Extensions;
using BisBuddy.Gear;
using BisBuddy.Items;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
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
using ItemRequirementTableColumns = System.Collections.Generic.List<(
    string Name,
    System.Action<string> Init,
    System.Action<(BisBuddy.Gear.ItemRequirementOwned Req, int Count)> Draw,
    System.Action<bool> Sort
    )>;

namespace BisBuddy.Ui.Renderers.Tabs.Debug
{
    public class DebugItemRequirementsTab : TabRenderer<DebugToolTab>
    {
        private readonly ITypedLogger<DebugItemRequirementsTab> logger;
        private readonly IGearsetsService gearsetsService;
        private readonly IItemDataService itemDataService;
        private readonly IConfigurationService configurationService;
        public WindowSizeConstraints? TabSizeConstraints => null;

        private List<(ItemRequirementOwned Req, int Count)> itemRequirements;

        private bool firstDraw = true;

        private bool groupReqs = true;

        private string itemNameFilter = string.Empty;

        private readonly ItemRequirementTableColumns groupedColumns;
        private readonly ItemRequirementTableColumns ungroupedColumns;


        public DebugItemRequirementsTab(
            ITypedLogger<DebugItemRequirementsTab> logger,
            IGearsetsService gearsetsService,
            IItemDataService itemData,
            IConfigurationService configurationService
            )
        {
            this.logger = logger;
            this.gearsetsService = gearsetsService;
            this.itemDataService = itemData;
            this.configurationService = configurationService;
            this.itemRequirements = [];

            var quantityColumnName = "#";
            var gearpieceColumnName = "Gearpiece";

            this.groupedColumns = [
            (
                "Item Id",
                (name) => ImGui.TableSetupColumn(name, ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.WidthFixed, 60f),
                (g) => ImGui.Text($"{g.Req.ItemRequirement.ItemId % 1_000_000}"),
                (desc) => itemRequirements = itemRequirements.OrderByDirection(g => g.Req.ItemRequirement.ItemId, desc).ToList()
            ),
            (
                "Item Name",
                (name) => ImGui.TableSetupColumn(name),
                (g) => ImGui.Text(itemDataService.GetItemNameById(g.Req.ItemRequirement.ItemId)),
                (desc) => itemRequirements = itemRequirements.OrderByDirection(req => itemDataService.GetItemNameById(req.Req.ItemRequirement.ItemId), desc).ToList()
            ),
            (
                quantityColumnName,
                (name) => ImGui.TableSetupColumn(name, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.PreferSortDescending, 30f),
                (g) => ImGui.Text($"{g.Count}"),
                (desc) => itemRequirements = itemRequirements.OrderByDirection(g => g.Count, desc).ToList()
            ),
            (
                "Gearset",
                (name) => ImGui.TableSetupColumn(name, ImGuiTableColumnFlags.PreferSortDescending),
                (g) => ImGui.Text($"{g.Req.Gearset.Name}"),
                (desc) => itemRequirements = itemRequirements.OrderByDirection(g => g.Req.Gearset.Name, desc).ToList()
            ),
            (
                gearpieceColumnName,
                (name) => ImGui.TableSetupColumn(name, ImGuiTableColumnFlags.PreferSortDescending),
                (g) => ImGui.Text(g.Req.Gearpiece.ItemName),
                (desc) => itemRequirements = itemRequirements.OrderByDirection(g => g.Req.Gearpiece.ItemName, desc).ToList()
            ),
            (
                "Requirement Type",
                (name) => ImGui.TableSetupColumn(name),
                (g) => ImGui.Text($"{g.Req.ItemRequirement.RequirementType}"),
                (desc) => itemRequirements = itemRequirements.OrderByDirection(g => g.Req.ItemRequirement.RequirementType, desc).ToList()
            ),
            (
                "Collection Status",
                (name) => ImGui.TableSetupColumn(name),
                (g) => ImGui.TextColored(configurationService.UiTheme.GetCollectionStatusTheme(g.Req.ItemRequirement.CollectionStatus).TextColor, $"{g.Req.ItemRequirement.CollectionStatus}"),
                (desc) => itemRequirements = itemRequirements.OrderByDirection(g => g.Req.ItemRequirement.CollectionStatus, desc).ToList()
            ),
            (
                "Color",
                (name) => ImGui.TableSetupColumn(name, ImGuiTableColumnFlags.WidthFixed, 55f),
                (g) => {
                    var (colorText, color) = getColor(g.Req.Gearset);
                    ImGui.TextColored(color with { W = 1.0f }, colorText);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"Click to copy {color}");
                    if (ImGui.IsItemClicked())
                        ImGui.SetClipboardText($"{color}");
                },
                (desc) => itemRequirements = itemRequirements.OrderByDirection(g => $"{getColor(g.Req.Gearset).Color}", desc).ToList()
            )];

            this.ungroupedColumns = groupedColumns.ToList();

            this.ungroupedColumns = ungroupedColumns.Where(c => c.Name != quantityColumnName).ToList();
            this.groupedColumns = groupedColumns.Where(c => c.Name != gearpieceColumnName).ToList();
        }

        private (string Source, Vector4 Color) getColor(Gearset gearset)
        {
            if (gearset.HighlightColor is not null)
                return ("Custom", gearset.HighlightColor.BaseColor);
            else
                return ("Default", configurationService.DefaultHighlightColor.BaseColor);
        }

        public bool ShouldDraw => true;

        public void SetTabState(TabState state)
        {
            throw new NotImplementedException();
        }

        private void updateItemRequirements()
        {
            if (groupReqs)
            {
                itemRequirements = gearsetsService
                    .AllItemRequirements
                    .Where(entry => itemDataService.GetItemNameById(entry.Key).Contains(itemNameFilter))
                    .SelectMany(entry => entry
                        .Value
                        .GroupBy(req => (req.Gearset, entry.Key, req.ItemRequirement.RequirementType, req.ItemRequirement.CollectionStatus))
                        .Select(g => (g.First(), g.Count()))
                        .ToList()
                    ).ToList();
            }
            else
            {
                itemRequirements = gearsetsService
                    .AllItemRequirements
                    .Where(entry => itemDataService.GetItemNameById(entry.Key).Contains(itemNameFilter))
                    .SelectMany(entry => entry
                        .Value
                        .Select(g => (g, 1))
                        .ToList()
                    ).ToList();
            }

            groupedColumns[0].Sort(true);
        }

        public void PreDraw() {
            if (!firstDraw)
                return;

            firstDraw = false;

            updateItemRequirements();
        }

        public void Draw()
        {
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, "Refresh"))
            {
                itemNameFilter = string.Empty;
                updateItemRequirements();
            }

            ImGui.SameLine();

            if (ImGui.Checkbox("Group by Gearset", ref groupReqs))
                updateItemRequirements();

            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();

            ImGui.SetNextItemWidth(150f);
            ImGui.InputText("###filter_item_name_input_text", ref itemNameFilter, maxLength: 200);

            ImGui.SameLine();

            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Filter, "Filter Items"))
                updateItemRequirements();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (itemRequirements.Count == 0)
            {
                ImGui.NewLine();
                ImGuiHelpers.CenteredText("No Item Requirements");
                return;
            }

            var columns = groupReqs ? groupedColumns : ungroupedColumns;

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
                columns[sortSpecs.Specs.ColumnIndex].Sort(sortSpecs.Specs.SortDirection == ImGuiSortDirection.Descending);
            }

            var clipper = ImGui.ImGuiListClipper();
            clipper.Begin(itemRequirements.Count);

            while (clipper.Step())
            {
                for (var rowIdx = clipper.DisplayStart; rowIdx < clipper.DisplayEnd; rowIdx++)
                {
                    using var _ = ImRaii.PushId(rowIdx);
                    var reqGroup = itemRequirements[rowIdx];
                    ImGui.TableNextRow();
                    foreach (var col in columns)
                    {
                        ImGui.TableNextColumn();
                        col.Draw(reqGroup);
                    }
                }
            }
        }
    }
}
