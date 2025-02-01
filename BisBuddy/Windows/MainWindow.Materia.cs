using BisBuddy.Gear;
using Dalamud.Interface.Utility.Raii;
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

            for (var i = 0; i < materiaGrouped.Count; i++)
            {
                using var _ = ImRaii.PushId(i);
                var materia = materiaGrouped[i];

                string needColorblind;
                string meldVerb;
                Vector4 textColor;
                if (materia.IsMelded)
                {
                    needColorblind = "";
                    meldVerb = "Unmeld";
                    textColor = ObtainedColor;
                }
                else
                {
                    needColorblind = "*";
                    meldVerb = "Meld";
                    textColor = UnobtainedColor;
                }

                var materiaButtonText = $"x{materia.Count} +{materia.StatQuantity} {materia.StatShortName}{needColorblind}";

                using (ImRaii.PushColor(ImGuiCol.Text, textColor))
                using (ImRaii.Disabled(!gearpiece.IsCollected))
                {
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
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{meldVerb} {materia.ItemName}");
                    if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) Plugin.LinkItemById(materia.ItemId);
                }
                ImGui.SameLine();
            }
        }
    }
}
