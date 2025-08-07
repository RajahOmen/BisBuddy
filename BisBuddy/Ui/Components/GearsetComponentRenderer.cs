using BisBuddy.Gear;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FFXIVClientStructs.FFXIV.Client.Game.InstanceContent.DynamicEvent.Delegates;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureGearsetModule.Delegates;
using BisBuddy.Resources;
using System.Numerics;
using Dalamud.Interface.Utility;
using BisBuddy.Util;
using BisBuddy.Services;
using Dalamud.Plugin.Services;

namespace BisBuddy.Ui.Components
{
    public class GearsetComponentRenderer(
        ITypedLogger<GearsetComponentRenderer> logger,
        ITextureProvider textureProvider,
        IComponentRendererFactory componentRendererFactory,
        IConfigurationService configurationService
        ) : IComponentRenderer<Gearset>
    {
        private readonly ITypedLogger<GearsetComponentRenderer> logger = logger;
        private readonly ITextureProvider textureProvider = textureProvider;
        private readonly IComponentRendererFactory componentRendererFactory = componentRendererFactory;
        private readonly IConfigurationService configurationService = configurationService;
        private Gearset? gearset;

        private UiTheme uiTheme =>
            configurationService.UiTheme;

        private static float IconWidth =>
            ImGui.GetTextLineHeightWithSpacing() * 2f;

        public void Initialize(Gearset renderableComponent) =>
            gearset ??= renderableComponent;

        public void Draw()
        {
            if (gearset is null)
            {
                logger.Error("Attempted to draw uninitialized component renderer");
                return;
            }

            var oldWindowPadding = ImGui.GetStyle().WindowPadding;
            var noScroll = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

            using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
            using (ImRaii.Child("gearset_view_panel", new Vector2(0, 0), border: true, noScroll))
            {
                var headerHeight = ImGui.GetTextLineHeight() * 4f;
                using (ImRaii.Child("gearset_view_panel_header", new Vector2(0, headerHeight), border: false, noScroll))
                {
                    if (uiTheme.ShowGearsetColorAccentFlag)
                        DrawGearsetColorAccent(gearset);
                    DrawGearsetClassJobIcon(gearset);
                    DrawGearsetHeader(gearset);
                }

                using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, oldWindowPadding))
                using (ImRaii.Child("gearset_view_panel_tabs", new Vector2(0, 0), border: true))
                {
                    using var gearsetTabBar = ImRaii.TabBar("###gearset_tab");
                    if (!gearsetTabBar)
                        return;

                    // TODO: LOCALIZATION!
                    using (var gearpieceTab = ImRaii.TabItem("Gearpieces"))
                    {
                        if (gearpieceTab)
                            DrawGearpiecesTab(gearset);
                    }

                    using (var propertiesTab = ImRaii.TabItem("Properties"))
                    {
                        if (propertiesTab)
                            DrawPropertiesTab(gearset);
                    }
                }
            }
        }

        private void DrawGearsetColorAccent(Gearset gearset)
        {
            var minOffset = 100 * ImGuiHelpers.GlobalScale;

            var cursorPos = ImGui.GetCursorScreenPos();
            var rectPos = new Vector2(cursorPos.X + minOffset, cursorPos.Y);
            var rectSize = new Vector2(
                x: ImGui.GetContentRegionAvail().X - minOffset,
                y: ImGui.GetTextLineHeight() * 2.5f + ImGui.GetStyle().ItemSpacing.X * 3
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
                p_min: rectPos,
                p_max: rectPos + rectSize,
                col_upr_left: emptyColor,
                col_upr_right: rectColor,
                col_bot_right: emptyColor,
                col_bot_left: emptyColor
                );
        }

        private void DrawGearsetClassJobIcon(Gearset gearset)
        {
            //var prevCursorPos = ImGui.GetCursorScreenPos();
            var cursorPos = ImGui.GetCursorScreenPos();
            var iconSize = new Vector2(IconWidth, IconWidth);
            var spacing = ImGui.GetStyle().ItemSpacing;
            var available = ImGui.GetContentRegionAvail();
            cursorPos.X += available.X - iconSize.X - spacing.Y;
            cursorPos.Y += (available.Y - iconSize.Y) / 2;

            var drawList = ImGui.GetWindowDrawList();

            //ImGui.SetCursorScreenPos(cursorPos);
            var classJobInfo = gearset.ClassJobInfo;
            if (textureProvider.GetFromGameIcon(classJobInfo.IconId).TryGetWrap(out var texture, out var exception))
            {
                using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.8f, !gearset.IsActive))
                    drawList.AddImage(texture.ImGuiHandle, cursorPos, cursorPos + iconSize);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(classJobInfo.Name);
            }
        }

        private void DrawGearsetHeader(Gearset gearset)
        {
            var spacing = ImGui.GetStyle().ItemSpacing.X * 2;
            using var indent = ImRaii.PushIndent(spacing);
            // gearset enabled checkbox
            ImGui.SetCursorPos(ImGui.GetCursorPos() + new Vector2(0, spacing));
            var enabled = gearset.IsActive;
            if (ImGui.Checkbox("###gearset_enable_checkbox", ref enabled))
            {
                logger.Debug($"{(enabled ? "Enabling" : "Disabling")} gearset \"{gearset.Name}\"");
                gearset.IsActive = enabled;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(enabled
                    ? Resource.EnabledGearsetTooltip
                    : Resource.DisabledGearsetTooltip
                    );

            ImGui.SameLine();

            // gearset name input
            var gearsetName = gearset.Name;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - spacing - IconWidth);
            if (ImGui.InputText($"##gearset_name_input", ref gearsetName, 512))
            {
                logger.Debug($"Renaming gearset \"{gearset.Name}\" to \"{gearsetName}\"");
                gearset.Name = gearsetName;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Resource.RenameGearsetTooltip);
            ImGui.SetCursorPos(ImGui.GetCursorPos() + new Vector2(0, spacing));

            //ImGui.PopClipRect();
        }

        private void DrawPropertiesTab(Gearset gearset)
        {
            
        }

        private void DrawGearpiecesTab(Gearset gearset)
        {
            var nameSize = new Vector2(400, 0) * ImGuiHelpers.GlobalScale;

            //using var table = ImRaii.Table("###gearpiece_table", 2, ImGuiTableFlags.BordersInnerV);
            //if (!table)
            //    return;

            //ImGui.TableSetupColumn("###names", ImGuiTableColumnFlags.WidthFixed, 0);
            //ImGui.TableSetupColumn("###details", ImGuiTableColumnFlags.WidthStretch, 0);

            //ImGui.TableNextColumn();

            //using (ImRaii.Child("###gearpiece_names", nameSize))
            //{
            //    foreach (var gearpiece in gearset.Gearpieces)
            //    {
            //        if (ImGui.Selectable(gearpiece.ItemName))
            //        {
            //            selectedGearpiece = gearpiece;
            //        }
            //    }
            //}

            //ImGui.TableNextColumn();

            //using (ImRaii.Child("###selected_gearpiece", new Vector2(0, 0)))
            //{
            //    if (selectedGearpiece != null)
            //    {
            //        GearpieceComponent.DrawGearpiece(selectedGearpiece);
            //    }
            //    else
            //    {
            //        ImGui.Text("Select a gearpiece to view its details.");
            //    }
            //}

            //ImGui.Spacing();

            //if (gearset.SourceUrl != null)
            //{
            //    //ImGui.SameLine();
            //    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Copy, $"{Resource.GearsetUrlButton}##copy_gearset_url"))
            //    {
            //        ImGui.SetClipboardText(gearset.SourceUrl);
            //    }
            //    if (ImGui.IsItemHovered())
            //    {
            //        ImGui.SetTooltip(string.Format(Resource.GearsetUrlTooltip, gearset.SourceType));
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
            //        ImGui.SetTooltip(string.Format(Resource.GearsetStringTooltip, gearset.SourceType));
            //    }
            //}

            //ImGui.SameLine();

            //if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.FileExport, $"{Resource.GearsetJsonButton}##export_gearset_json"))
            //{
            //    //ImGui.SetClipboardText(gearsetsService.ExportGearsetToJsonStr(gearset));
            //}
            //if (ImGui.IsItemHovered())
            //{
            //    ImGui.SetTooltip(Resource.GearsetJsonTooltip);
            //}

            //ImGui.SameLine();

            // DEFAULT COLOR CHECKBOX
            //var useDefaultColor = gearset.HighlightColor is null;
            //if (ImGui.Checkbox(Resource.GearsetDefaultColorCheckbox, ref useDefaultColor))
            //{
            //    if (useDefaultColor)
            //        gearset.HighlightColor = null;
            //    else
            //        gearset.HighlightColor = configurationService.DefaultHighlightColor;
            //}
            //if (ImGui.IsItemHovered())
            //    ImGui.SetTooltip(Resource.GearsetDefaultColorTooltip);

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

            //ImGui.Spacing();
            //ImGui.Separator();
            ImGui.Spacing();

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
                        ImGui.Spacing();
                        drawingLeftSide = true;
                    }
                    if (!drawingRightSide && (gearpiece.GearpieceType & GearpieceType.RightSide) != 0)
                    {
                        ImGui.Spacing();
                        ImGui.Spacing();
                        drawingRightSide = true;
                    }

                    using (ImRaii.PushId(i))
                    {
                        componentRendererFactory
                            .GetComponentRenderer(gearpiece)
                            .Draw();
                    }
                }
            }
        }
    }
}
