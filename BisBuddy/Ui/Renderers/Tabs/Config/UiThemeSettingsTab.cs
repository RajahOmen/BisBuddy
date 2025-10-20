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
            var imageSize = new Vector2(ImGui.GetTextLineHeightWithSpacing());

            ImGui.Text(Resource.UiThemeConfigColorsSectionHeader);
            using (ImRaii.PushIndent(10f))
            {
                foreach (var type in Enum.GetValues<CollectionStatusType>().OrderByDescending(t => (int) t))
                {
                    var (textColor, gameIcon) = uiTheme.GetCollectionStatusTheme(type);
                    var typeDescription = attributeService
                        .GetEnumAttribute<DisplayAttribute>(type)!.GetDescription()!;

                    if (textureProvider.GetFromGameIcon((int)gameIcon).TryGetWrap(out var texture, out var exception))
                    {
                        var drawImage = () =>
                        {
                            ImGui.Image(texture.Handle, size: imageSize, tintCol: textColor);
                            ImGui.SameLine();
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 2);
                            using (ImRaii.PushFont(UiBuilder.IconFont))
                            using (ImRaii.PushColor(ImGuiCol.Text, textColor))
                                ImGui.Text(FontAwesomeIcon.Lock.ToIconString());
                        };
                        if (DrawColorPicker(typeDescription, textColor, out var newColor, prefixTextLabel: drawImage))
                            uiTheme.SetCollectionStatusTheme(type, newColor, gameIcon);
                    }
                }

                ImGui.Spacing();

                var offset = imageSize.Y * 0.15f;
                var materiaDiameter = imageSize.Y - offset;

                Action drawMateriaFunc(bool advanced)
                {
                    return () =>
                    {
                        var yPos = ImGui.GetCursorPosY() + offset;
                        ImGui.SetCursorPosY(yPos);
                        uiComponents.MateriaSlot(materiaDiameter, advanced, CollectionStatusType.NotObtainable, materiaTooltip: Resource.UnmeldVerb);
                        ImGui.SameLine();
                        ImGui.SetCursorPosY(yPos);
                        uiComponents.MateriaSlot(materiaDiameter, advanced, CollectionStatusType.ObtainedComplete, materiaTooltip: Resource.MeldVerb);
                    };
                }

                if (DrawColorPicker("Materia Slot (Normal)", uiTheme.MateriaSlotNormalColor, out var materiaSlotNormal, prefixTextLabel: drawMateriaFunc(false)))
                    uiTheme.MateriaSlotNormalColor = materiaSlotNormal;

                if (DrawColorPicker("Materia Slot (Advanced)", uiTheme.MateriaSlotAdvancedColor, out var materiaSlotAdvanced, prefixTextLabel: drawMateriaFunc(true)))
                    uiTheme.MateriaSlotAdvancedColor = materiaSlotAdvanced;
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

        private bool DrawColorPicker(string label, Vector4 color, out Vector4 newColor, Action? prefixTextLabel = null)
        {
            using var _ = ImRaii.PushId(label);

            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(3.0f, 4.0f)))
            {
                if (ImGui.ColorButton(label, color))
                    ImGui.OpenPopup("###color_button_popup");

                ImGui.SameLine();
                if (prefixTextLabel is Action action)
                {
                    ImGui.Dummy(new(3, 0));
                    ImGui.SameLine();
                    action();
                    ImGui.SameLine();
                    ImGui.Dummy(new(3, 0));
                    ImGui.SameLine();
                }
                using (ImRaii.PushColor(ImGuiCol.Text, color))
                    ImGui.Text(label);
            }


            using (var popup = ImRaii.Popup("###color_button_popup"))
            {
                if (popup)
                {
                    if (ImGui.ColorPicker4("##picker", ref color))
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
