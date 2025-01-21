using BisBuddy.Gear;
using Dalamud.Interface;
using ImGuiNET;
using System.Numerics;

namespace BisBuddy.Windows
{
    public partial class MainWindow
    {
        private Gearset gearsetHovered = null;
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

            ImGui.BeginDisabled(!gearset.IsActive);
            ImGui.PushFont(UiBuilder.IconFont);
            var refreshButtonSize = ImGui.CalcTextSize(FontAwesomeIcon.Sync.ToIconString()) + ImGui.GetStyle().FramePadding * 2;
            if (ImGui.Button(FontAwesomeIcon.Sync.ToIconString() + $"##gearset_refresh", refreshButtonSize))
            {
                Services.Log.Debug($"refreshing gearset {gearset.Name}");
                plugin.UpdateFromInventory([gearset]);
                gearsetRefreshed = true;
            }
            ImGui.PopFont();
            if (ImGui.IsItemHovered())
            {
                updateGearsetHovered(gearset);
                var tooltip = gearsetRefreshed ? "Gearset Updated!" : "Sync Gearset With Inventory";
                ImGui.SetTooltip(tooltip);
            }
            ImGui.EndDisabled();

            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            var shiftHeld = ImGui.IsKeyDown(ImGuiKey.LeftShift) || ImGui.IsKeyDown(ImGuiKey.RightShift);
            var deleteButtonSize = ImGui.CalcTextSize(FontAwesomeIcon.TrashAlt.ToIconString()) + ImGui.GetStyle().FramePadding * 2;
            var deleteTooltipText = shiftHeld ? "Click to quick delete" : "Shift+Click to quick delete";
            var lightRed = new Vector4(0.5f, 0.2f, 0.2f, 1.0f);
            var darkRed = new Vector4(0.7f, 0.15f, 0.15f, 1.0f);
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

            ImGui.PopStyleVar();
            ImGui.SameLine();

            if (ImGui.CollapsingHeader($"[{gearset.JobAbbrv}] {gearset.Name}###gearset_collapsingheader"))
            {
                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Indent();

                var gearsetName = gearset.Name;
                if (ImGui.InputText($"##gearset_rename_input", ref gearsetName, 512))
                {
                    gearset.Name = gearsetName;
                    plugin.Configuration.Save();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Rename Gearset");

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

                if (ImGui.Button("Copy Json##export_gearset_json"))
                {
                    ImGui.SetClipboardText(gearset.ExportToJsonStr());
                }

                if (gearset.SourceUrl != null)
                {
                    ImGui.SameLine();
                    if (ImGui.Button("Copy Url##copy_gearset_url"))
                    {
                        ImGui.SetClipboardText(gearset.SourceUrl);
                    }
                }

                ImGui.SameLine();

                ImGui.PushStyleColor(ImGuiCol.Button, shiftHeld ? darkRed : lightRed);
                if (ImGui.Button($"Delete###delete_gearset_button" + $"##gearset_delete"))
                {
                    if (shiftHeld)
                    {
                        deleteGearset = true;
                    }
                    else
                    {
                        ImGui.OpenPopup("Delete Gearset?##delete_gearset_confirm_popup");
                    }
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Delete this Gearset. {deleteTooltipText}");

                ImGui.PopStyleColor();

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

                ImGui.BeginDisabled(!gearset.IsActive);
                for (var i = 0; i < gearset.Gearpieces.Count; i++)
                {
                    var item = gearset.Gearpieces[i];
                    ImGui.PushID($"{item.ItemId}_{i}");
                    drawGearpiece(item, gearset);
                    ImGui.PopID();
                }
                ImGui.EndDisabled();

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Unindent();
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
