using BisBuddy.Gear;
using Dalamud.Interface;
using ImGuiNET;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BisBuddy.Windows
{
    public partial class MainWindow
    {
        private void drawPrerequesites(List<GearpiecePrerequesite> prerequesites, Gearpiece parentGearpiece)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
            for (var i = 0; i < prerequesites.Count; i++)
            {
                ImGui.PushID(i);
                var prereq = prerequesites[i];
                var prereqLabelColorblind = prereq.IsCollected ? "" : "*";
                Vector4 color;

                if (prereq.IsCollected) color = ObtainedColor;
                else if (prereq.Prerequesites.Count > 0 && prereq.Prerequesites.All(p => p.IsCollected))
                    color = AlmostObtained;
                else color = UnobtainedColor;
                ImGui.PushStyleColor(ImGuiCol.Text, color);

                if (prereq.IsManuallyCollected)
                {
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.Text(FontAwesomeIcon.Lock.ToIconString());
                    ImGui.PopFont();
                    ImGui.PopStyleColor();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Collection status locked. Inventory syncs will not uncollect.");
                    ImGui.SameLine();
                }
                else ImGui.PopStyleColor();

                ImGui.PushStyleColor(ImGuiCol.Text, color);
                ImGui.BeginDisabled(parentGearpiece.IsCollected);
                if (ImGui.Button($"{prereq.ItemName}{prereqLabelColorblind}##prereq_button"))
                {
                    prereq.SetCollected(!prereq.IsCollected, true);
                    plugin.SaveGearsetsWithUpdate();
                }
                ImGui.EndDisabled();
                ImGui.PopStyleColor();
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) Plugin.LinkItemById(prereq.ItemId);
                if (ImGui.IsItemHovered())
                {
                    if (prereq.IsCollected)
                    {
                        ImGui.SetTooltip("Mark as Not Collected");
                    }
                    else
                    {
                        ImGui.SetTooltip("Lock as Collected");
                    }
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                if (prereq.PrerequesiteCount > 0)
                {
                    ImGui.Indent(30);
                    drawPrerequesites(prereq.Prerequesites, parentGearpiece);
                    ImGui.Unindent(30);
                }
                ImGui.PopID();
            }
            ImGui.PopStyleColor();
        }
    }
}
