using BisBuddy.Resources;
using BisBuddy.Services.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Numerics;
using static Dalamud.Interface.Windowing.Window;

namespace BisBuddy.Ui.Renderers.Tabs.Config
{
    public class GeneralSettingsTab(IConfigurationService configurationService) : TabRenderer<ConfigWindowTab>
    {
        private readonly IConfigurationService configurationService = configurationService;

        public WindowSizeConstraints? TabSizeConstraints => null;

        public bool ShouldDraw => true;
        public void Draw()
        {
            ImGui.Text(Resource.GeneralConfigurationHighlightAppearanceCategory);
            ImGui.Spacing();

            using (ImRaii.PushIndent(10f))
            {
                using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(3.0f, 4.0f)))
                {
                    // COLOR PICKER
                    var existingColor = configurationService.DefaultHighlightColor.BaseColor;
                    if (ImGui.ColorButton(
                        $"{Resource.HighlightColorButtonTooltip}###ColorPickerButton",
                        existingColor,
                        ImGuiColorEditFlags.NoDragDrop
                        ))
                    {
                        ImGui.OpenPopup($"###ColorPickerPopup");
                    }

                    using (var popup = ImRaii.Popup($"###ColorPickerPopup"))
                    {
                        if (popup)
                        {
                            if (ImGui.ColorPicker4(
                                $"###ColorPicker",
                                ref existingColor,
                                ImGuiColorEditFlags.NoPicker
                                | ImGuiColorEditFlags.AlphaBar
                                | ImGuiColorEditFlags.NoSidePreview
                                | ImGuiColorEditFlags.DisplayRgb
                                | ImGuiColorEditFlags.DisplayHex
                                | ImGuiColorEditFlags.NoBorder
                                ))
                            {
                                if (existingColor != configurationService.DefaultHighlightColor.BaseColor)
                                    configurationService.DefaultHighlightColor.UpdateColor(existingColor);
                            }
                        }
                    }
                    ImGui.SameLine();
                    ImGui.TextUnformatted(Resource.HighlightColorButtonLabel);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Resource.HighlightColorHelp);

                // BRIGHT CUSTOM NODE HIGHLIGHTING
                var brightListItemHighlighting = configurationService.BrightListItemHighlighting;
                if (ImGui.Checkbox(Resource.BrightListItemHighlightingCheckbox, ref brightListItemHighlighting))
                    configurationService.BrightListItemHighlighting = brightListItemHighlighting;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Resource.BrightListItemHighlightingHelp);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text(Resource.GeneralConfigurationMiscellaneousCategory);
            ImGui.Spacing();

            using (ImRaii.PushIndent(10f))
            {
                //UNCOLLECTED MATERIA HIGHLIGHTING
                var highlightUncollectedItemMateria = configurationService.HighlightUncollectedItemMateria;
                if (ImGui.Checkbox(Resource.HighlightUncollectedItemMateriaCheckbox, ref highlightUncollectedItemMateria))
                    configurationService.HighlightUncollectedItemMateria = highlightUncollectedItemMateria;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Resource.HighlightUncollectedItemMateriaHelp);

                // ASSIGNMENT GROUPING
                var strictMateriaMatching = configurationService.StrictMateriaMatching;
                if (ImGui.Checkbox(Resource.StrictMateriaMatchingCheckbox, ref strictMateriaMatching))
                    configurationService.StrictMateriaMatching = strictMateriaMatching;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Resource.StrictMateriaMatchingHelp);
            }
        }

        public void PreDraw() { }

        public void SetTabState(TabState state)
        {
            throw new NotImplementedException();
        }
    }
}
