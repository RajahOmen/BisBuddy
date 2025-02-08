using BisBuddy.Gear;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
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
            var gearpieceHasMelds = !gearpiece.ItemMateria.Any(m => !m.IsMelded);
            var gearpiecePrereqsCollected =
                gearpiece.PrerequisiteItems.Count > 0
                && gearpiece.PrerequisiteItems.All(p => p.IsCollected);

            var checkmarkColor = gearpieceManuallyCollected
                ? ManuallyCollectedColor
                : AutomaticallyCollectedColor;

            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(4.0f, 4.0f)))
            {
                using (ImRaii.PushColor(ImGuiCol.CheckMark, checkmarkColor))
                {
                    if (ImGui.Checkbox($"##gearpiece_collected", ref gearpieceCollected))
                    {
                        gearpiece.SetCollected(gearpieceCollected, true);
                        Services.Log.Debug($"Set \"{gearset.Name}\" gearpiece \"{gearpiece.ItemName}\" to {(gearpieceCollected ? "collected" : "not collected")}");
                        plugin.SaveGearsetsWithUpdate(true);
                    }
                }
                if (ImGui.IsItemHovered())
                {
                    var tooltip =
                        gearpieceCollected
                        ? gearpieceManuallyCollected
                        ? "Collection status locked. Inventory syncs will not uncollect"
                        : "Mark as Not Collected"
                        : "Lock as Collected";
                    ImGui.SetTooltip(tooltip);
                }

                ImGui.SameLine();

                if (ImGuiComponents.IconButton(FontAwesomeIcon.ExternalLinkAlt))
                {
                    Plugin.LinkItemById(gearpiece.ItemId);
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Link item in chat");

                ImGui.SameLine();
            }

            // what label/color to apply to gearpice text
            // by default, fully not collected
            var gearpieceCollectedLabel = "*";
            var textColor = UnobtainedColor;
            if (gearpieceCollected && gearpieceHasMelds)
            {
                gearpieceCollectedLabel = "";
                textColor = ObtainedColor;
            }
            else if (!gearpieceCollected && !gearpiecePrereqsCollected)
            {
                gearpieceCollectedLabel = "*";
                textColor = UnobtainedColor;
            }
            else
            {
                gearpieceCollectedLabel = "**";
                textColor = AlmostObtained;
            }

            var hasSubItems = gearpiece.ItemMateria.Count > 0 || gearpiece.PrerequisiteItems.Count > 0;

            using (ImRaii.PushColor(ImGuiCol.Text, textColor))
            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0)))
            {
                if (
                ImGui.CollapsingHeader($"{gearpiece.ItemName}{gearpieceCollectedLabel}###gearpiece_collapsing_header")
                && hasSubItems
                )
                {
                    ImGui.Spacing();

                    var materiaMeldedCount =
                        $"[{gearpiece.ItemMateria.Where(m => m.IsMelded).Count()}/{gearpiece.ItemMateria.Count}]";
                    var windowWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetCursorPosX();
                    var childHeight = ImGui.GetTextLineHeightWithSpacing() + (ImGui.GetStyle().FramePadding.Y * 2.0f);
                    var childHeightPadding = 6.5f;

                    // don't inherit text color for children
                    using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1.0f, 1.0f, 1.0f, 1.0f)))
                    {
                        if (gearpiece.ItemMateria.Count > 0)
                        {
                            var materiaChildHeight = childHeight + (2 * childHeightPadding);
                            using (
                                ImRaii.Child(
                                    "gearpiece_materia_child",
                                    new Vector2(windowWidth, childHeight + childHeightPadding * 2),
                                    true,
                                    ImGuiWindowFlags.AlwaysUseWindowPadding
                                    )
                                )
                            {
                                drawMateria(gearpiece);
                            }
                        }

                        if (gearpiece.PrerequisiteItems.Count > 0)
                        {
                            var prereqCount = gearpiece.PrerequisiteItems.Sum(p => p.PrerequesiteCount + 1);
                            var prereqChildHeight = (prereqCount * childHeight) + (childHeightPadding * 2);
                            using (
                                ImRaii.Child(
                                    "gearpiece_prereq_child",
                                    new Vector2(windowWidth, prereqChildHeight),
                                    true,
                                    ImGuiWindowFlags.AlwaysUseWindowPadding
                                    )
                                )
                            {
                                drawPrerequesites(gearpiece.PrerequisiteItems, gearpiece);
                            }
                        }
                    }
                    ImGui.Spacing();
                }
            }
        }
    }
}
