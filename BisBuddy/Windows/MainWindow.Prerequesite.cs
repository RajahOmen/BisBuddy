using BisBuddy.Gear.Prerequesites;
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
        private void drawOrNode(PrerequesiteOrNode node, int parentCount = 1)
        {
            using var tabBar = ImRaii.TabBar($"###or_item_prerequesites_{node.GetHashCode()}");
            if (tabBar)
            {
                for (var i = 0; i < node.PrerequesiteTree.Count; i++)
                {
                    var prereq = node.PrerequesiteTree[i];
                    var prereqLabelColorblind = prereq.IsCollected
                        ? ""
                        : prereq.IsObtainable
                        ? "**"
                        : "*";

                    Vector4 textColor;
                    if (prereq.IsCollected)
                        textColor = ObtainedColor;
                    else if (prereq.PrerequesiteCount() > 0 && prereq.IsObtainable)
                        textColor = AlmostObtained;
                    else
                        textColor = UnobtainedColor;

                    using (ImRaii.PushId(i))
                    using (ImRaii.PushColor(ImGuiCol.Text, textColor))
                    using (var tabItem = ImRaii.TabItem($"Source {i + 1} ({prereq.SourceType}){prereqLabelColorblind}###or_node_tab_item_{i}"))
                    {
                        if (tabItem)
                        {
                            ImGui.Spacing();
                            try
                            {
                                drawPrerequesiteTree(prereq, parentCount);
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

        private void drawAndNode(PrerequesiteAndNode node, int parentCount = 1)
        {
            var groupedPrereqs = node.Groups();

            for (var i = 0; i < groupedPrereqs.Count; i++)
            {
                using var _ = ImRaii.PushId(i);
                var prereq = groupedPrereqs[i];
                drawPrerequesiteTree(prereq.Node, prereq.Count * parentCount);
            }
        }

        private void drawAtomNode(PrerequesiteAtomNode node, int parentCount = 1)
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
            else if (node.PrerequesiteCount() > 0 && node.IsObtainable)
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
                if (ImGui.Button($"{countLabel}{node.ItemName}{prereqLabelColorblind}##collect_prereq_button"))
                    node.SetCollected(!node.IsCollected, true);
            }

            if (node.PrerequesiteTree.Count > 1)
                throw new Exception($"item {node.ItemName} has too many prerequesites ({node.PrerequesiteTree})");

            if (node.PrerequesiteTree.Count == 1 && !node.IsCollected)
            {
                var drawList = ImGui.GetWindowDrawList();
                var curLoc = ImGui.GetCursorScreenPos();
                var col = ImGui.GetColorU32(textColor);
                var textHeight = (ImGui.CalcTextSize("HI").Y / 2) + ImGui.GetStyle().FramePadding.Y;

                using (ImRaii.PushIndent(25.0f, scaled: false))
                {
                    drawList.AddLine(curLoc + new Vector2(10, 0), curLoc + new Vector2(10, textHeight), col, 2);
                    drawList.AddLine(curLoc + new Vector2(10, textHeight), curLoc + new Vector2(20, textHeight), col, 2);
                    drawPrerequesiteTree(node.PrerequesiteTree[0], parentCount);
                }
            }
        }

        private void drawPrerequesiteTree(PrerequesiteNode prerequesiteNode, int parentCount = 1)
        {
            if (prerequesiteNode.GetType() == typeof(PrerequesiteOrNode))
            {
                drawOrNode((PrerequesiteOrNode) prerequesiteNode, parentCount);
            }
            else if (prerequesiteNode.GetType() == typeof(PrerequesiteAndNode))
            {
                drawAndNode((PrerequesiteAndNode) prerequesiteNode, parentCount);
            }
            else if (prerequesiteNode.GetType() == typeof(PrerequesiteAtomNode))
            {
                drawAtomNode((PrerequesiteAtomNode) prerequesiteNode, parentCount);
            }
        }
    }
}
