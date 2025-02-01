using BisBuddy.Gear;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
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
                    gearset.IsActive = isActive;
                    Services.Log.Debug($"{(isActive ? "enabled" : "disabled")} gearset \"{gearset.Name}\"");
                    plugin.SaveGearsetsWithUpdate();
                }

                if (ImGui.IsItemHovered())
                {
                    updateGearsetHovered(gearset);
                    var tooltip = isActive ? "Disable Gearset" : "Enable Gearset";
                    ImGui.SetTooltip(tooltip);
                }

                ImGui.SameLine();

                var shiftHeld = ImGui.IsKeyDown(ImGuiKey.LeftShift) || ImGui.IsKeyDown(ImGuiKey.RightShift);
                var deleteTooltipText = shiftHeld ? "Click to quick delete" : "Shift+Click to quick delete";
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
                            ImGui.OpenPopup("Delete Gearset?##delete_gearset_icon_confirm_popup");
                        }
                    }
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Delete this Gearset. {deleteTooltipText}");

                using var deletePopup = ImRaii.Popup("Delete Gearset?##delete_gearset_icon_confirm_popup");
                if (deletePopup)
                {
                    if (ImGui.Button("Yes, Delete###confirm_delete_icon_gearset_button"))
                    {
                        deleteGearset = true;
                        ImGui.CloseCurrentPopup();
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("This cannot be undone!");
                }

                ImGui.SameLine();
            }

            if (ImGui.CollapsingHeader($"[{gearset.JobAbbrv}] {gearset.Name}###gearset_collapsingheader"))
            {
                ImGui.Spacing();

                var isAllCollected = gearset.Gearpieces.TrueForAll(g => g.IsCollected);
                var isAllManuallyCollected = isAllCollected & gearset.Gearpieces.TrueForAll(g => g.IsManuallyCollected);

                var checkboxColor = isAllManuallyCollected
                    ? ManuallyCollectedColor
                    : AutomaticallyCollectedColor;

                using (ImRaii.PushColor(ImGuiCol.CheckMark, checkboxColor))
                using (ImRaii.Disabled(!gearset.IsActive))
                {
                    if (ImGui.Checkbox("###collect_all_gearpieces", ref isAllCollected))
                    {
                        foreach (var gearpiece in gearset.Gearpieces) gearpiece.SetCollected(isAllCollected, true);
                        plugin.SaveGearsetsWithUpdate();
                    }
                }
                if (ImGui.IsItemHovered())
                {
                    var tooltip = isAllCollected
                        ? "Mark all as not Collected"
                        : "Lock all as Collected";
                    ImGui.SetTooltip(tooltip);
                }

                ImGui.SameLine();

                var gearsetName = gearset.Name;
                var copyButtonWidth = ImGuiComponents.GetIconButtonWithTextWidth(FontAwesomeIcon.Copy, "Link");
                var exportButtonWidth = ImGuiComponents.GetIconButtonWithTextWidth(FontAwesomeIcon.FileExport, "JSON");
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - (copyButtonWidth + exportButtonWidth + 17));
                if (ImGui.InputText($"##gearset_rename_input", ref gearsetName, 512))
                {
                    gearset.Name = gearsetName;
                    plugin.Configuration.Save();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Rename Gearset");

                ImGui.SameLine();

                if (gearset.SourceUrl != null)
                {
                    ImGui.SameLine();
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Copy, "Link##copy_gearset_url"))
                    {
                        ImGui.SetClipboardText(gearset.SourceUrl);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip($"Copy {gearset.SourceType} link to clipboard");
                    }
                }

                ImGui.SameLine();

                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.FileExport, "JSON##export_gearset_json"))
                {
                    ImGui.SetClipboardText(gearset.ExportToJsonStr());
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Copy gearset JSON to clipboard");
                }

                ImGui.SameLine();

                using var deletePopup = ImRaii.Popup("Delete Gearset?##delete_gearset_confirm_popup");
                if (deletePopup)
                {
                    if (ImGui.Button("Yes, Delete###confirm_delete_gearset_button"))
                    {
                        deleteGearset = true;
                        ImGui.CloseCurrentPopup();
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("This cannot be undone!");
                }

                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                if (gearset.Gearpieces.Count > 0)
                {
                    var drawingLeftSide = (gearset.Gearpieces[0].GearpieceType & GearpieceType.LeftSide) != 0;
                    var drawingRightSide = (gearset.Gearpieces[0].GearpieceType & GearpieceType.RightSide) != 0;

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
