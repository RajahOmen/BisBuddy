using BisBuddy.Gear.Prerequisites;
using BisBuddy.Services;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using System;
using BisBuddy.Resources;
using System.Numerics;
using BisBuddy.Services.Configuration;
using BisBuddy.Gear;

namespace BisBuddy.Ui.Renderers.Components;

public class PrerequisiteNodeComponentRenderer(
    ITypedLogger<PrerequisiteNodeComponentRenderer> logger,
    IConfigurationService configurationService
    ) : ComponentRendererBase<IPrerequisiteNode>
{
    private readonly ITypedLogger<PrerequisiteNodeComponentRenderer> logger = logger;
    private readonly IConfigurationService configurationService = configurationService;
    private IPrerequisiteNode? prerequisiteNode;

    private UiTheme uiTheme =>
        configurationService.UiTheme;

    public override void Initialize(IPrerequisiteNode renderableComponent) =>
        prerequisiteNode = renderableComponent;

    public override void Draw()
    {
        if (prerequisiteNode is null)
        {
            logger.Error("Attempted to draw uninitialized component renderer");
            return;
        }

        drawPrerequisiteTree(prerequisiteNode);
    }

    private void drawPrerequisiteTree(IPrerequisiteNode prerequisiteNode, int parentCount = 1)
    {
        var nodeType = prerequisiteNode.GetType();
        if (nodeType == typeof(PrerequisiteOrNode))
            drawOrNode((PrerequisiteOrNode)prerequisiteNode, parentCount);
        else if (nodeType == typeof(PrerequisiteAndNode))
            drawAndNode((PrerequisiteAndNode)prerequisiteNode, parentCount);
        else if (nodeType == typeof(PrerequisiteAtomNode))
            drawAtomNode((PrerequisiteAtomNode)prerequisiteNode, parentCount);
        else
            logger.Error($"Cannot render {nameof(IPrerequisiteNode)} type \"{prerequisiteNode.GetType()}\"");
    }

    private void drawOrNode(PrerequisiteOrNode node, int parentCount = 1)
    {
        using var tabBar = ImRaii.TabBar($"###or_item_prerequisites_{node.GetHashCode()}");
        if (tabBar)
        {
            for (var i = 0; i < node.PrerequisiteTree.Count; i++)
            {
                var prereq = node.PrerequisiteTree[i];
                var prereqLabelColorblind = prereq.IsCollected
                    ? ""
                    : prereq.CollectionStatus == CollectionStatusType.Obtainable
                    ? "**"
                    : "*";

                var (textColor, gameIcon) = uiTheme.GetCollectionStatusTheme(node.CollectionStatus);

                using (ImRaii.PushId(i))
                using (ImRaii.PushColor(ImGuiCol.Text, textColor))
                using (var tabItem = ImRaii.TabItem($"Source {i + 1} ({prereq.SourceType}){prereqLabelColorblind}###or_node_tab_item_{i}"))
                {
                    if (!tabItem)
                        return;
                    try
                    {
                        drawPrerequisiteTree(prereq, parentCount);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error drawing nested prereq");
                    }
                }
            }
        }
    }

    private void drawAndNode(PrerequisiteAndNode node, int parentCount = 1)
    {
        var groupedPrereqs = node.Groups();

        for (var i = 0; i < groupedPrereqs.Count; i++)
        {
            using var _ = ImRaii.PushId(i);
            var prereq = groupedPrereqs[i];
            drawPrerequisiteTree(prereq.Node, prereq.Count * parentCount);
        }
    }

    private void drawAtomNode(PrerequisiteAtomNode node, int parentCount = 1)
    {
        var prereqLabelColorblind = node.IsCollected
        ? ""
        : node.CollectionStatus == CollectionStatusType.Obtainable
        ? "**"
        : "*";

        var countLabel = parentCount == 1
            ? ""
            : $"{parentCount}x ";

        var (textColor, gameIcon) = uiTheme.GetCollectionStatusTheme(node.CollectionStatus);

        using (ImRaii.PushColor(ImGuiCol.Text, textColor))
        {
            if (node.CollectLock)
            {
                using (ImRaii.PushFont(UiBuilder.IconFont))
                    ImGui.Text(FontAwesomeIcon.Check.ToIconString());

                if (ImGui.IsItemHovered())
                    using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1, 1, 1, 1)))
                        ImGui.SetTooltip(Resource.ManuallyCollectedTooltip);

                ImGui.SameLine();
            }

            //using (ImRaii.Disabled(parentGearpiece.IsCollected))
            //{
            //
            //}
            if (ImGui.Button($"{countLabel}{node.ItemName}{prereqLabelColorblind}##collect_prereq_button"))
            {
                node.SetIsCollectedLocked(!node.IsCollected);
                //logger.Debug($"Set gearpiece \"{parentGearpiece.ItemName}\" prereq \"{node.ItemName}\" to {(node.IsCollected ? "collected" : "not collected")}");
            }
            if (ImGui.IsItemHovered())
            {
                if (node.IsCollected)
                    ImGui.SetTooltip(string.Format(Resource.PrerequisiteTooltipBase, Resource.AutomaticallyCollectedTooltip));
                else
                    ImGui.SetTooltip(string.Format(Resource.PrerequisiteTooltipBase, Resource.UncollectedTooltip));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            //if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            //    searchItemById(node.ItemId);
        }

        if (node.PrerequisiteTree.Count > 1)
            throw new Exception($"item {node.ItemName} has too many prerequisites ({node.PrerequisiteTree})");

        if (node.PrerequisiteTree.Count == 1 && !node.IsCollected)
        {
            // draw a L shape for parent-child relationship
            var drawList = ImGui.GetWindowDrawList();
            var curLoc = ImGui.GetCursorScreenPos();
            var col = ImGui.GetColorU32(textColor);
            var halfButtonHeight = ImGui.CalcTextSize("HI").Y / 2 + ImGui.GetStyle().FramePadding.Y;
            drawList.AddLine(curLoc + new Vector2(10, 0), curLoc + new Vector2(10, halfButtonHeight), col, 2);
            drawList.AddLine(curLoc + new Vector2(10, halfButtonHeight), curLoc + new Vector2(20, halfButtonHeight), col, 2);

            using (ImRaii.PushIndent(25.0f, scaled: false))
                drawPrerequisiteTree(node.PrerequisiteTree[0], parentCount);
        }
    }
}
