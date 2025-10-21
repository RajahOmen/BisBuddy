using BisBuddy.Gear;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using System.Linq;
using System.Numerics;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Gear.Prerequisites;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Utility;
using System.ComponentModel.DataAnnotations;
using BisBuddy.Items;
using BisBuddy.Gear.Melds;
using BisBuddy.Resources;
using Dalamud.Interface.Components;
using Dalamud.Interface.Style;
using System;

namespace BisBuddy.Ui.Renderers.Components
{
    public class GearpieceComponentRenderer(
        ITypedLogger<GearpieceComponentRenderer> logger,
        ITextureProvider textureProvider,
        IRendererFactory rendererFactory,
        IConfigurationService configurationService,
        IItemDataService itemDataService,
        IAttributeService attributeService,
        UiComponents uiComponents
        ) : ComponentRendererBase<Gearpiece>
    {
        private static readonly Vector4 ExpandedBackgroundColorMult = new(0.6f, 0.6f, 0.6f, 1.0f);
        private Vector4 ExpandedBackgroundColor => uiTheme.ButtonColor * ExpandedBackgroundColorMult;
        private static float CornerRound => 5.0f;

        private readonly ITypedLogger<GearpieceComponentRenderer> logger = logger;
        private readonly ITextureProvider textureProvider = textureProvider;
        private readonly IConfigurationService configurationService = configurationService;
        private readonly IRendererFactory rendererFactory = rendererFactory;
        private readonly IItemDataService itemDataService = itemDataService;
        private readonly IAttributeService attributeService = attributeService;
        private readonly UiComponents uiComponents = uiComponents;
        private Gearpiece? gearpiece;
        private bool isExpanded = false;
        private bool? nextIsExpanded = null;
        private bool prereqExpanded = defaultPrereqExpandedState(null);

        private UiTheme uiTheme =>
            configurationService.UiTheme;

        public override void Initialize(Gearpiece renderableComponent) =>
            gearpiece ??= renderableComponent;

        private static bool defaultPrereqExpandedState(Gearpiece? gearpiece)
        {
            if (gearpiece == null)
                return false;

            if (gearpiece.CollectionStatus >= CollectionStatusType.Obtainable)
                return false;

            return true;
        }

        public override void Draw()
        {
            isExpanded = nextIsExpanded ?? isExpanded;
            nextIsExpanded = null;

            if (gearpiece is null)
            {
                logger.Error("Attempted to draw uninitialized component renderer");
                return;
            }

            var gearpieceCollected = gearpiece.IsCollected;
            var gearpieceManuallyCollected = gearpiece.CollectLock;
            var gearpieceNeedsMelds = gearpiece.ItemMateria.Any(m => !m.IsCollected);

            var (textColor, gameIcon) = uiTheme.GetCollectionStatusTheme(gearpiece.CollectionStatus);

            var hasSubItems = gearpiece.ItemMateria.Any() || gearpiece.PrerequisiteTree != null;
            var startPos = ImGui.GetCursorScreenPos();

            var windowDrawList = ImGui.GetWindowDrawList();
            var splitter = ImGui.ImDrawListSplitter();
            splitter.Split(windowDrawList, 3);
            splitter.SetCurrentChannel(windowDrawList, 2);
            var topLeftPos = ImGui.GetCursorScreenPos();

            var outlineThickness = 3f;
            var thickOffset = (float)Math.Floor(outlineThickness / 2) - 0.1f;
            var internalThickOffset = outlineThickness - thickOffset;

            using (ImRaii.PushColor(ImGuiCol.Text, textColor))
            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0)))
            {
                drawGearpieceButton();
                if (isExpanded && hasSubItems)
                {
                    var contentRegion = ImGui.GetContentRegionAvail();
                    var currentPos = ImGui.GetCursorPos();
                    var itemSpacing = ImGui.GetStyle().ItemSpacing;
                    var padding = itemSpacing.X + internalThickOffset;
                    var padSize = new Vector2(padding, padding);
                    var tableSize = new Vector2(contentRegion.X - padding * 2, 0);
                    ImGui.SetCursorPosX(currentPos.X + padding);
                    ImGui.SetCursorPosY(currentPos.Y + itemSpacing.Y);
                    using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1.0f, 1.0f, 1.0f, 1.0f)))
                    using (ImRaii.PushStyle(ImGuiStyleVar.CellPadding, itemSpacing))
                    using (var table = ImRaii.Table("###gearpiece_details_table", 1, ImGuiTableFlags.None, tableSize))
                    {
                        if (table)
                        {
                            if (gearpiece.ItemMateria.Count > 0)
                            {
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                drawMateriaGroup();
                            }
                            if (gearpiece.PrerequisiteTree != null)
                            {
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                drawPrerequisites(windowDrawList, splitter);
                            }
                            ImGui.Spacing();
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + internalThickOffset);
                        }
                    }

                }
            }

            if (isExpanded && hasSubItems)
            {
                var outerThicknessOffset = new Vector2(thickOffset, thickOffset);
                var botRightPos = ImGui.GetCursorScreenPos()
                    + new Vector2(ImGui.GetContentRegionAvail().X, -ImGui.GetStyle().ItemSpacing.Y * 2);
                botRightPos.X -= thickOffset;
                topLeftPos += outerThicknessOffset;
                splitter.SetCurrentChannel(windowDrawList, 0);
                var col = ImGui.GetColorU32(uiTheme.ButtonColor);
                windowDrawList.AddRect(topLeftPos, botRightPos, col, CornerRound, ImDrawFlags.Closed, outlineThickness);
            }

            splitter.Merge(windowDrawList);
        }

        private void drawGearpieceButton()
        {
            if (gearpiece is null)
            {
                logger.Error("Attempted to draw uninitialized component renderer");
                return;
            }

            var hasSubItems = gearpiece.ItemMateria.Count > 0 || gearpiece.PrerequisiteTree != null;
            using (ImRaii.PushStyle(ImGuiStyleVar.DisabledAlpha, 1.0f))
            using (ImRaii.Disabled(!hasSubItems))
            using (ImRaii.PushColor(ImGuiCol.Button, uiTheme.ButtonColor))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, uiTheme.ButtonHovered))
            using (ImRaii.PushColor(ImGuiCol.ButtonActive, uiTheme.ButtonActive))
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
                var buttonText = $"{gearpiece.ItemName}";
                var collectionStatusButtonSize = new Vector2(buttonSize.Y, buttonSize.Y);
                var collectionStatusTheme = uiTheme.GetCollectionStatusTheme(gearpiece.CollectionStatus);
                var materiaSize = 16 * ImGuiHelpers.GlobalScale;
                var materiaSpacing = 3 * ImGuiHelpers.GlobalScale;
                var materiaXOffset = gearpiece.ItemMateria.Count * (materiaSize + materiaSpacing) + materiaSpacing;

                using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0), isExpanded))
                    if (ImGui.Button($"###{gearpiece.ItemId}gearpiece_expand_button", buttonSize))
                    {
                        nextIsExpanded = !isExpanded;
                        if (nextIsExpanded is true)
                        {
                            prereqExpanded = defaultPrereqExpandedState(gearpiece);
                        }
                    }
                var mainButtonHovered = ImGui.IsItemHovered();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    if (gearpiece.ItemMateria.Count > 0 || gearpiece.PrerequisiteTree is not null)
                        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    else
                        UiComponents.SetSolidTooltip(
                            Resource.GearpieceNoSubItemsTooltip
                            );
                }
                using (ImRaii.Enabled())
                {
                    rendererFactory
                        .GetRenderer(gearpiece, RendererType.ContextMenu)
                        .Draw();
                }


                var nextPos = ImGui.GetCursorPos();

                var textOffset = new Vector2(
                    x: collectionStatusButtonSize.X * 2 + 2 * ImGuiHelpers.GlobalScale,
                    y: (buttonSize.Y - ImGui.GetTextLineHeight()) / 2
                    );
                var textPos = buttonPos + textOffset;
                var botRightClipRect = buttonScreenPos + new Vector2(buttonSize.X - (materiaXOffset + materiaSpacing * 1), buttonSize.Y);
                ImGui.SetCursorPos(textPos);
                ImGui.PushClipRect(buttonScreenPos, botRightClipRect, true);
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
                using (ImRaii.PushColor(ImGuiCol.Button, statusButtonColor))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, statusButtonColor))
                using (ImRaii.PushColor(ImGuiCol.ButtonActive, statusButtonColor))
                using (ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, Vector2.One * 0.5f))
                {
                    if (ImGui.Button("##gearpiece_collection_status_button", collectionStatusButtonSize))
                        ImGui.OpenPopup("##gearpiece_context_menu");
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                        var collectionStatusDesc = attributeService
                            .GetEnumAttribute<DisplayAttribute>(gearpiece.CollectionStatus)!
                            .GetDescription();

                        var tooltip = gearpiece.CollectLock
                            ? string.Format(Resource.CollectionStatusLockedTooltipPrefix, collectionStatusDesc)
                            : collectionStatusDesc;

                        UiComponents.SetSolidTooltip(tooltip);
                    }
                }

                var ratio = 0.75f;
                var collectionStatusButtonIconSize = collectionStatusButtonSize * ratio;
                var iconXOffset = collectionStatusButtonSize.X * (1 - ratio) / 2;
                var iconYOffset = collectionStatusButtonSize.Y * (1 - ratio) / 2;
                // draw lock icon
                if (gearpiece.CollectLock)
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

                // UI CATEGORY ICON
                var uiCategory = itemDataService.GetItemUICategory(gearpiece.ItemId);
                if (textureProvider
                    .GetFromGameIcon(uiCategory.Icon)
                    .TryGetWrap(out var uiCategoryTexture, out var uiCategoryException)
                    )
                {
                    using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.65f, mainButtonHovered))
                    {
                        ImGui.Image(uiCategoryTexture.Handle, collectionStatusButtonIconSize);
                    }
                    if (ImGui.IsItemHovered())
                        using (ImRaii.PushColor(ImGuiCol.Text, Vector4.One))
                        {
                            // TODO: FIX THIS
                            UiComponents.SetSolidTooltip(uiCategory.Name.ExtractText());
                        }
                }

                // MATERIA
                var materiaYOffset = (buttonSize.Y - materiaSize) / 2;
                var materiaStartPos = new Vector2(
                    buttonPos.X + buttonSize.X - materiaXOffset,
                    buttonPos.Y + materiaYOffset
                    );

                ImGui.SetCursorPos(materiaStartPos);

                var materiaSlotCount = itemDataService.GetItemMateriaSlotCount(gearpiece.ItemId);
                foreach (var (idx, materia) in gearpiece.ItemMateria.Index())
                {
                    using (ImRaii.PushId(idx))
                    {
                        ImGui.SetCursorPosX(materiaStartPos.X + idx * (materiaSize + materiaSpacing));
                        ImGui.SetCursorPosY(materiaStartPos.Y);
                        var isAdvancedMelding = idx >= materiaSlotCount;
                        uiComponents.MateriaSlot(
                            materiaSize,
                            isAdvancedMelding,
                            materia.CollectionStatus,
                            materia.StatStrength
                            );
                    }
                }

                ImGui.SetCursorPos(nextPos);
            }
        }

        private void drawMateriaGroup()
        {
            if (gearpiece is null)
            {
                logger.Error("Attempted to draw uninitialized component renderer");
                return;
            }

            var childPadding = 10f;
            var materiaSpacing = 5f;

            var oldSpacing = ImGui.GetStyle().ItemSpacing;
            using var childSpacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(materiaSpacing, oldSpacing.Y));

            var windowWidth = ImGui.GetContentRegionAvail().X;

            var childHeight = ImGui.GetTextLineHeight() + ImGui.GetStyle().FramePadding.Y * 2f;
            using var smallerScrollbar = ImRaii.PushStyle(ImGuiStyleVar.ScrollbarSize, childPadding * 1.5f);

            var itemMateriaWidth = getItemMateriaWidth(gearpiece.ItemMateria, childPadding);
            var extraScrollbarHeight = itemMateriaWidth > windowWidth
                ? ImGui.GetStyle().ScrollbarSize - childPadding : 0;

            var totalChildHeight = childHeight + childPadding * 2 + extraScrollbarHeight;
            using (ImRaii.PushColor(ImGuiCol.ChildBg, uiTheme.ButtonColor))
            using (ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, CornerRound))
            using (
                ImRaii.Child(
                    "gearpiece_materia_child",
                    new Vector2(0, totalChildHeight),
                    false,
                    ImGuiWindowFlags.HorizontalScrollbar
                    )
                )
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + childPadding);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + childPadding);
                rendererFactory
                    .GetRenderer(gearpiece.ItemMateria, RendererType.Component)
                    .Draw();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + childPadding - materiaSpacing);
            }
        }

        private static float getItemMateriaWidth(MateriaGroup itemMateria, float sidePadding)
        {
            var horizontalSpacing = ImGui.GetStyle().ItemSpacing.X;
            var buttonPadding = ImGui.GetStyle().FramePadding.X;
            var textWidth = sidePadding * 2 - horizontalSpacing;
            foreach (var group in itemMateria.StatusGroups)
            {
                var groupWidth =
                    ImGui.CalcTextSize($"x{group.Count} {group.Type.StatStrength}").X
                    + buttonPadding * 2
                    + horizontalSpacing
                    ;
                textWidth += groupWidth;
            }

            return textWidth;
        }

        private void drawPrerequisites(ImDrawListPtr drawList, ImDrawListSplitterPtr splitter)
        {
            if (gearpiece?.PrerequisiteTree is IPrerequisiteNode node)
            {
                Vector4 prereqButtonTextColor;
                if (gearpiece.IsCollected)
                    prereqButtonTextColor = uiTheme.GetCollectionStatusTheme(CollectionStatusType.ObtainedComplete).TextColor;
                else
                    prereqButtonTextColor = uiTheme.GetCollectionStatusTheme(node.CollectionStatus).TextColor;

                var buttonSize = new Vector2(
                    x: ImGui.GetContentRegionAvail().X / ImGuiHelpers.GlobalScale, 0
                    );
                var icon = prereqExpanded
                    ? FontAwesomeIcon.CaretDown
                    : FontAwesomeIcon.CaretRight;
                var topLeft = ImGui.GetCursorScreenPos();
                using (ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(0, 0.5f)))
                using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, CornerRound))
                using (ImRaii.PushStyle(ImGuiStyleVar.TabRounding, CornerRound))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, uiTheme.ButtonHovered))
                using (ImRaii.PushColor(ImGuiCol.ButtonActive, uiTheme.ButtonActive))
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, prereqButtonTextColor))
                    using (ImRaii.PushColor(ImGuiCol.Button, uiTheme.ButtonColor))
                        if (ImGuiComponents.IconButtonWithText(icon, Resource.PrerequisiteGearpieceHeader, buttonSize))
                            prereqExpanded = !prereqExpanded;
                    if (prereqExpanded)
                    {
                        ImGui.Spacing();

                        var isCollected = gearpiece?.IsCollected ?? false;
                        var disabledAlpha = isCollected
                            ? 0.5f : 1.0f;
                        var cellPaddingX = ImGui.GetStyle().CellPadding.X;
                        using (ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(cellPaddingX, 0)))
                        using (ImRaii.PushStyle(ImGuiStyleVar.DisabledAlpha, disabledAlpha))
                        using (ImRaii.Disabled(isCollected))
                        {
                            // table for adding left and right padding
                            using var table = ImRaii.Table("###gearpiece_prerequisite_tabledd", 1, ImGuiTableFlags.PadOuterX);

                            if (!table)
                                return;

                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            rendererFactory
                                .GetRenderer(node, RendererType.Component)
                                .Draw();
                            ImGui.Spacing();
                        }

                    }
                }

                if (prereqExpanded)
                {
                    var botRight = ImGui.GetCursorScreenPos();
                    var contentRegion = ImGui.GetContentRegionAvail();
                    var pos = ImGui.GetCursorPos();
                    botRight.X += contentRegion.X;
                    splitter.SetCurrentChannel(drawList, 1);
                    drawList.AddRectFilled(topLeft, botRight, ImGui.GetColorU32(ExpandedBackgroundColor), CornerRound);
                    splitter.SetCurrentChannel(drawList, 2);
                    ImGui.SetCursorPosY(pos.Y + ImGui.GetStyle().ItemSpacing.Y);
                }
            }
        }
    }
}
