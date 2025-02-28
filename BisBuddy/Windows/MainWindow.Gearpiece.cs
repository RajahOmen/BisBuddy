using BisBuddy.Gear;
using BisBuddy.Gear.Prerequesites;
using BisBuddy.Resources;
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
                gearpiece.PrerequisiteTree != null
                && gearpiece.PrerequisiteTree.PrerequesiteTree.All(p => p.IsCollected);

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

                        // don't update here. Creates issues with being unable to unassign pieces reliably due to no manual lock for uncollected.
                        plugin.SaveGearsetsWithUpdate(false);
                    }
                }
                if (ImGui.IsItemHovered())
                {
                    var tooltip =
                        gearpieceCollected
                        ? gearpieceManuallyCollected
                        ? Resource.ManuallyCollectedTooltip
                        : Resource.AutomaticallyCollectedTooltip
                        : Resource.UncollectedTooltip;
                    ImGui.SetTooltip(tooltip);
                }

                ImGui.SameLine();

                if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
                    Plugin.SearchItemById(gearpiece.ItemId);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(string.Format(Resource.SearchInventoryForItemTooltip, gearpiece.ItemName));

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
            else if (!gearpieceCollected && !gearpiecePrereqsCollected && !gearpiece.IsObtainable)
            {
                gearpieceCollectedLabel = "*";
                textColor = UnobtainedColor;
            }
            else
            {
                gearpieceCollectedLabel = "**";
                textColor = AlmostObtained;
            }

            var hasSubItems = gearpiece.ItemMateria.Count > 0 || gearpiece.PrerequisiteTree != null;

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

                        if (gearpiece.PrerequisiteTree != null)
                        {
                            drawPrerequesiteTree(gearpiece.PrerequisiteTree);
                            ImGui.Spacing();
                            ImGui.Separator();
                            //var prereqCount = gearpiece.Prerequisites.PrerequesiteCount();
                            //var prereqChildHeight = (prereqCount * childHeight) + (childHeightPadding * 2);
                            //using (
                            //    ImRaii.Child(
                            //        "gearpiece_prereq_child",
                            //        new Vector2(windowWidth, prereqChildHeight),
                            //        true,
                            //        ImGuiWindowFlags.AlwaysUseWindowPadding
                            //        )
                            //    )
                            //{
                                
                            //}
                        }
                    }
                    ImGui.Spacing();
                }
            }
        }
    }
}
