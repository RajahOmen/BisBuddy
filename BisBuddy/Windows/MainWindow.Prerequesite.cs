using BisBuddy.Gear.Prerequesites;
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
            using var tabBar = ImRaii.TabBar("##or_item_prerequesites");
            if (tabBar)
            {
                for (var i = 0; i < node.PrerequesiteTree.Count; i++)
                {
                    var prereq = node.PrerequesiteTree[i];
                    using var _ = ImRaii.PushId(i);
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

                    using (ImRaii.PushColor(ImGuiCol.Text, textColor))
                    using (var tabItem = ImRaii.TabItem($"Source {i + 1} ({prereq.SourceType}){prereqLabelColorblind}###sourcetabitem"))
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

        private void drawAndNode(PrerequesiteAndNode node, int mult = 1)
        {
            var groupedPrereqs = node.Groups();

            for (var i = 0; i < groupedPrereqs.Count; i++)
            {
                var _ = ImRaii.PushId(i);
                var prereq = groupedPrereqs[i];
                drawPrerequesiteTree(prereq.Node, prereq.Count * mult);
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
                if (ImGui.Button($"{countLabel}{node.ItemName}{prereqLabelColorblind}##collect_prereq_button"))
                    node.SetCollected(!node.IsCollected, true);
            }

            if (node.PrerequesiteTree.Count > 1)
                throw new Exception($"item {node.ItemName} has too many prerequesites ({node.PrerequesiteTree})");

            if (node.PrerequesiteTree.Count == 1 && !node.IsCollected)
            {
                using (ImRaii.PushIndent(40.0f))
                {
                    drawPrerequesiteTree(node.PrerequesiteTree[0], parentCount);
                }
            }
        }

        private void drawPrerequesiteTree(PrerequesiteNode prerequesiteNode, int parentCount = 1)
        {
            if (prerequesiteNode.GetType() == typeof(PrerequesiteOrNode))
            {
                using var _ = ImRaii.PushId(1);
                drawOrNode((PrerequesiteOrNode) prerequesiteNode, parentCount);
            }
            else if (prerequesiteNode.GetType() == typeof(PrerequesiteAndNode))
            {
                using var _ = ImRaii.PushId(2);
                drawAndNode((PrerequesiteAndNode) prerequesiteNode, parentCount);
            }
            else if (prerequesiteNode.GetType() == typeof(PrerequesiteAtomNode))
            {
                using var _ = ImRaii.PushId(3);
                drawAtomNode((PrerequesiteAtomNode) prerequesiteNode, parentCount);
            }
        }
    }
}
