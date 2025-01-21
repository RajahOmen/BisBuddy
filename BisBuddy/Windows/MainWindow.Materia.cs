using BisBuddy.Gear;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;

namespace BisBuddy.Windows
{
    public partial class MainWindow
    {
        private void drawMateria(Gearpiece gearpiece)
        {
            if (gearpiece.ItemMateria == null || gearpiece.ItemMateria.Count == 0) return;

            var materiaGrouped = gearpiece.ItemMateria
                .GroupBy(m => new
                {
                    m.StatShortName,
                    m.StatQuantity,
                    m.IsMelded,
                    m.ItemId,
                    m.ItemName
                }).OrderBy(g => g.Key.IsMelded)
                .ThenByDescending(g => g.Key.StatQuantity)
                .ThenBy(g => g.Key.StatShortName)
                .Select(g => new
                {
                    g.Key.ItemId,
                    g.Key.StatShortName,
                    g.Key.StatQuantity,
                    g.Key.IsMelded,
                    g.Key.ItemName,
                    Count = g.Count()
                }).ToList();

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));

            for (var i = 0; i < materiaGrouped.Count; i++)
            {
                var materia = materiaGrouped[i];
                ImGui.PushID($"{i}{materia.Count}{materia.ItemId}{materia.IsMelded}{gearpiece.ItemId}");

                string needColorblind;
                if (materia.IsMelded)
                {
                    needColorblind = "";
                    ImGui.PushStyleColor(ImGuiCol.Text, ObtainedColor);
                }
                else
                {
                    needColorblind = "*";
                    ImGui.PushStyleColor(ImGuiCol.Text, UnobtainedColor);
                }

                var materiaButtonText = $"x{materia.Count} +{materia.StatQuantity} {materia.StatShortName}{needColorblind}";


                ImGui.BeginDisabled(!gearpiece.IsCollected);
                if (ImGui.Button($"{materiaButtonText}##materia_meld_button"))
                {
                    if (materia.IsMelded)
                    {
                        Services.Log.Verbose($"Unmelding materia {materia.ItemId} from {gearpiece.ItemName}");
                        gearpiece.UnmeldSingleMateria(materia.ItemId);
                    }
                    else
                    {
                        Services.Log.Verbose($"Melding materia {materia.ItemId} from {gearpiece.ItemName}");
                        gearpiece.MeldSingleMateria(materia.ItemId);
                    }

                    plugin.SaveGearsetsWithUpdate();
                }
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{(materia.IsMelded ? "-" : "+")}1 {materia.ItemName}");
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) Plugin.LinkItemById(materia.ItemId);
                if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.EndDisabled();
                ImGui.PopID();
                ImGui.SameLine();
            }
            ImGui.PopStyleColor();
        }
    }
}
