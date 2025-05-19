using BisBuddy.Gear;
using BisBuddy.Resources;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System.Numerics;

namespace BisBuddy.Windows
{
    public partial class MainWindow
    {
        private void drawMateria(Gearpiece gearpiece)
        {
            if (gearpiece.ItemMateria == null || gearpiece.ItemMateria.Count == 0) return;

            for (var i = 0; i < gearpiece.ItemMateriaGrouped?.Count; i++)
            {
                using var _ = ImRaii.PushId(i);
                var materiaGroup = gearpiece.ItemMateriaGrouped[i];

                string needColorblind;
                string meldVerb;
                Vector4 textColor;
                if (materiaGroup.Materia.IsMelded)
                {
                    needColorblind = "";
                    meldVerb = Resource.UnmeldVerb;
                    textColor = ObtainedColor;
                }
                else
                {
                    needColorblind = "*";
                    meldVerb = Resource.MeldVerb;
                    textColor = UnobtainedColor;
                }

                var materiaButtonText = $"x{materiaGroup.Count} +{materiaGroup.Materia.StatQuantity} {materiaGroup.Materia.StatShortName}{needColorblind}";

                using (ImRaii.PushColor(ImGuiCol.Text, textColor))
                {
                    if (ImGui.Button($"{materiaButtonText}##materia_meld_button"))
                    {
                        if (materiaGroup.Materia.IsMelded)
                        {
                            Services.Log.Verbose($"Unmelding materia {materiaGroup.Materia.ItemId} from {gearpiece.ItemName}");
                            gearpiece.UnmeldSingleMateria(materiaGroup.Materia.ItemId);
                        }
                        else
                        {
                            Services.Log.Verbose($"Melding materia {materiaGroup.Materia.ItemId} from {gearpiece.ItemName}");
                            gearpiece.MeldSingleMateria(materiaGroup.Materia.ItemId);
                        }

                        // don't update here. Creates issues with being unable to unassign materia reliably due to no manual lock for uncollected.
                        plugin.SaveGearsetsWithUpdate(false);
                    }
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(string.Format(Resource.MateriaTooltip, meldVerb, materiaGroup.Materia.ItemName));
                if (ImGui.IsItemHovered())
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    Plugin.SearchItemById(materiaGroup.Materia.ItemId);
                ImGui.SameLine();
            }
        }
    }
}
