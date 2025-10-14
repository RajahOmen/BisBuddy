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
using System.Collections.Generic;
using System.Linq;
using Lumina.Extensions;

namespace BisBuddy.Ui.Renderers.Components;

public class PrerequisiteNodeComponentRenderer(
    ITypedLogger<PrerequisiteNodeComponentRenderer> logger,
    IConfigurationService configurationService,
    IRendererFactory rendererFactory
    ) : ComponentRendererBase<IPrerequisiteNode>
{
    private readonly ITypedLogger<PrerequisiteNodeComponentRenderer> logger = logger;
    private readonly IConfigurationService configurationService = configurationService;
    private readonly IRendererFactory rendererFactory = rendererFactory;
    private IPrerequisiteNode? prerequisiteNode;

    private HashSet<PrerequisiteOrNode> prereqsDrawn = [];

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

        var actions = new List<Action>();
        drawPrerequisiteTree(prerequisiteNode, actions);
        foreach (var action in actions)
            action();
    }

    private void drawPrerequisiteTree(IPrerequisiteNode prerequisiteNode, List<Action> actions, int parentCount = 1)
    {
        var nodeType = prerequisiteNode.GetType();
        if (nodeType == typeof(PrerequisiteOrNode))
            drawOrNode((PrerequisiteOrNode)prerequisiteNode, actions, parentCount);
        else if (nodeType == typeof(PrerequisiteAndNode))
            drawAndNode((PrerequisiteAndNode)prerequisiteNode, actions, parentCount);
        else if (nodeType == typeof(PrerequisiteAtomNode))
            drawAtomNode((PrerequisiteAtomNode)prerequisiteNode, actions, parentCount);
        else
            logger.Error($"Cannot render {nameof(IPrerequisiteNode)} type \"{prerequisiteNode.GetType()}\"");
    }

    private void drawOrNode(PrerequisiteOrNode node, List<Action> actions, int parentCount = 1)
    {
        using var tabBar = ImRaii.TabBar($"###or_item_prerequisites_{node.GetHashCode()}");
        if (!tabBar)
            return;


        int? tabIdxDefaultActive = null;
        if (!prereqsDrawn.Contains(node))
        {
            //logger.Verbose($"no value, finding...");
            tabIdxDefaultActive = node.CompletePrerequisiteTree
                .Index()
                .FirstOrNull(entry => entry.Item.IsActive)?.Index ?? -1;
        }

        prereqsDrawn.Add(node);

        for (var i = 0; i < node.CompletePrerequisiteTree.Count; i++)
        {
            var (prereqNode, prereqIsActive) = node.CompletePrerequisiteTree[i];

            var (textColor, gameIcon) = uiTheme.GetCollectionStatusTheme(prereqNode.CollectionStatus);

            var tabSelected = i == tabIdxDefaultActive;
            var flags = tabSelected
                ? ImGuiTabItemFlags.SetSelected
                : ImGuiTabItemFlags.None;

            using (ImRaii.PushId(i))
            using (ImRaii.PushColor(ImGuiCol.Text, textColor))
            using (ImRaii.Disabled(!prereqIsActive))
            using (var tabItem = ImRaii.TabItem($"Source {i + 1} ({prereqNode.SourceType})##or_node_tab_item_{i}", flags))
            {
                if (!tabItem)
                    continue;
                try
                {
                    drawPrerequisiteTree(prereqNode, actions, parentCount);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error drawing nested prereq");
                }
            }
        }

        var toggleTabFlags = tabIdxDefaultActive == -1
            ? ImGuiTabItemFlags.SetSelected
            : ImGuiTabItemFlags.None;
        using (var tabItem = ImRaii.TabItem($"Settings##or_node_tab_item", toggleTabFlags))
        {
            if (!tabItem)
                return;
            try
            {
                foreach (var (idx, (prereqNode, prereqIsActive)) in node.CompletePrerequisiteTree.Index())
                {
                    var active = prereqIsActive;
                    if (ImGui.Checkbox($"Include Source {idx + 1} ({prereqNode.SourceType})##or_node_toggle_option_{idx}", ref active))
                    {
                        actions.Add(() => node.SetPrerequisiteActiveStatus(prereqNode, !prereqIsActive));
                    }
                }
                //drawPrerequisiteTree(prereq, actions, parentCount);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error drawing nested prereq");
            }
        }
    }

    private void drawAndNode(PrerequisiteAndNode node, List<Action> actions, int parentCount = 1)
    {
        var groupedPrereqs = node.Groups();

        for (var i = 0; i < groupedPrereqs.Count; i++)
        {
            using var _ = ImRaii.PushId(i);
            var prereq = groupedPrereqs[i];
            drawPrerequisiteTree(prereq.Node, actions, prereq.Count * parentCount);
        }
    }

    private void drawAtomNode(PrerequisiteAtomNode node, List<Action> actions, int parentCount = 1)
    {
        var countLabel = parentCount == 1
            ? ""
            : $"{parentCount}x ";

        var (textColor, gameIcon) = uiTheme.GetCollectionStatusTheme(node.CollectionStatus);

        using (ImRaii.PushColor(ImGuiCol.Text, textColor))
        using (ImRaii.PushColor(ImGuiCol.CheckMark, textColor))
        {
            var collected = node.IsCollected;
            if (ImGui.Checkbox($"{countLabel}{node.ItemName}##collect_prereq_button", ref collected))
            {
                actions.Add(() => node.SetIsCollectedLocked(!node.IsCollected));
            }
            if (ImGui.IsItemHovered())
            {
                if (node.IsCollected)
                    ImGui.SetTooltip(string.Format(Resource.PrerequisiteTooltipBase, Resource.AutomaticallyCollectedTooltip));
                else
                    ImGui.SetTooltip(string.Format(Resource.PrerequisiteTooltipBase, Resource.UncollectedTooltip));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            rendererFactory.GetRenderer(node, RendererType.ContextMenu).Draw();
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
                drawPrerequisiteTree(node.PrerequisiteTree[0], actions, parentCount);
        }
    }
}
