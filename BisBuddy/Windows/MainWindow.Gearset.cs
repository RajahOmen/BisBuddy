using BisBuddy.Gear;
using BisBuddy.Resources;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System.Linq;
using System.Numerics;

namespace BisBuddy.Windows
{
    public partial class MainWindow
    {
        private Gearset? gearsetHovered = null;

        private static Vector4 ManuallyCollectedColor = new(0.0f, 1.0f, 0.0f, 1.0f);
        private static Vector4 AutomaticallyCollectedColor = new(1.0f, 1.0f, 1.0f, 1.0f);

        private void updateGearsetHovered(Gearset hovererdGearset)
        {
            if (gearsetHovered == null || gearsetHovered != hovererdGearset)
            {
                gearsetHovered = hovererdGearset;
            }
        }

        private bool drawGearset(Gearset gearset)
        {
            var deleteGearset = false;
            var isActive = gearset.IsActive;

            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(4.0f, 4.0f)))
            {
                if (ImGui.Checkbox($"##gearset_active", ref isActive))
                {
                    gearset.SetActiveStatus(isActive);
                    logger.Debug($"{(isActive ? "enabled" : "disabled")} gearset \"{gearset.Name}\"");
                    gearsetsService.ScheduleUpdateFromInventory();
                }

                if (ImGui.IsItemHovered())
                {
                    updateGearsetHovered(gearset);
                    var tooltip = isActive
                        ? Resource.EnabledGearsetTooltip
                        : Resource.DisabledGearsetTooltip;
                    ImGui.SetTooltip(tooltip);
                }

                ImGui.SameLine();

                var shiftHeld = ImGui.IsKeyDown(ImGuiKey.LeftShift) || ImGui.IsKeyDown(ImGuiKey.RightShift);
                var deleteTooltipText = shiftHeld
                    ? Resource.DeleteGearsetTooltipQuick
                    : Resource.DeleteGearsetTooltip;
                var lightRed = new Vector4(0.5f, 0.2f, 0.2f, 1.0f);
                var darkRed = new Vector4(0.6f, 0.15f, 0.15f, 1.0f);

                using (ImRaii.PushColor(ImGuiCol.Button, shiftHeld ? darkRed : lightRed))
                {
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.TrashAlt))
                    {
                        if (shiftHeld)
                        {
                            deleteGearset = true;
                        }
                        else
                        {
                            ImGui.OpenPopup($"{Resource.DeleteGearsetPopupTitle}##delete_gearset_icon_confirm_popup");
                        }
                    }
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(deleteTooltipText);

                using (var deletePopup = ImRaii.Popup($"{Resource.DeleteGearsetPopupTitle}##delete_gearset_icon_confirm_popup"))
                {
                    if (deletePopup)
                    {
                        if (ImGui.Button($"{Resource.DeleteGearsetConfirmButton}###confirm_delete_icon_gearset_button"))
                        {
                            deleteGearset = true;
                            ImGui.CloseCurrentPopup();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip(Resource.DeleteGearsetConfirmTooltip);
                    }
                }

                ImGui.SameLine();
            }

            if (ImGui.CollapsingHeader($"[{gearset.JobAbbrv}] {gearset.Name}###gearset_collapsingheader"))
            {
                ImGui.Spacing();

                var isAllCollected = gearset.Gearpieces.All(g => g.IsCollected);
                var isAllManuallyCollected = isAllCollected & gearset.Gearpieces.All(g => g.IsManuallyCollected);

                var checkboxColor = isAllManuallyCollected
                    ? ManuallyCollectedColor
                    : AutomaticallyCollectedColor;

                using (ImRaii.PushColor(ImGuiCol.CheckMark, checkboxColor))
                using (ImRaii.Disabled(!gearset.IsActive))
                {
                    if (ImGui.Checkbox("###collect_all_gearpieces", ref isAllCollected))
                    {
                        foreach (var gearpiece in gearset.Gearpieces)
                            gearpiece.SetCollected(isAllCollected, true);
                    }
                }
                if (ImGui.IsItemHovered())
                {
                    var tooltip = isAllCollected
                        ? Resource.AllCollectedTooltip
                        : Resource.AllUncollectedTooltip;
                    ImGui.SetTooltip(tooltip);
                }

                ImGui.SameLine();

                var gearsetName = gearset.Name;
                var copyButtonWidth = ImGuiComponents.GetIconButtonWithTextWidth(FontAwesomeIcon.Copy, Resource.GearsetUrlButton);
                var exportButtonWidth = ImGuiComponents.GetIconButtonWithTextWidth(FontAwesomeIcon.FileExport, Resource.GearsetJsonButton);
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - ImGui.GetStyle().FramePadding.X);
                if (ImGui.InputText($"##gearset_rename_input", ref gearsetName, 512))
                    gearset.SetName(gearsetName);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Resource.RenameGearsetTooltip);

                ImGui.Spacing();
                ImGui.Indent();

                if (gearset.SourceUrl != null)
                {
                    //ImGui.SameLine();
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Copy, $"{Resource.GearsetUrlButton}##copy_gearset_url"))
                    {
                        ImGui.SetClipboardText(gearset.SourceUrl);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(string.Format(Resource.GearsetUrlTooltip, gearset.SourceType));
                    }
                }
                // don't support simultaneous source urls and strings for now
                else if (gearset.SourceString != null)
                {
                    ImGui.SameLine();
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Copy, $"{Resource.GearsetStringButton}##copy_gearset_string"))
                    {
                        ImGui.SetClipboardText(gearset.SourceString);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(string.Format(Resource.GearsetStringTooltip, gearset.SourceType));
                    }
                }

                ImGui.SameLine();

                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.FileExport, $"{Resource.GearsetJsonButton}##export_gearset_json"))
                {
                    ImGui.SetClipboardText(gearsetsService.ExportGearsetToJsonStr(gearset));
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(Resource.GearsetJsonTooltip);
                }

                ImGui.SameLine();

                // DEFAULT COLOR CHECKBOX
                var useDefaultColor = gearset.HighlightColor is null;
                if (ImGui.Checkbox(Resource.GearsetDefaultColorCheckbox, ref useDefaultColor))
                {
                    if (useDefaultColor)
                        gearset.HighlightColor = null;
                    else
                        gearset.HighlightColor = configurationService.DefaultHighlightColor;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Resource.GearsetDefaultColorTooltip);

                ImGui.SameLine();

                // COLOR PICKER
                using (ImRaii.Disabled(useDefaultColor))
                using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(3.0f, 4.0f)))
                {
                    var existingColor = gearset.HighlightColor?.BaseColor ?? configurationService.DefaultHighlightColor.BaseColor;
                    if (ImGui.ColorButton($"{Resource.GearsetHighlightColorButtonTooltip}###ColorPickerButton", existingColor))
                        ImGui.OpenPopup($"###ColorPickerPopup");

                    using (var popup = ImRaii.Popup($"###ColorPickerPopup"))
                    {
                        if (popup)
                            if (ImGui.ColorPicker4(
                                $"###ColorPicker",
                                ref existingColor,
                                (
                                    ImGuiColorEditFlags.NoPicker
                                    | ImGuiColorEditFlags.AlphaBar
                                    | ImGuiColorEditFlags.NoSidePreview
                                    | ImGuiColorEditFlags.DisplayRGB
                                    | ImGuiColorEditFlags.NoBorder
                                )))
                                gearset.HighlightColor?.UpdateColor(existingColor);
                    }
                    ImGui.SameLine();
                    ImGui.Text(Resource.GearsetHighlightColorLabel);
                }

                ImGui.Unindent();
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                if (gearset.Gearpieces.Count > 0)
                {
                    var drawingLeftSide = (gearset.Gearpieces[0].GearpieceType & GearpieceType.LeftSide) != 0;
                    var drawingRightSide = (gearset.Gearpieces[0].GearpieceType & GearpieceType.RightSide) != 0;

                    using (ImRaii.PushIndent(20.0f))
                    using (ImRaii.Disabled(!gearset.IsActive))
                    {
                        for (var i = 0; i < gearset.Gearpieces.Count; i++)
                        {
                            var item = gearset.Gearpieces[i];

                            if (!drawingLeftSide && (item.GearpieceType & GearpieceType.LeftSide) != 0)
                            {
                                ImGui.Spacing();
                                drawingLeftSide = true;
                            }
                            if (!drawingRightSide && (item.GearpieceType & GearpieceType.RightSide) != 0)
                            {
                                ImGui.Spacing();
                                drawingRightSide = true;
                            }

                            using (ImRaii.PushId(i))
                            {
                                drawGearpiece(item, gearset);
                            }
                        }
                    }
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }
            if (ImGui.IsItemHovered())
            {
                updateGearsetHovered(gearset);
            }
            return deleteGearset;
        }
    }
}
