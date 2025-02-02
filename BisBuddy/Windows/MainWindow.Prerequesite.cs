using BisBuddy.Gear;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
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
            for (var i = 0; i < prerequesites.Count; i++)
            {
                using var _ = ImRaii.PushId(i);
                var prereq = prerequesites[i];
                var prereqLabelColorblind = prereq.IsCollected ? "" : "*";
                Vector4 textColor;

                if (prereq.IsCollected) textColor = ObtainedColor;
                else if (prereq.Prerequesites.Count > 0 && prereq.Prerequesites.All(p => p.IsCollected))
                    textColor = AlmostObtained;
                else textColor = UnobtainedColor;

                using (ImRaii.PushColor(ImGuiCol.Text, textColor))
                {
                    if (prereq.IsManuallyCollected)
                    {
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            ImGui.Text(FontAwesomeIcon.Check.ToIconString());
                        }
                        if (ImGui.IsItemHovered())
                        {
                            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1, 1, 1, 1)))
                            {
                                ImGui.SetTooltip("Collection status locked. Inventory syncs will not uncollect");
                            }
                        }

                        ImGui.SameLine();
                    }

                    using (ImRaii.Disabled(parentGearpiece.IsCollected))
                    {
                        if (ImGui.Button($"{prereq.ItemName}{prereqLabelColorblind}##prereq_button"))
                        {
                            prereq.SetCollected(!prereq.IsCollected, true);
                            Services.Log.Debug($"Set \"{parentGearpiece.ItemName}\" prereq \"{prereq.ItemName}\" to {(prereq.IsCollected ? "collected" : "not collected")}");
                            plugin.SaveGearsetsWithUpdate();
                        }
                    }
                }

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
                    using (ImRaii.PushIndent(40.0f))
                    {
                        drawPrerequesites(prereq.Prerequesites, parentGearpiece);
                    }
                }
            }
        }
    }
}
