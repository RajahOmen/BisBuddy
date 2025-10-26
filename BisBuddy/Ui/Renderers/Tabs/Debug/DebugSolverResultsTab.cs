using BisBuddy.Factories;
using BisBuddy.ItemAssignment;
using BisBuddy.Items;
using BisBuddy.Services.Gearsets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using System;
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
                gearsetsService.ScheduleUpdateFromInventory(saveChanges: false);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (gearpiecesResult is SolveResult gearResult)
                drawSolveResult("Gearpiece Assignments", gearResult);
            else
                ImGui.Text("No Gearpiece Assignments");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (prerequisitesResult is SolveResult prereqResult)
                drawSolveResult("Prerequisite Assignments", prereqResult);
            else
                ImGui.Text("No Prerequisite Assignments");
        }

        private void drawSolveResult(string resultTitle, SolveResult result)
        {
            ImGui.Text(resultTitle);

            ImGui.Spacing();

            var (assignments, edges, candidateItems, assignmentGroups) = result;

            // 2 for idx, gearpiece
            var numColumns = 2 + candidateItems.Count;
            var flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp;
            using var table = ImRaii.Table($"{resultTitle}##solve_result_table", numColumns, flags);
            if (!table)
                return;

            // HEADER SETUP

            ImGui.TableSetupColumn("Idx", ImGuiTableColumnFlags.WidthFixed, 25f);
            ImGui.TableSetupColumn("Gearpiece");
            foreach (var item in candidateItems)
                ImGui.TableSetupColumn(itemDataService.GetItemNameById(item.ItemId));

            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
            for (var i = 0; i < numColumns; i++)
            {
                if (!ImGui.TableSetColumnIndex(i))
                    continue;

                var colName = ImGui.TableGetColumnName(i);
                ImGui.TableHeader(colName);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(colName);
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

                 //was this assign group actually assigned a candidate item?
                int? assignIdx = null;
                for (var i = 0; i < assignments.Length; i++)
                {
                    // TODO: Coupling here, change when replacing ItemAssignmentSolver
                    if (assignments[i] == rowIdx && edges[rowIdx, i] != ItemAssigmentSolver.NoEdgeWeightValue)
                    {
                        assignIdx = i;
                        break;
                    }
                }

                ImGui.Text($"{rowIdx}");
                ImGui.TableNextColumn();

                var assignGroupItemName = itemDataService.GetItemNameById(assignGroup.ItemId);
                if (assignIdx is not null)
                    ImGui.TextColored(pickedColor, assignGroupItemName);
                else
                    ImGui.Text(assignGroupItemName);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(assignGroupItemName);

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

                    if (colIdx == assignIdx)
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
