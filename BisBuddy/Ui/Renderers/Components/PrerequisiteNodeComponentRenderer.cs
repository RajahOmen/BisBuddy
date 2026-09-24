using BisBuddy.Gear;
using BisBuddy.Gear.Prerequisites;
using BisBuddy.Items;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Lumina.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace BisBuddy.Ui.Renderers.Components;

public class PrerequisiteNodeComponentRenderer(
    ITypedLogger<PrerequisiteNodeComponentRenderer> logger,
    IConfigurationService configurationService,
    IRendererFactory rendererFactory,
    ITextureProvider textureProvider,
    IAttributeService attributeService,
    IItemDataService itemDataService,
    IDebugService debugService
    ) : ComponentRendererBase<PrerequisiteNode>
{
    private readonly ITypedLogger<PrerequisiteNodeComponentRenderer> logger = logger;
    private readonly IConfigurationService configurationService = configurationService;
    private readonly IRendererFactory rendererFactory = rendererFactory;
    private readonly ITextureProvider textureProvider = textureProvider;
    private readonly IAttributeService attributeService = attributeService;
    private readonly IItemDataService itemDataService = itemDataService;
    private readonly IDebugService debugService = debugService;
    private PrerequisiteNode? prerequisiteNode;
    private const float ButtonHeightMultiplier = 1.3f;

    private readonly HashSet<PrerequisiteNode> prereqsDrawn = [];

    private UiTheme uiTheme =>
        configurationService.UiTheme;

    public override void Initialize(PrerequisiteNode renderableComponent) =>
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

        if (actions.Count > 0)
            logger.Debug($"old node state before {actions.Count} action(s) ({prerequisiteNode.NodeId}):\n{prerequisiteNode}");

        foreach (var action in actions)
            action();

        if (actions.Count > 0)
        {

            logger.Debug($"new node state after {actions.Count} action(s) ({prerequisiteNode.NodeId}):\n{prerequisiteNode}");

            foreach (var group in prerequisiteNode.CompletePrerequisiteTree)
                logger.Verbose($"Group groups! PrerequisiteAndGroup:\n{group}]\nACTUAL GROUPS:\n{string.Join("\n", group.Groups.Select(g => $"group x{g.Count}, nodeid ({g.Node.NodeId})\n{g.Node.ToString().Replace("\n", "\n  ")}"))}");
        }
    }

    private bool drawPrerequisiteTree(
        PrerequisiteNode node,
        List<Action> actions,
        int parentCount = 1
        )
    {
        var prerequisiteTree = node.CompletePrerequisiteTree;
        if (prerequisiteTree.Count <= 0)
            return false;

        // only one option, dont need to draw a tab bar
        if (prerequisiteTree.Count == 1)
        {
            drawPrerequisiteAndGroup(
                andGroup: prerequisiteTree[0],
                actions: actions,
                parentCount: parentCount
            );
            var lastGroupNode = prerequisiteTree[0].Groups[^1].Node;
            return lastGroupNode.IsCollected || !lastGroupNode.HasPrerequisites;
        }
        else
        {
            // make a tab bar to show different options of how to retrieve item
            using var tabBar = ImRaii.TabBar($"###or_item_prerequisites_{prerequisiteTree.GetHashCode()}");
            if (!tabBar)
                return false;


            int? tabIdxDefaultActive = null;
            if (!prereqsDrawn.Contains(node))
            {
                tabIdxDefaultActive = prerequisiteTree
                    .Index()
                    .FirstOrNull(entry => entry.Item.IsActive)?.Index ?? -1;
            }

            prereqsDrawn.Add(node);
            var prereqCount = prerequisiteTree.Count;

            var lastWasCollected = false;
            for (var i = 0; i < prereqCount; i++)
            {
                var prereqGroup = prerequisiteTree[i];

                var (textColor, gameIcon) = uiTheme.GetCollectionStatusTheme(prereqGroup.CollectionStatus);

                var tabSelected = prereqCount <= 1 || i == tabIdxDefaultActive;
                var flags = tabSelected
                    ? ImGuiTabItemFlags.SetSelected
                    : ImGuiTabItemFlags.None;

                var tabName = $"Source {i + 1} ({prereqGroup.SourceType})";

                using (ImRaii.PushId(i))
                using (ImRaii.PushColor(ImGuiCol.Text, textColor))
                {
                    using (ImRaii.PushStyle(ImGuiStyleVar.DisabledAlpha, 0.5f, !prereqGroup.IsActive))
                    using (ImRaii.Enabled())
                    using (ImRaii.Disabled(!prereqGroup.IsActive))
                    using (var tabItem = ImRaii.TabItem($"{tabName}##or_node_tab_item_{i}", flags))
                    {
                        if (!prereqGroup.IsActive && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            UiComponents.SetSolidTooltip(string.Format(Resource.DisabledPrerequisiteTooltip, tabName));

                        if (!tabItem)
                            continue;
                    }

                    try
                    {
                        drawPrerequisiteAndGroup(
                            andGroup: prereqGroup,
                            actions: actions,
                            parentCount: parentCount
                        );
                        var lastGroupNode = prereqGroup.Groups[^1].Node;
                        lastWasCollected = lastGroupNode.IsCollected || !lastGroupNode.HasPrerequisites;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Error drawing nested prereq");
                    }
                }

            }

            if (prereqCount <= 1)
                return lastWasCollected;

            var toggleTabFlags = tabIdxDefaultActive == -1
                ? ImGuiTabItemFlags.SetSelected
                : ImGuiTabItemFlags.None;
            using (ImRaii.Enabled())
            using (var tabItem = ImRaii.TabItem($"{Resource.PrerequisiteOrNodeSettingsTabName}##or_node_tab_item", toggleTabFlags))
            {
                if (!tabItem)
                    return lastWasCollected;
                try
                {
                    using (ImRaii.PushIndent(5f))
                    {
                        foreach (var (idx, prereqGroup) in prerequisiteTree.Index())
                        {
                            var active = prereqGroup.IsActive;
                            if (ImGui.Checkbox($"Include Source {idx + 1} ({Enum.GetName(prereqGroup.SourceType)})##or_node_toggle_option_{idx}", ref active))
                            {
                                actions.Add(() => node.SetPrerequisiteGroupActiveStatus(idx, !prereqGroup.IsActive));
                            }
                            if (ImGui.IsItemHovered())
                            {
                                UiComponents.SetSolidTooltip(Resource.PrerequisiteOrNodeToggleTooltip);
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error drawing nested prereq");
                }
            }

            return lastWasCollected;
        }
    }

    private void drawPrerequisiteAndGroup(
        PrerequisiteAndGroup andGroup,
        List<Action> actions,
        int parentCount = 1
    )
    {
        for (var i = 0; i < andGroup.Groups.Count; i++)
        {
            using var _ = ImRaii.PushId(i);
            var prereq = andGroup.Groups[i];
            drawPrerequisiteNode(
                node: prereq.Node,
                actions: actions,
                parentCount: prereq.Count * parentCount
            );
        }
    }

    private void drawPrerequisiteNode(
        PrerequisiteNode node,
        List<Action> actions,
        int parentCount = 1
        )
    {
        var (textColor, gameIcon) = uiTheme.GetCollectionStatusTheme(node.CollectionStatus);

        var countLabel = parentCount == 1
        ? ""
        : $"{parentCount}x ";


        using (ImRaii.PushColor(ImGuiCol.Text, textColor))
        using (ImRaii.PushColor(ImGuiCol.CheckMark, textColor))
        {
            var collected = node.IsCollected;
            using (ImRaii.Disabled(!node.CollectLock))
            {
                if (drawPrerequisiteButton(node, parentCount))
                {
                    actions.Add(() => {
                        logger.Verbose($"{(!node.IsCollected ? "collecting" : "uncollecting")} node ({node.NodeId})\n{node}");
                        node.SetIsCollectedLocked(!node.IsCollected);
                    });
                }
            }
        }


        if (node.CompletePrerequisiteTree.Count > 0 && !node.IsCollected)
        {
            // draw a L shape for parent-child relationship
            var drawList = ImGui.GetWindowDrawList();
            var curLoc = ImGui.GetCursorScreenPos();
            var col = ImGui.GetColorU32(textColor with { W = textColor.W * 0.4f});
            var lineStartLoc = ImGui.GetCursorScreenPos() + new Vector2(10, - (ImGui.GetStyle().ItemSpacing.Y / 2));
            var halfButtonHeight = ImGui.GetTextLineHeightWithSpacing() * ButtonHeightMultiplier / 2;

            var lastCollectedOrLeaf = false;
            using (ImRaii.PushIndent(25.0f, scaled: false))
            {
                lastCollectedOrLeaf = drawPrerequisiteTree(
                    node: node,
                    actions: actions,
                    parentCount: parentCount
                );
            }

            var lineHeight = ImGui.GetCursorScreenPos().Y - lineStartLoc.Y - ImGui.GetStyle().ItemSpacing.Y - halfButtonHeight;
            var lineWidth = lastCollectedOrLeaf ? 10 : 25;
            drawList.AddLine(lineStartLoc, lineStartLoc + new Vector2(0, lineHeight), col, 2);
            drawList.AddLine(lineStartLoc + new Vector2(0, lineHeight), lineStartLoc + new Vector2(lineWidth, lineHeight), col, 2);
        }
    }

    private bool drawPrerequisiteButton(PrerequisiteNode node, int count)
    {
        var collectionStatusTheme = uiTheme.GetCollectionStatusTheme(node.CollectionStatus);

        var x = 0.35f;
        using (ImRaii.PushColor(ImGuiCol.Button, collectionStatusTheme.TextColor * new Vector4(x, x, x, 0.65f)))
        //using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.3f, 0.3f, 1)))
        //using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0.4f, 0.4f, 1)))
        using (ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(0, 0.5f)))
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.One * 5))
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(0.0f, ImGui.GetStyle().ItemSpacing.Y)))
        {
            var buttonSize = new Vector2(
                x: ImGui.GetContentRegionAvail().X,
                y: ImGui.GetTextLineHeightWithSpacing() * ButtonHeightMultiplier
            );

            var buttonPos = ImGui.GetCursorPos();
            var buttonScreenPos = ImGui.GetCursorScreenPos();
            var countText = $"{count}x ";
            var buttonText = $"{countText}{node.ItemName}";
            var collectionStatusButtonSize = new Vector2(buttonSize.Y, buttonSize.Y);

            var mainButton = ImGui.Button($"###{node.ItemId}gearpiece_expand_button", buttonSize);
            var mainButtonHovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
            var mainButtonHoveredActive = ImGui.IsItemHovered();

            using (ImRaii.Enabled())
                rendererFactory
                    .GetRenderer(node, RendererType.ContextMenu)
                    .Draw();

            var nextPos = ImGui.GetCursorPos();

            var textOffset = new Vector2(
                x: collectionStatusButtonSize.X * 2 + 2 * ImGuiHelpers.GlobalScale,
                y: (buttonSize.Y - ImGui.GetTextLineHeight()) / 2
                );
            var textPos = buttonPos + textOffset;
            ImGui.SetCursorPos(textPos);

            ImGui.PushClipRect(buttonScreenPos, buttonScreenPos + new Vector2(buttonSize.X, buttonSize.Y), true);
            try
            {
                ImGui.Text(buttonText);
            }
            finally
            {
                ImGui.PopClipRect();
            }


            ImGui.SetCursorPos(buttonPos);

            // COLLECTION STATUS BUTTON
            var statusButtonColor = collectionStatusTheme.TextColor * new Vector4(1, 1, 1, 0.15f);
            var collectionStatusHovered = false;
            using (ImRaii.PushColor(ImGuiCol.Button, statusButtonColor))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, statusButtonColor))
            using (ImRaii.PushColor(ImGuiCol.ButtonActive, statusButtonColor))
            using (ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, Vector2.One * 0.5f))
            {
                if (ImGui.Button("##gearpiece_collection_status_button", collectionStatusButtonSize))
                    ImGui.OpenPopup("##gearpiece_context_menu");
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    collectionStatusHovered = true;
                    var collectionStatusDesc = attributeService
                            .GetEnumAttribute<DisplayAttribute>(node.CollectionStatus)!
                            .GetDescription();

                    var tooltip = node.CollectLock
                        ? string.Format(Resource.CollectionStatusLockedTooltipPrefix, collectionStatusDesc)
                        : collectionStatusDesc;

                    using (ImRaii.Enabled())
                        UiComponents.SetSolidTooltip(tooltip);
                }
            }

            var ratio = 0.75f;
            var collectionStatusButtonIconSize = collectionStatusButtonSize * ratio;
            var iconXOffset = collectionStatusButtonSize.X * (1 - ratio) / 2;
            var iconYOffset = collectionStatusButtonSize.Y * (1 - ratio) / 2;
            ImGui.SetCursorPos(buttonPos + new Vector2(iconXOffset, iconYOffset));

            // draw lock icon
            if (node.CollectLock)
            {
                using (ImRaii.PushFont(UiBuilder.IconFont))
                {
                    var iconStr = FontAwesomeIcon.Lock.ToIconString();
                    var iconSize = ImGui.CalcTextSize(iconStr);
                    var xOffset = (collectionStatusButtonSize.X - iconSize.X) / 2;
                    var yOffset = (collectionStatusButtonSize.Y - iconSize.Y) / 2;
                    ImGui.SetCursorPos(buttonPos + new Vector2(xOffset, yOffset));
                    var color = collectionStatusTheme.TextColor * new Vector4(1f, 1f, 1f, 0.7f);
                    using (ImRaii.PushColor(ImGuiCol.Text, color))
                        ImGui.Text(iconStr);
                }
            }
            // draw collection status icon
            else
            {
                ImGui.SetCursorPos(buttonPos + new Vector2(iconXOffset, iconYOffset));

                debugService.AssertMainThreadDebug();
                if (textureProvider.GetFromGameIcon((int)collectionStatusTheme.Icon).TryGetWrap(out var texture, out var exception))
                    ImGui.Image(texture.Handle, collectionStatusButtonIconSize, collectionStatusTheme.TextColor);
            }

            ImGui.SetCursorPos(buttonPos + new Vector2(iconXOffset + collectionStatusButtonSize.X + 1 * ImGuiHelpers.GlobalScale, iconYOffset));

            // ITEM ICON
            var iconId = itemDataService.GetItemIconId(node.ItemId);
            var iconHovered = false;
            if (textureProvider
                .GetFromGameIcon((uint)iconId)
                .TryGetWrap(out var iconTexture, out var iconException)
                )
            {
                using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.65f, mainButtonHoveredActive))
                {
                    ImGui.Image(iconTexture.Handle, collectionStatusButtonIconSize);
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    using (ImRaii.Enabled())
                    using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 1.0f))
                    using (ImRaii.PushColor(ImGuiCol.Text, StyleModelV1.DalamudStandard.BuiltInColors?.DalamudWhite ?? new Vector4(1, 1, 1, 1)))
                    using (ImRaii.Tooltip())
                    {
                        iconHovered = true;
                        ImGui.Image(iconTexture.Handle, collectionStatusButtonIconSize * 4);
                    }
            }

            if (mainButtonHovered && !iconHovered && !collectionStatusHovered)
            {
                using (ImRaii.Enabled())
                {
                    if (!node.CollectLock)
                    {
                        UiComponents.SetSolidTooltip(string.Format(Resource.GearpieceLockedDisabledTooltip, node.ItemName));
                    }
                    else
                    {
                        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                        UiComponents.SetSolidTooltip(node.ItemName);
                    }
                }
            }

            ImGui.SetCursorPos(nextPos);

            return mainButton;
        }
    }
}
