using BisBuddy.Factories;
using BisBuddy.ItemAssignment;
using BisBuddy.Items;
using BisBuddy.Services.Gearsets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.Numerics;
using static Dalamud.Interface.Windowing.Window;

namespace BisBuddy.Ui.Renderers.Tabs.Debug
{
    public class DebugSolverResultsTab(
        IItemAssignmentSolverFactory solverFactory,
        IItemDataService itemDataService,
        IGearsetsService gearsetsService
        ) : TabRenderer<DebugToolTab>
    {
        private readonly IItemAssignmentSolverFactory solverFactory = solverFactory;
        private readonly IItemDataService itemDataService = itemDataService;
        private readonly IGearsetsService gearsetsService = gearsetsService;

        private SolveResult? gearpiecesResult => solverFactory.LastCreatedSolver?.GearpiecesResult;
        private SolveResult? prerequisitesResult => solverFactory.LastCreatedSolver?.PrerequisitesResult;

        public WindowSizeConstraints? TabSizeConstraints => null;

        public bool ShouldDraw => true;

        public void SetTabState(TabState state)
        {
            throw new NotImplementedException();
        }

        public void PreDraw() { }

        public void Draw()
        {
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, "Sync Inventory"))
                gearsetsService.QueueUpdateFromInventory(saveChanges: false);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            using var tabBar = ImRaii.TabBar("##solve_results_tabs");
            if (!tabBar)
                return;

            using (var gearpieceTab = ImRaii.TabItem("Gearpieces"))
            {
                if (gearpieceTab)
                {
                    if (gearpiecesResult is SolveResult gearResult
                        && gearResult.CandidateItems.Count > 0
                        && gearResult.AssignmentGroups.Count > 0)
                    {
                        drawSolveResult(gearResult);
                    }
                    else
                    {
                        ImGui.NewLine();
                        ImGuiHelpers.CenteredText("No Gearpiece Assignments");
                    }
                }
            }

            using (var prerequisitesTab = ImRaii.TabItem("Prerequisites"))
            {
                if (prerequisitesTab)
                {
                    if (prerequisitesResult is SolveResult prereqResult
                        && prereqResult.CandidateItems.Count > 0
                        && prereqResult.AssignmentGroups.Count > 0)
                    {
                        drawSolveResult(prereqResult);
                    }
                    else
                    {
                        ImGui.NewLine();
                        ImGuiHelpers.CenteredText("No Prerequisite Assignments");
                    }
                }
            }
        }

        private void drawSolveResult(SolveResult result)
        {
            ImGui.Spacing();

            var (assignments, edges, candidateItems, assignmentGroups) = result;

            // 2 for idx, gearpiece
            var numColumns = 2 + candidateItems.Count;
            var flags = (
                ImGuiTableFlags.RowBg
                | ImGuiTableFlags.BordersInnerV
                | ImGuiTableFlags.SizingStretchProp
                | ImGuiTableFlags.PadOuterX
                );
            using var table = ImRaii.Table($"##solve_result_table", numColumns, flags);
            if (!table)
                return;

            // HEADER SETUP
            ImGui.TableSetupColumn("Idx", ImGuiTableColumnFlags.WidthFixed, 25f);
            ImGui.TableSetupColumn("Gearpiece", initWidthOrWeight: 2f);
            foreach (var item in candidateItems)
                ImGui.TableSetupColumn(itemDataService.GetItemNameById(item.ItemId));

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableNextRow(ImGuiTableRowFlags.Headers);

            ImGui.TableNextColumn();
            ImGui.TableHeader("Idx");

            ImGui.TableNextColumn();
            ImGui.TableHeader("Gearpiece");

            foreach (var item in candidateItems)
            {
                ImGui.TableNextColumn();

                var itemName = itemDataService.GetItemNameById(item.ItemId);
                ImGui.TableHeader(itemName);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{itemName} ({item.ItemId})");
                if (ImGui.IsItemClicked())
                    ImGui.SetClipboardText($"{item.ItemId}");
            }

            // TABLE DATA
            var pickedColor = new Vector4(0, 1, 0, 1);
            var numRows = edges.GetLength(0);
            var numCols = edges.GetLength(1);

            for (var rowIdx = 0; rowIdx < numRows; rowIdx++)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                var assignGroup = assignmentGroups[rowIdx];

                // was this assign group actually assigned a candidate item?
                var assignIdxs = new HashSet<int>();
                for (var i = 0; i < assignments.Length; i++)
                {
                    // TODO: Coupling here, change when replacing ItemAssignmentSolver
                    if (assignments[i] == rowIdx && edges[rowIdx, i] != ItemAssigmentSolver.NoEdgeWeightValue)
                        assignIdxs.Add(i);
                }

                ImGui.Text($"{rowIdx}");
                ImGui.TableNextColumn();

                var assignGroupItemName = itemDataService.GetItemNameById(assignGroup.ItemId);
                if (assignIdxs.Count > 0)
                    ImGui.TextColored(pickedColor, assignGroupItemName);
                else
                    ImGui.Text(assignGroupItemName);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{assignGroupItemName} ({assignGroup.ItemId})");
                if (ImGui.IsItemClicked())
                    ImGui.SetClipboardText($"{assignGroup.ItemId}");

                // draw the edge weights
                for (var colIdx = 0; colIdx < numCols; colIdx++)
                {
                    ImGui.TableNextColumn();
                    var edgeWeight = edges[rowIdx, colIdx];

                    var edgeWeightLabel = $"{edgeWeight}";
                    if (edgeWeight == ItemAssigmentSolver.NoEdgeWeightValue)
                        edgeWeightLabel = "";
                    if (edgeWeight == ItemAssigmentSolver.DummyEdgeWeightValue)
                        edgeWeightLabel = "DUMMY";

                    if (assignIdxs.Contains(colIdx))
                        ImGui.TextColored(pickedColor, edgeWeightLabel);
                    else
                        ImGui.Text(edgeWeightLabel);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"{edgeWeight}");
                }
            }
        }
    }
}
