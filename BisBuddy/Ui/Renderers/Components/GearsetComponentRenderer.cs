using BisBuddy.Gear;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using BisBuddy.Resources;
using System.Numerics;
using Dalamud.Interface.Utility;
using BisBuddy.Services;
using Dalamud.Plugin.Services;
using System.Drawing;
using KamiToolKit.System;

namespace BisBuddy.Ui.Renderers.Components
{
    public class GearsetComponentRenderer(
        ITypedLogger<GearsetComponentRenderer> logger,
        ITextureProvider textureProvider,
        IRendererFactory rendererFactory,
        IConfigurationService configurationService
        ) : ComponentRendererBase<Gearset>
    {
        private readonly ITypedLogger<GearsetComponentRenderer> logger = logger;
        private readonly ITextureProvider textureProvider = textureProvider;
        private readonly IRendererFactory rendererFactory = rendererFactory;
        private readonly IConfigurationService configurationService = configurationService;
        private Gearset? gearset;

        private UiTheme uiTheme =>
            configurationService.UiTheme;

        private static float IconWidth =>
            ImGui.GetTextLineHeightWithSpacing() * 2f;

        public override void Initialize(Gearset renderableComponent) =>
            gearset ??= renderableComponent;

        public override void Draw()
        {
            if (gearset is null)
            {
                logger.Error("Attempted to draw uninitialized component renderer");
                return;
            }

            var oldWindowPadding = ImGui.GetStyle().WindowPadding;
            var windowYPadding = oldWindowPadding.Y;
            oldWindowPadding.Y = 0;
            var oldSpacing = ImGui.GetStyle().ItemSpacing;
            var noScroll = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

            using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
            using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.7f, !gearset.IsActive))
            using (ImRaii.Child("gearset_view_panel", new Vector2(0, 0), border: false, noScroll))
            using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, oldWindowPadding))
            {
                var headerHeight = ImGui.GetTextLineHeightWithSpacing() * 2.5f;
                using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
                using (ImRaii.Child("gearset_view_panel_header", new Vector2(0, headerHeight), border: false, noScroll))
                using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, oldSpacing))
                {
                    if (uiTheme.ShowGearsetColorAccentFlag)
                        DrawGearsetColorAccent(gearset);
                    DrawGearsetClassJobIcon(gearset);
                    DrawGearsetHeader(gearset);
                }
                ImGui.Separator();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() - oldSpacing.Y);
                UiComponents.PushTableClipRect();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + windowYPadding);
                try
                {
                    using (ImRaii.Child("gearset_view_panel_tabs", new Vector2(0, 0), border: false, ImGuiWindowFlags.AlwaysUseWindowPadding))
                    {
                        using var gearsetTabBar = ImRaii.TabBar("###gearset_tab");
                        if (!gearsetTabBar)
                            return;

                        using (var gearpieceTab = ImRaii.TabItem(Resource.GearsetGearpiecesTabName))
                        {
                            ImGui.Spacing();
                            if (gearpieceTab)
                            {
                                DrawGearpiecesTab(gearset);
                            }
                        }

                        using (var propertiesTab = ImRaii.TabItem(Resource.GearsetPropertiesTabName))
                        {
                            ImGui.Spacing();
                            if (propertiesTab)
                                DrawPropertiesTab(gearset);
                        }
                    }
                }
                finally
                {
                    ImGui.PopClipRect();
                }


                var botRight = ImGui.GetCursorPos();
                botRight.X += ImGui.GetContentRegionAvail().X;
            }
        }

        private void DrawGearsetColorAccent(Gearset gearset)
        {
            var minOffset = 100 * ImGuiHelpers.GlobalScale;

            var cursorPos = ImGui.GetCursorScreenPos();
            var rectPos = new Vector2(cursorPos.X + minOffset, cursorPos.Y);
            var rectSize = new Vector2(
                x: ImGui.GetContentRegionAvail().X - minOffset,
                y: ImGui.GetTextLineHeight() * 3f + ImGui.GetStyle().ItemSpacing.X * 3
                );
            var gearsetColor = gearset.HighlightColor?.BaseColor
                ?? configurationService.DefaultHighlightColor.BaseColor;

            // y = (x + c) / (1 + c)
            var alphaCoeff = 2 / 3f;
            gearsetColor.W = (gearsetColor.W + alphaCoeff) / (1 + alphaCoeff);
            var rectColor = ImGui.GetColorU32(gearsetColor);
            var emptyColor = ImGui.GetColorU32(new Vector4(gearsetColor.X, gearsetColor.Y, gearsetColor.Z, 0.0f));
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilledMultiColor(
                pMin: rectPos,
                pMax: rectPos + rectSize,
                colUprLeft: emptyColor,
                colUprRight: rectColor,
                colBotRight: emptyColor,
                colBotLeft: emptyColor
                );
        }

        private void DrawGearsetClassJobIcon(Gearset gearset)
        {
            var prevCursorPos = ImGui.GetCursorScreenPos();
            var cursorPos = ImGui.GetCursorScreenPos();
            var iconSize = new Vector2(IconWidth, IconWidth);
            var spacing = ImGui.GetStyle().ItemSpacing;
            var available = ImGui.GetContentRegionAvail();
            cursorPos.X += available.X - (iconSize.X + spacing.Y);
            cursorPos.Y += (available.Y - (iconSize.Y - spacing.Y)) / 2;

            var classJobInfo = gearset.ClassJobInfo;
            if (textureProvider.GetFromGameIcon(classJobInfo.IconId).TryGetWrap(out var texture, out var exception))
            {
                ImGui.SetCursorScreenPos(cursorPos);
                ImGui.Image(texture.Handle, iconSize);
                if (ImGui.IsItemHovered())
                    UiComponents.SetSolidTooltip(classJobInfo.Name);
                ImGui.SetCursorScreenPos(prevCursorPos);
            }
        }

        private void DrawGearsetHeader(Gearset gearset)
        {
            // gearset enabled checkbox
            var buttonPaddingSize = new Vector2(6f) * ImGuiHelpers.GlobalScale;
            var paddingSize = ImGui.GetStyle().FramePadding;
            var textHeight = ImGui.GetTextLineHeight();
            var textHeightSpacing = ImGui.GetTextLineHeightWithSpacing();
            var extraSpace = ImGui.GetContentRegionAvail().Y - (textHeight + buttonPaddingSize.Y * 2);

            var spacing = ImGui.GetStyle().ItemSpacing.X * ImGuiHelpers.GlobalScale;
            using var indent = ImRaii.PushIndent(extraSpace / 2, scaled: false);
            using var itemSpacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(extraSpace / 2));

            var textYOffset = (ImGui.GetContentRegionAvail().Y - (textHeight + paddingSize.Y * 2)) / 2;
            var checkboxYOffset = extraSpace / 2;

            var prevYPos = ImGui.GetCursorPosY();
            ImGui.SetCursorPosY(prevYPos + checkboxYOffset);
            var enabled = gearset.IsActive;

            var enabledColor = uiTheme.GetCollectionStatusTheme(CollectionStatusType.ObtainedComplete).TextColor;

            using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, buttonPaddingSize))
            using (ImRaii.PushColor(ImGuiCol.CheckMark, enabledColor, enabled))
                if (ImGui.Checkbox("###gearset_enable_checkbox", ref enabled))
                {
                    logger.Info($"{(enabled ? "Enabling" : "Disabling")} gearset \"{gearset.Name}\"");
                    gearset.IsActive = enabled;
                }
            if (ImGui.IsItemHovered())
                UiComponents.SetSolidTooltip(enabled
                    ? Resource.EnabledGearsetTooltip
                    : Resource.DisabledGearsetTooltip
                    );

            ImGui.SameLine();
            ImGui.SetCursorPosY(prevYPos + textYOffset);

            // gearset name input
            var gearsetName = gearset.Name;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - spacing - IconWidth);
            if (ImGui.InputText($"##gearset_name_input", ref gearsetName, 512))
            {
                logger.Debug($"Renaming gearset \"{gearset.Name}\" to \"{gearsetName}\"");
                gearset.Name = gearsetName;
            }
            if (ImGui.IsItemHovered())
                UiComponents.SetSolidTooltip(Resource.RenameGearsetTooltip);
            ImGui.SetCursorPos(ImGui.GetCursorPos() + new Vector2(0, spacing));
        }

        private void DrawPropertiesTab(Gearset gearset)
        {
            //if (gearset.SourceUrl != null)
            //{
            //    //ImGui.SameLine();
            //    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Copy, $"{Resource.GearsetUrlButton}##copy_gearset_url"))
            //    {
            //        ImGui.SetClipboardText(gearset.SourceUrl);
            //    }
            //    if (ImGui.IsItemHovered())
            //    {
            //        UiComponents.SetTooltipSolid(string.Format(Resource.GearsetUrlTooltip, gearset.SourceType));
            //    }
            //}
            //// don't support simultaneous source urls and strings for now
            //else if (gearset.SourceString != null)
            //{
            //    ImGui.SameLine();
            //    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Copy, $"{Resource.GearsetStringButton}##copy_gearset_string"))
            //    {
            //        ImGui.SetClipboardText(gearset.SourceString);
            //    }
            //    if (ImGui.IsItemHovered())
            //    {
            //        UiComponents.SetTooltipSolid(string.Format(Resource.GearsetStringTooltip, gearset.SourceType));
            //    }
            //}

            //ImGui.SameLine();

            //if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.FileExport, $"{Resource.GearsetJsonButton}##export_gearset_json"))
            //{
            //    //ImGui.SetClipboardText(gearsetsService.ExportGearsetToJsonStr(gearset));
            //}
            //if (ImGui.IsItemHovered())
            //{
            //    UiComponents.SetTooltipSolid(Resource.GearsetJsonTooltip);
            //}

            //ImGui.SameLine();

            //// DEFAULT COLOR CHECKBOX
            //var useDefaultColor = gearset.HighlightColor is null;
            //if (ImGui.Checkbox(Resource.GearsetDefaultColorCheckbox, ref useDefaultColor))
            //{
            //    if (useDefaultColor)
            //        gearset.HighlightColor = null;
            //    else
            //    {
            //        var newColor = new HighlightColor(configurationService.DefaultHighlightColor.BaseColor);
            //        gearset.HighlightColor = newColor;
            //    }
            //}
            //if (ImGui.IsItemHovered())
            //    UiComponents.SetTooltipSolid(Resource.GearsetDefaultColorTooltip);

            //ImGui.SameLine();

            // COLOR PICKER
            //using (ImRaii.Disabled(useDefaultColor))
            //using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(3.0f, 4.0f)))
            //{
            //    var existingColor = gearset.HighlightColor?.BaseColor ?? configurationService.DefaultHighlightColor.BaseColor;
            //    if (ImGui.ColorButton($"{Resource.GearsetHighlightColorButtonTooltip}###ColorPickerButton", existingColor))
            //        ImGui.OpenPopup($"###ColorPickerPopup");

            //    using (var popup = ImRaii.Popup($"###ColorPickerPopup"))
            //    {
            //        if (popup)
            //            if (ImGui.ColorPicker4(
            //                $"###ColorPicker",
            //                ref existingColor,
            //                (
            //                    ImGuiColorEditFlags.NoPicker
            //                    | ImGuiColorEditFlags.AlphaBar
            //                    | ImGuiColorEditFlags.NoSidePreview
            //                    | ImGuiColorEditFlags.DisplayRGB
            //                    | ImGuiColorEditFlags.NoBorder
            //                )))
            //                gearset.HighlightColor?.UpdateColor(existingColor);
            //    }
            //    ImGui.SameLine();
            //    ImGui.Text(Resource.GearsetHighlightColorLabel);
            //}
        }

        private void DrawGearpiecesTab(Gearset gearset)
        {
            if (gearset.Gearpieces.Count > 0)
            {
                var drawingLeftSide = (gearset.Gearpieces[0].GearpieceType & GearpieceType.LeftSide) != 0;
                var drawingRightSide = (gearset.Gearpieces[0].GearpieceType & GearpieceType.RightSide) != 0;

                for (var i = 0; i < gearset.Gearpieces.Count; i++)
                {
                    var gearpiece = gearset.Gearpieces[i];

                    if (!drawingLeftSide && (gearpiece.GearpieceType & GearpieceType.LeftSide) != 0)
                    {
                        ImGui.Spacing();
                        ImGui.Separator();
                        ImGui.Spacing();
                        drawingLeftSide = true;
                    }
                    if (!drawingRightSide && (gearpiece.GearpieceType & GearpieceType.RightSide) != 0)
                    {
                        ImGui.Spacing();
                        ImGui.Separator();
                        ImGui.Spacing();
                        drawingRightSide = true;
                    }

                    using (ImRaii.PushId(i))
                    {
                        rendererFactory
                            .GetRenderer(gearpiece, RendererType.Component)
                            .Draw();
                    }
                }
            }
        }
    }
}
