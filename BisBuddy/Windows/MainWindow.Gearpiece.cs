using BisBuddy.Gear;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;

namespace BisBuddy.Windows
{
    public partial class MainWindow
    {
        private static readonly Vector4 AlmostObtained = new(1.0f, 1.0f, 0.2f, 1.0f);

        private void drawGearpiece(Gearpiece gearpiece, Gearset gearset)
        {
            var gearpieceCollected = gearpiece.IsCollected;
            var gearpieceManuallyCollected = gearpiece.IsManuallyCollected;
            var gearpieceNeedsMelds = gearpiece.ItemMateria.Any(m => !m.IsMelded);
            var gearpiecePrereqsCollected =
                gearpiece.PrerequisiteItems.Count > 0
                && gearpiece.PrerequisiteItems.All(p => p.IsCollected);
            if (gearpieceManuallyCollected) ImGui.PushStyleColor(ImGuiCol.CheckMark, ManuallyCollectedColor);
            if (ImGui.Checkbox($"##gearpiece_collected", ref gearpieceCollected))
            {
                gearpiece.SetCollected(gearpieceCollected, true);
                Services.Log.Verbose($"Set \"{gearset.Name}\" gearpiece \"{gearpiece.ItemName}\" to {(gearpieceCollected ? "collected" : "not collected")}");
                plugin.SaveGearsetsWithUpdate();
            }
            if (gearpieceManuallyCollected) ImGui.PopStyleColor();
            if (ImGui.IsItemHovered())
            {
                var tooltip =
                    gearpieceCollected
                    ? gearpieceManuallyCollected
                    ? "Collection status locked. Inventory syncs will not uncollect."
                    : "Mark as Not Collected"
                    : "Lock as Collected";
                ImGui.SetTooltip(tooltip);
            }

            ImGui.SameLine();

            var gearpieceCollectedLabel = "";
            if (gearpieceCollected)
            {
                if (gearpieceNeedsMelds)
                {
                    gearpieceCollectedLabel = "**";
                    ImGui.PushStyleColor(ImGuiCol.Text, AlmostObtained);
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, ObtainedColor);
                }
            }
            else
            {
                if (gearpiecePrereqsCollected)
                {
                    gearpieceCollectedLabel = "**";
                    ImGui.PushStyleColor(ImGuiCol.Text, AlmostObtained);
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, UnobtainedColor);
                    gearpieceCollectedLabel = "*";
                }
            }

            var hasSubItems = gearpiece.ItemMateria.Count > 0 || gearpiece.PrerequisiteItems.Count > 0;

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
            if (
                ImGui.CollapsingHeader($"{gearpiece.ItemName}{gearpieceCollectedLabel}###gearpiece_collapsing_header")
                && hasSubItems
                )
            {
                ImGui.PopStyleColor();
                ImGui.PopStyleColor();
                ImGui.Indent(30);
                var materiaMeldedCount =
                    $"[{gearpiece.ItemMateria.Where(m => m.IsMelded).Count()}/{gearpiece.ItemMateria.Count}]";
                var windowWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetCursorPosX();
                var childTextHeight = ImGui.CalcTextSize("0000").Y + (ImGui.GetStyle().FramePadding.Y * 2.0f);
                var childHeightPadding = 8;

                if (gearpiece.ItemMateria.Count > 0)
                {
                    ImGui.BeginChild("gearpiece_materia_child", new Vector2(windowWidth, childTextHeight + childHeightPadding * 2), true);
                    drawMateria(gearpiece);
                    ImGui.EndChild();
                }

                if (gearpiece.PrerequisiteItems.Count > 0)
                {
                    var gearpiecePrereqCount = gearpiece.PrerequisiteItems.Sum(p => p.PrerequesiteCount + 1);
                    ImGui.BeginChild("gearpiece_prereq_child", new Vector2(windowWidth, (childTextHeight + childHeightPadding) * gearpiecePrereqCount + childHeightPadding), true);
                    drawPrerequesites(gearpiece.PrerequisiteItems, gearpiece);
                    ImGui.EndChild();
                }
                ImGui.Unindent(30);
                ImGui.Spacing();
            }
            else
            {
                ImGui.PopStyleColor();
                ImGui.PopStyleColor();
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) Plugin.LinkItemById(gearpiece.ItemId);
            if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }
}
