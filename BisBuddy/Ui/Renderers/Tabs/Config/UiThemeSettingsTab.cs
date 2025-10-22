using BisBuddy.Gear;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Ui.Renderers.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using static Dalamud.Interface.Windowing.Window;

namespace BisBuddy.Ui.Renderers.Tabs.Config
{
    public class UiThemeSettingsTab(
        IConfigurationService configurationService,
        IAttributeService attributeService,
        ITextureProvider textureProvider,
        UiComponents uiComponents
        ) : TabRenderer<ConfigWindowTab>
    {
        private readonly IConfigurationService configurationService = configurationService;
        private readonly IAttributeService attributeService = attributeService;
        private readonly ITextureProvider textureProvider = textureProvider;
        private readonly UiComponents uiComponents = uiComponents;

        private UiTheme uiTheme => configurationService.UiTheme;

        public WindowSizeConstraints? TabSizeConstraints => null;

        public bool ShouldDraw => true;

        public void Draw()
        {
            var buttonHeight = ImGui.GetTextLineHeightWithSpacing() * 1.3f;
            var imageSize = new Vector2(buttonHeight * 0.75f);

            ImGui.Text(Resource.UiThemeConfigColorsSectionHeader);

            ImGui.Spacing();
            using (ImRaii.PushIndent(10f))
            {
                var lockSpacing = 5 * ImGuiHelpers.GlobalScale;

                foreach (var type in Enum.GetValues<CollectionStatusType>().OrderByDescending(t => (int) t))
                {
                    var (textColor, gameIcon) = uiTheme.GetCollectionStatusTheme(type);
                    var typeDescription = attributeService
                        .GetEnumAttribute<DisplayAttribute>(type)!.GetDescription()!;

                    if (textureProvider.GetFromGameIcon((int)gameIcon).TryGetWrap(out var texture, out var exception))
                    {
                        void drawImage()
                        {
                            var oldPos = ImGui.GetCursorPos();
                            ImGui.Image(texture.Handle, size: imageSize, tintCol: textColor);
                            ImGui.SetCursorPos(oldPos + new Vector2(imageSize.X + lockSpacing, 0));
                            using (ImRaii.PushFont(UiBuilder.IconFont))
                                ImGui.Text(FontAwesomeIcon.Lock.ToIconString());
                        }
                        if (DrawColorPicker(typeDescription, textColor, out var newColor, iconDraw: drawImage))
                            uiTheme.SetCollectionStatusTheme(type, newColor, gameIcon);
                    }
                }

                ImGui.Spacing();

                var materiaDiameter = 16 * ImGuiHelpers.GlobalScale;
                var materiaSpacing = 6 * ImGuiHelpers.GlobalScale;

                Action drawMateriaFunc(bool advanced)
                {
                    return () =>
                    {
                        var oldPos = ImGui.GetCursorPos();
                        uiComponents.MateriaSlot(materiaDiameter, advanced, CollectionStatusType.NotObtainable, materiaTooltip: Resource.UnmeldVerb);
                        ImGui.SetCursorPos(oldPos + new Vector2(materiaDiameter + materiaSpacing, 0));
                        uiComponents.MateriaSlot(materiaDiameter, advanced, CollectionStatusType.ObtainedComplete, materiaTooltip: Resource.MeldVerb);
                    };
                }

                if (DrawColorPicker(Resource.UiThemeColorMateriaSlotNormalLabel, uiTheme.MateriaSlotNormalColor, out var materiaSlotNormal, iconDraw: drawMateriaFunc(false)))
                    uiTheme.MateriaSlotNormalColor = materiaSlotNormal;

                if (DrawColorPicker(Resource.UiThemeColorMateriaSlotAdvancedLabel, uiTheme.MateriaSlotAdvancedColor, out var materiaSlotAdvanced, iconDraw: drawMateriaFunc(true)))
                    uiTheme.MateriaSlotAdvancedColor = materiaSlotAdvanced;

                ImGui.Spacing();

                if (DrawColorPicker(Resource.UiThemeColorGearpieceButton, uiTheme.ButtonColor, out var buttonColor, setTextColor: false))
                    uiTheme.ButtonColor = buttonColor;

                if (DrawColorPicker(Resource.UiThemeColorGearpieceButtonHovered, uiTheme.ButtonHovered, out var buttonHovered, setTextColor: false))
                    uiTheme.ButtonHovered = buttonHovered;

                if (DrawColorPicker(Resource.UiThemeColorGearpieceButtonActive, uiTheme.ButtonActive, out var buttonActive, setTextColor: false))
                    uiTheme.ButtonActive = buttonActive;
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text(Resource.UiThemeConfigStylesSectionHeader);
            ImGui.Spacing();

            using (ImRaii.PushIndent(10f))
            {
                var accentInGearset = uiTheme.ShowGearsetColorAccentFlag;
                if (ImGui.Checkbox(Resource.ShowGearsetColorAccentFlagLabel, ref accentInGearset))
                    uiTheme.ShowGearsetColorAccentFlag = accentInGearset;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Resource.ShowGearsetColorAccentFlagTooltip);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text(Resource.UiThemeConfigResetsSectionHeader);
            ImGui.Spacing();

            using (ImRaii.PushIndent(10f))
            {
                if (ImGui.Button(Resource.ResetUiThemeButton))
                    configurationService.ResetUiTheme();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Resource.ResetUiThemeTooltip);
            }
        }

        public void PreDraw() { }

        public void SetTabState(TabState state)
        {
            throw new NotImplementedException();
        }

        private bool DrawColorPicker(string label, Vector4 color, out Vector4 newColor, Action? iconDraw = null, bool setTextColor = true)
        {
            using var _ = ImRaii.PushId(label);
            var buttonHeight = ImGui.GetTextLineHeightWithSpacing() * 1.3f;
            var textYOffset = (buttonHeight - ImGui.GetTextLineHeight()) / 2;

            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(3.0f, 4.0f)))
            {
                if (ImGui.ColorButton(label, color, size: new(buttonHeight), flags: ImGuiColorEditFlags.NoDragDrop))
                    ImGui.OpenPopup("###color_button_popup");

                using (ImRaii.PushColor(ImGuiCol.Text, color, setTextColor))
                {
                    ImGui.SameLine();

                    var buttonSize = new Vector2(ImGui.GetContentRegionAvail().X, buttonHeight);
                    var iconPos = ImGui.GetCursorPos();
                    var screenPos = ImGui.GetCursorScreenPos();

                    iconPos.X += 5f * ImGuiHelpers.GlobalScale;
                    iconPos.Y += textYOffset;
                    var textPos = iconPos;

                    using (ImRaii.PushColor(ImGuiCol.Button, uiTheme.ButtonColor))
                    using (ImRaii.PushColor(ImGuiCol.ButtonActive, uiTheme.ButtonActive))
                    using (ImRaii.PushColor(ImGuiCol.ButtonHovered, uiTheme.ButtonHovered))
                        ImGui.Button("###mock_colors_button", buttonSize);

                    var nextPos = ImGui.GetCursorPos();

                    ImGui.PushClipRect(screenPos, screenPos + buttonSize, true);
                    try
                    {
                        if (iconDraw is Action iconDrawAction)
                        {
                            textPos.X += 50f * ImGuiHelpers.GlobalScale;
                            ImGui.SetCursorPos(iconPos);
                            iconDrawAction();
                        }

                        ImGui.SetCursorPos(textPos);

                        ImGui.Text(label);

                        ImGui.SetCursorPos(nextPos);
                    }
                    finally
                    {
                        ImGui.PopClipRect();
                    }
                }
            }


            using (var popup = ImRaii.Popup("###color_button_popup"))
            {
                if (popup)
                {
                    if (ImGui.ColorPicker4(
                        "##picker",
                        ref color,
                        ImGuiColorEditFlags.NoPicker
                        | ImGuiColorEditFlags.AlphaBar
                        | ImGuiColorEditFlags.NoSidePreview
                        | ImGuiColorEditFlags.DisplayRgb
                        | ImGuiColorEditFlags.NoBorder
                        ))
                    {
                        newColor = color;
                        return true;
                    }
                }

                newColor = default;
                return false;
            }
        }
    }
}
