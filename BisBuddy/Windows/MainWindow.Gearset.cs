using BisBuddy.Gear;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using ImGuiNET;
using System.Numerics;

namespace BisBuddy.Windows
{
    public partial class MainWindow
    {
        private Gearset? gearsetHovered = null;
        private bool gearsetRefreshed = false;

        private static Vector4 ManuallyCollectedColor = new(0.0f, 1.0f, 0.0f, 1.0f);

        private void updateGearsetHovered(Gearset hovererdGearset)
        {
            if (gearsetHovered == null || gearsetHovered != hovererdGearset)
            {
                gearsetHovered = hovererdGearset;
                gearsetRefreshed = false;
            }
        }

        private bool drawGearset(Gearset gearset)
        {
            var deleteGearset = false;

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4f, 4f));
            ImGui.PushFont(UiBuilder.IconFont);
            var isActive = gearset.IsActive;
            if (ImGui.Checkbox($"##gearset_active", ref isActive))
            {
                gearset.IsActive = isActive;
                Services.Log.Debug($"{(isActive ? "enabled" : "disabled")} gearset \"{gearset.Name}\"");
                plugin.SaveGearsetsWithUpdate();
            }
            ImGui.PopFont();
            if (ImGui.IsItemHovered())
            {
                updateGearsetHovered(gearset);
                var tooltip = isActive ? "Disable Gearset" : "Enable Gearset";
                ImGui.SetTooltip(tooltip);
            }

            ImGui.SameLine();

            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            var shiftHeld = ImGui.IsKeyDown(ImGuiKey.LeftShift) || ImGui.IsKeyDown(ImGuiKey.RightShift);
            var deleteButtonSize = ImGui.CalcTextSize(FontAwesomeIcon.TrashAlt.ToIconString()) + ImGui.GetStyle().FramePadding * 2;
            var deleteTooltipText = shiftHeld ? "Click to quick delete" : "Shift+Click to quick delete";
            var lightRed = new Vector4(0.5f, 0.2f, 0.2f, 1.0f);
            var darkRed = new Vector4(0.6f, 0.15f, 0.15f, 1.0f);
            ImGui.PushStyleColor(ImGuiCol.Button, shiftHeld ? darkRed : lightRed);
            if (ImGui.Button($"{FontAwesomeIcon.TrashAlt.ToIconString()}###delete_gearset_icon_button"))
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

            ImGui.PopStyleColor();
            ImGui.PopFont();
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(3.0f, 5.0f));

            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Delete this Gearset. {deleteTooltipText}");

            if (ImGui.BeginPopup("Delete Gearset?##delete_gearset_icon_confirm_popup"))
            {
                if (ImGui.Button("Yes, Delete###confirm_delete_icon_gearset_button"))
                {
                    deleteGearset = true;
                    ImGui.CloseCurrentPopup();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("This cannot be undone!");
                ImGui.EndPopup();
            }

            ImGui.SameLine();
            ImGui.PopStyleVar();

            if (ImGui.CollapsingHeader($"[{gearset.JobAbbrv}] {gearset.Name}###gearset_collapsingheader"))
            {
                ImGui.Spacing();

                var isAllCollected = gearset.Gearpieces.TrueForAll(g => g.IsCollected);
                var isAllManuallyCollected = isAllCollected & gearset.Gearpieces.TrueForAll(g => g.IsManuallyCollected);
                ImGui.BeginDisabled(!gearset.IsActive);
                if (isAllManuallyCollected) ImGui.PushStyleColor(ImGuiCol.CheckMark, ManuallyCollectedColor);
                if (ImGui.Checkbox("###collect_all_gearpieces", ref isAllCollected))
                {
                    foreach (var gearpiece in gearset.Gearpieces) gearpiece.SetCollected(isAllCollected, true);
                    plugin.SaveGearsetsWithUpdate();
                }
                if (isAllManuallyCollected) ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                {
                    var tooltip = isAllCollected
                        ? "Mark all as not Collected"
                        : "Lock all as Collected";
                    ImGui.SetTooltip(tooltip);
                }
                ImGui.EndDisabled();

                ImGui.SameLine();

                var gearsetName = gearset.Name;
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 122);
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

                if (ImGui.BeginPopup("Delete Gearset?##delete_gearset_confirm_popup"))
                {
                    if (ImGui.Button("Yes, Delete###confirm_delete_gearset_button"))
                    {
                        deleteGearset = true;
                        ImGui.CloseCurrentPopup();
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("This cannot be undone!");
                    ImGui.EndPopup();
                }

                ImGui.Spacing();
                ImGui.Spacing();

                var drawingLeftSide = false;
                var drawingRightSide = false;

                ImGui.BeginDisabled(!gearset.IsActive);
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

                    ImGui.PushID($"{item.ItemId}_{i}");
                    drawGearpiece(item, gearset);
                    ImGui.PopID();
                }
                ImGui.EndDisabled();

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
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
