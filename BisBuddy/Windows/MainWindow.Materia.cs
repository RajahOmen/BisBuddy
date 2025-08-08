using BisBuddy.Gear;
using BisBuddy.Resources;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
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
                            logger.Verbose($"Unmelding materia {materiaGroup.Materia.ItemId} from {gearpiece.ItemName}");
                            gearpiece.UnmeldSingleMateria(materiaGroup.Materia.ItemId);
                        }
                        else
                        {
                            logger.Verbose($"Melding materia {materiaGroup.Materia.ItemId} from {gearpiece.ItemName}");
                            gearpiece.MeldSingleMateria(materiaGroup.Materia.ItemId);
                        }
                    }
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(string.Format(Resource.MateriaTooltip, meldVerb, materiaGroup.Materia.ItemName));
                if (ImGui.IsItemHovered())
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    searchItemById(materiaGroup.Materia.ItemId);
                ImGui.SameLine();
            }
        }
    }
}
