using BisBuddy.Gear;
using BisBuddy.Gear.Prerequisites;
using BisBuddy.Resources;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Numerics;

namespace BisBuddy.Windows
{
    public partial class MainWindow
    {
        private void drawOrNode(PrerequisiteOrNode node, Gearpiece parentGearpiece, int parentCount = 1)
        {
            using var tabBar = ImRaii.TabBar($"###or_item_prerequisites_{node.GetHashCode()}");
            if (tabBar)
            {
                for (var i = 0; i < node.PrerequisiteTree.Count; i++)
                {
                    var prereq = node.PrerequisiteTree[i];
                    var prereqLabelColorblind = prereq.IsCollected
                        ? ""
                        : prereq.IsObtainable
                        ? "**"
                        : "*";

                    Vector4 textColor;
                    if (prereq.IsCollected)
                        textColor = ObtainedColor;
                    else if (prereq.PrerequisiteCount() > 0 && prereq.IsObtainable)
                        textColor = AlmostObtained;
                    else
                        textColor = UnobtainedColor;

                    using (ImRaii.PushId(i))
                    using (ImRaii.PushColor(ImGuiCol.Text, textColor))
                    using (var tabItem = ImRaii.TabItem($"Source {i + 1} ({prereq.SourceType}){prereqLabelColorblind}###or_node_tab_item_{i}"))
                    {
                        if (tabItem)
                        {
                            try
                            {
                                drawPrerequisiteTree(prereq, parentGearpiece, parentCount);
                            }
                            catch (Exception ex)
                            {
                                Services.Log.Error(ex, "Error drawing nested prereq");
                            }
                        }
                    }
                }
            }
        }

        private void drawAndNode(PrerequisiteAndNode node, Gearpiece parentGearpiece, int parentCount = 1)
        {
            var groupedPrereqs = node.Groups();

            for (var i = 0; i < groupedPrereqs.Count; i++)
            {
                using var _ = ImRaii.PushId(i);
                var prereq = groupedPrereqs[i];
                drawPrerequisiteTree(prereq.Node, parentGearpiece, prereq.Count * parentCount);
            }
        }

        private void drawAtomNode(PrerequisiteAtomNode node, Gearpiece parentGearpiece, int parentCount = 1)
        {
            var prereqLabelColorblind = node.IsCollected
            ? ""
            : node.IsObtainable
            ? "**"
            : "*";

            var countLabel = parentCount == 1
                ? ""
                : $"{parentCount}x ";

            Vector4 textColor;
            if (node.IsCollected)
                textColor = ObtainedColor;
            else if (node.PrerequisiteCount() > 0 && node.IsObtainable)
                textColor = AlmostObtained;
            else
                textColor = UnobtainedColor;

            using (ImRaii.PushColor(ImGuiCol.Text, textColor))
            {
                if (node.IsManuallyCollected)
                {
                    using (ImRaii.PushFont(UiBuilder.IconFont))
                    {
                        ImGui.Text(FontAwesomeIcon.Check.ToIconString());
                    }
                    if (ImGui.IsItemHovered())
                    {
                        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1, 1, 1, 1)))
                        {
                            ImGui.SetTooltip(Resource.ManuallyCollectedTooltip);
                        }
                    }

                    ImGui.SameLine();
                }

                using (ImRaii.Disabled(parentGearpiece.IsCollected))
                {
                    if (ImGui.Button($"{countLabel}{node.ItemName}{prereqLabelColorblind}##collect_prereq_button"))
                    {
                        node.SetCollected(!node.IsCollected, true);
                        Services.Log.Debug($"Set gearpiece \"{parentGearpiece.ItemName}\" prereq \"{node.ItemName}\" to {(node.IsCollected ? "collected" : "not collected")}");

                        // don't update here. Creates issues with being unable to unassign prereqs reliably due to no manual lock for uncollected
                        plugin.SaveGearsetsWithUpdate(false);
                    }
                }
                if (ImGui.IsItemHovered())
                {
                    if (node.IsCollected)
                    {
                        ImGui.SetTooltip(string.Format(Resource.PrerequisiteTooltipBase, Resource.AutomaticallyCollectedTooltip));
                    }
                    else
                    {
                        ImGui.SetTooltip(string.Format(Resource.PrerequisiteTooltipBase, Resource.UncollectedTooltip));
                    }
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    Plugin.SearchItemById(node.ItemId);
            }

            if (node.PrerequisiteTree.Count > 1)
                throw new Exception($"item {node.ItemName} has too many prerequisites ({node.PrerequisiteTree})");

            if (node.PrerequisiteTree.Count == 1 && !node.IsCollected)
            {
                // draw a L shape for parent-child relationship
                var drawList = ImGui.GetWindowDrawList();
                var curLoc = ImGui.GetCursorScreenPos();
                var col = ImGui.GetColorU32(textColor);
                var halfButtonHeight = (ImGui.CalcTextSize("HI").Y / 2) + ImGui.GetStyle().FramePadding.Y;
                drawList.AddLine(curLoc + new Vector2(10, 0), curLoc + new Vector2(10, halfButtonHeight), col, 2);
                drawList.AddLine(curLoc + new Vector2(10, halfButtonHeight), curLoc + new Vector2(20, halfButtonHeight), col, 2);

                using (ImRaii.PushIndent(25.0f, scaled: false))
                {
                    drawPrerequisiteTree(node.PrerequisiteTree[0], parentGearpiece, parentCount);
                }
            }
        }

        private void drawPrerequisiteTree(IPrerequisiteNode prerequisiteNode, Gearpiece parentGearpiece, int parentCount = 1)
        {
            if (prerequisiteNode.GetType() == typeof(PrerequisiteOrNode))
            {
                drawOrNode((PrerequisiteOrNode)prerequisiteNode, parentGearpiece, parentCount);
            }
            else if (prerequisiteNode.GetType() == typeof(PrerequisiteAndNode))
            {
                drawAndNode((PrerequisiteAndNode)prerequisiteNode, parentGearpiece, parentCount);
            }
            else if (prerequisiteNode.GetType() == typeof(PrerequisiteAtomNode))
            {
                drawAtomNode((PrerequisiteAtomNode)prerequisiteNode, parentGearpiece, parentCount);
            }
        }
    }
}
