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
using BisBuddy.Items;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using System.ComponentModel.DataAnnotations;

namespace BisBuddy.Ui.Renderers.Components;

public class PrerequisiteNodeComponentRenderer(
    ITypedLogger<PrerequisiteNodeComponentRenderer> logger,
    IConfigurationService configurationService,
    IRendererFactory rendererFactory,
    ITextureProvider textureProvider,
    IAttributeService attributeService,
    IItemDataService itemDataService
    ) : ComponentRendererBase<IPrerequisiteNode>
{
    private readonly ITypedLogger<PrerequisiteNodeComponentRenderer> logger = logger;
    private readonly IConfigurationService configurationService = configurationService;
    private readonly IRendererFactory rendererFactory = rendererFactory;
    private readonly ITextureProvider textureProvider = textureProvider;
    private readonly IAttributeService attributeService = attributeService;
    private readonly IItemDataService itemDataService = itemDataService;
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
            tabIdxDefaultActive = node.CompletePrerequisiteTree
                .Index()
                .FirstOrNull(entry => entry.Item.IsActive)?.Index ?? -1;
        }

        prereqsDrawn.Add(node);
        var prereqCount = node.CompletePrerequisiteTree.Count;

        for (var i = 0; i < prereqCount; i++)
        {
            var (prereqNode, prereqIsActive) = node.CompletePrerequisiteTree[i];

            var (textColor, gameIcon) = uiTheme.GetCollectionStatusTheme(prereqNode.CollectionStatus);

            var tabSelected = prereqCount <= 1 || i == tabIdxDefaultActive;
            var flags = tabSelected
                ? ImGuiTabItemFlags.SetSelected
                : ImGuiTabItemFlags.None;

            var tabName = $"Source {i + 1} ({prereqNode.SourceType})";

            // dont look at this
            Func<ImRaii.IEndObject> tabActiveStatusFunc;
            if (prereqIsActive)
                tabActiveStatusFunc = ImRaii.Enabled;
            else
                tabActiveStatusFunc = ImRaii.Disabled;

            using (ImRaii.PushId(i))
            using (ImRaii.PushColor(ImGuiCol.Text, textColor))
            {
                using (ImRaii.PushStyle(ImGuiStyleVar.DisabledAlpha, 0.5f, !prereqIsActive))
                using (tabActiveStatusFunc())
                using (var tabItem = ImRaii.TabItem($"{tabName}##or_node_tab_item_{i}", flags))
                {
                    if (!prereqIsActive && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        using (ImRaii.Enabled())
                            UiComponents.SetSolidTooltip(string.Format(Resource.DisabledPrerequisiteTooltip, tabName));

                    if (!tabItem)
                        continue;
                }

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

        if (prereqCount <= 1)
            return;

        var toggleTabFlags = tabIdxDefaultActive == -1
            ? ImGuiTabItemFlags.SetSelected
            : ImGuiTabItemFlags.None;
        using (ImRaii.Enabled())
        using (var tabItem = ImRaii.TabItem($"{Resource.PrerequisiteOrNodeSettingsTabName}##or_node_tab_item", toggleTabFlags))
        {
            if (!tabItem)
                return;
            try
            {
                using (ImRaii.PushIndent(5f))
                {
                    foreach (var (idx, (prereqNode, prereqIsActive)) in node.CompletePrerequisiteTree.Index())
                    {
                        var active = prereqIsActive;
                        if (ImGui.Checkbox($"Include Source {idx + 1} ({prereqNode.SourceType})##or_node_toggle_option_{idx}", ref active))
                        {
                            actions.Add(() => node.SetPrerequisiteActiveStatus(prereqNode, !prereqIsActive));
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
            using (ImRaii.Disabled(!node.CollectLock))
            {
                if (drawPrerequisiteButton(node, parentCount))
                {
                    actions.Add(() => node.SetIsCollectedLocked(!node.IsCollected));
                }
            }
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

    private bool drawPrerequisiteButton(PrerequisiteAtomNode node, int count)
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
                y: ImGui.GetTextLineHeightWithSpacing() * 1.3f
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

            //if (textureProvider.GetFromGameIcon((int)collectionStatusTheme.Icon).TryGetWrap(out var texture, out var exception))
                //ImGui.Image(texture.Handle, collectionStatusButtonIconSize, collectionStatusTheme.TextColor);
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

                if (textureProvider.GetFromGameIcon((int)collectionStatusTheme.Icon).TryGetWrap(out var texture, out var exception))
                    ImGui.Image(texture.Handle, collectionStatusButtonIconSize, collectionStatusTheme.TextColor);
            }

            ImGui.SetCursorPos(buttonPos + new Vector2(iconXOffset + collectionStatusButtonSize.X + 1 * ImGuiHelpers.GlobalScale, iconYOffset));

            // ITEM ICON
            var iconId = itemDataService.GetItemIconId(node.ItemId);
            var iconHovered = false;
            if (textureProvider
                .GetFromGameIcon((uint) iconId)
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
