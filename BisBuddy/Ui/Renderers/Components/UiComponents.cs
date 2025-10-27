using BisBuddy.Gear;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Util;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;

namespace BisBuddy.Ui.Renderers.Components
{
    public class UiComponents(
        IAttributeService attributeService,
        IConfigurationService configurationService
        )
    {
        private static Vector2 AutoAdjustSize = new(0, 0);

        public static Vector4 MateriaFillColorMult = new(0.7f, 0.7f, 0.7f, 0.9f);

        public static float MateriaSlotBorderThickness = 1.5f;

        private readonly IAttributeService attributeService = attributeService;
        private UiTheme uiTheme =>
            configurationService.UiTheme;

        /// <summary>
        /// Given a enum type, draws a selectable element with the enum values as choices when clicked.
        /// Returns the selected enum value
        /// </summary>
        /// <typeparam name="T">The enum type</typeparam>
        /// <param name="enumToDraw">The value to draw initially</param>
        /// <param name="size">The size of the selectable element. Defaults to filling space</param>
        /// <param name="itemSpacing">How much spacing to give between selectable elements in the dropdown</param>
        /// <param name="selected">If the selectable should be marked as selected</param>
        /// <param name="selectableFlags">The selectable's flags</param>
        /// <param name="popupFlags">Flags for opening the popup</param>
        /// <param name="popupWindowFlags">The popup's window flags</param>
        /// <returns></returns>
        public bool DrawCachedEnumSelectableDropdown<T>(
            T enumToDraw,
            out T newEnumValue,
            Vector2? size = null,
            Vector2? itemSpacing = null,
            bool selected = true,
            ImGuiSelectableFlags selectableFlags = ImGuiSelectableFlags.None,
            ImGuiPopupFlags popupFlags = ImGuiPopupFlags.None,
            ImGuiWindowFlags popupWindowFlags = ImGuiWindowFlags.None
            ) where T : Enum
        {
            using var id = ImRaii.PushId("uicomponent_draw_dropdown");

            newEnumValue = enumToDraw;

            var buttonName = attributeService
                .GetEnumAttribute<DisplayAttribute>(enumToDraw)!
                .GetName()!;
            var buttonSize = size ?? AutoAdjustSize;
            var selectablePos = ImGui.GetCursorScreenPos();
            var selectableHeight = ImGui.GetFrameHeightWithSpacing();

            if (SelectableCentered(
                label: $"{buttonName}##draw_dropdown_button",
                selected: selected,
                flags: selectableFlags,
                size: buttonSize,
                labelPosOffsetScaled: new(5, 0),
                centerX: false
                ))
            {
                var popupLocation = selectablePos;
                popupLocation.Y += selectableHeight;
                ImGui.SetNextWindowPos(popupLocation);

                ImGui.OpenPopup($"draw_dropdown_popup", popupFlags);
            }

            var selectionMade = false;
            var dropdownWidth = Math.Max(buttonSize.X, 70 * ImGuiHelpers.GlobalScale);
            ImGui.SetNextWindowSize(new(dropdownWidth, 0));
            using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
            using (var dropdownPopup = ImRaii.Popup($"draw_dropdown_popup"))
            {
                if (!dropdownPopup)
                    return false;

                var spacing = itemSpacing ?? Constants.SelectableListSpacing;
                spacing *= ImGuiHelpers.GlobalScale;

                using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing);
                var selectableSize = new Vector2(dropdownWidth, ImGui.GetTextLineHeightWithSpacing());
                foreach (T val in Enum.GetValues(typeof(T)))
                {
                    var optionName = attributeService
                        .GetEnumAttribute<DisplayAttribute>(val)!
                        .GetName()!;
                    var optionSize = ImGui.CalcTextSize(optionName);
                    var optionSelected = val.Equals(newEnumValue);
                    var pos = ImGui.GetCursorPos();
                    var textPosOffset = new Vector2(
                        spacing.X,
                        (selectableSize.Y - optionSize.Y) / 2
                        );
                    ImGui.SetCursorPos(pos + textPosOffset);
                    ImGui.Text(optionName);
                    ImGui.SetCursorPos(pos);
                    if (ImGui.Selectable($"###draw_dropdown_{optionName}", optionSelected, ImGuiSelectableFlags.None, selectableSize))
                    {
                        newEnumValue = val;
                        selectionMade = true;
                    }
                }
            }

            return selectionMade;
        }

        public bool DrawCachedEnumComboDropdown<T>(
            T enumToDraw,
            out T newEnumValue,
            bool selected = true,
            ImGuiComboFlags flags = ImGuiComboFlags.None
            ) where T : Enum
        {
            newEnumValue = enumToDraw;
            using var id = ImRaii.PushId($"uicomponent_draw_combo_{enumToDraw}");

            var comboAttribute = attributeService
                .GetEnumAttribute<DisplayAttribute>(enumToDraw)!;
            var comboName = comboAttribute.GetName()!;

            using var combo = ImRaii.Combo("##uicomponent_combo", comboName, flags);
            if (
                ImGui.IsItemHovered()
                && comboAttribute.GetDescription() is string comboDesc
            )
                ImGui.SetTooltip(comboDesc);

            if (!combo)
                return false;

            var selectionMade = false;
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(5, 5)))
            {
                foreach (T val in Enum.GetValues(typeof(T)))
                {
                    var optionAttribute = attributeService
                        .GetEnumAttribute<DisplayAttribute>(val)!;
                    var optionName = optionAttribute.GetName()!;
                    if (ImGui.Selectable($"{optionName}##draw_dropdown"))
                    {
                        selectionMade = true;
                        newEnumValue = val;
                    }
                    if (ImGui.IsItemHovered()
                        && optionAttribute.GetDescription() is string optionDesc)
                    {
                        ImGui.SetTooltip(optionDesc);
                    }
                }
            }

            return selectionMade;
        }

        /// <summary>
        /// An ImGui Selectable with a centered label.
        /// </summary>
        /// <param name="label">The label for the selectable</param>
        /// <param name="size">The size of the selectable. Defaults to auto-resize</param>
        /// <returns>If the selectable is clicked</returns>
        public static bool SelectableCentered(
            string label,
            bool centerX = true,
            bool centerY = true,
            bool selected = true,
            Vector2? size = null,
            ImGuiSelectableFlags flags = ImGuiSelectableFlags.None,
            Vector2? labelPosOffset = null,
            Vector2? labelPosOffsetScaled = null
            )
        {
            // strip any non-visible parts from being drawn
            var labelText = label.Split("#")[0];
            var labelId = label.Split("#").LastOrDefault();

            var selectableSize = size ?? AutoAdjustSize;
            var selectablePos = ImGui.GetCursorPos();

            var labelSize = ImGui.CalcTextSize(label);
            var labelOffset = (labelPosOffset ?? Vector2.Zero) + (labelPosOffsetScaled * ImGuiHelpers.GlobalScale ?? Vector2.Zero);

            var itemSpacing = ImGui.GetStyle().ItemSpacing;

            // calculate label position and place it
            var labelX = centerX
                ? selectablePos.X + Math.Max(0, (selectableSize.X - labelSize.X) / 2) + labelOffset.X
                : selectablePos.X + itemSpacing.X + labelOffset.X;
            var labelY = centerY
                ? selectablePos.Y + Math.Max(0, (selectableSize.Y - labelSize.Y) / 2) + labelOffset.Y
                : selectablePos.Y + itemSpacing.Y + labelOffset.Y;
            var labelPos = new Vector2(labelX, labelY);
            ImGui.SetCursorPos(labelPos);
            ImGui.Text(labelText);

            // return to selectable pos and place it
            ImGui.SetCursorPos(selectablePos);
            return ImGui.Selectable(
                label: $"###uicomponents_centered_selectable_{labelId}",
                selected: selected,
                flags: flags,
                size: selectableSize
                );
        }

        public void MateriaSlot(
            float diameter,
            bool isAdvanced,
            CollectionStatusType collectionStatus,
            string? materiaTooltip = null
            )
        {
            var drawList = ImGui.GetWindowDrawList();
            var cursorPos = ImGui.GetCursorScreenPos();
            var edgeCol = isAdvanced
                ? ImGui.GetColorU32(uiTheme.MateriaSlotAdvancedColor)
                : ImGui.GetColorU32(uiTheme.MateriaSlotNormalColor);

            var (materiaColor, _) = uiTheme.GetCollectionStatusTheme(collectionStatus);

            var radius = diameter / 2;
            var circleSize = new Vector2(diameter, diameter);
            var circlePos = cursorPos + circleSize / 2;

            if (collectionStatus >= CollectionStatusType.ObtainedPartial)
            {
                var innerCol = ImGui.GetColorU32(materiaColor * MateriaFillColorMult);
                drawList.AddCircleFilled(circlePos, radius, innerCol);
            }

            drawList.AddCircle(circlePos, radius, edgeCol, MateriaSlotBorderThickness * ImGuiHelpers.GlobalScale);
            ImGui.Dummy(circleSize);

            if (materiaTooltip is not string tooltipString)
                return;

            var tooltip = isAdvanced
                ? $"{Resource.AdvancedMateriaMeldTooltip}{tooltipString}"
                : tooltipString;
            using (ImRaii.PushColor(ImGuiCol.Text, materiaColor))
                if (ImGui.IsItemHovered())
                    SetSolidTooltip(tooltip);
        }

        public static void SetSolidTooltip(ImU8String text)
        {
            using (ImRaii.Enabled())
            using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 1.0f))
            {
                ImGui.SetTooltip(text);
            }
        }

        /// <summary>
        /// Tables in ImGui have a quirk where when drawing with no padding, the contents can write on the border of the table.
        /// This adds a clip rect that cuts off the content of the tables slightly to prevent that.
        /// </summary>
        public static void PushTableClipRect(int borderSize = 1)
        {
            var topLeft = ImGui.GetCursorScreenPos();
            var bottomRight = topLeft + ImGui.GetContentRegionAvail();
            topLeft.Y += borderSize;
            bottomRight.Y -= borderSize;
            ImGui.PushClipRect(topLeft, bottomRight, true);
        }

        public static void DrawChildLConnector()
        {
            var drawList = ImGui.GetWindowDrawList();
            var curLoc = ImGui.GetCursorScreenPos();
            var col = ImGui.GetColorU32(Vector4.One);
            var halfButtonHeight = ImGui.GetTextLineHeight() / 2 + ImGui.GetStyle().FramePadding.Y;
            drawList.AddLine(curLoc + new Vector2(10, 0), curLoc + new Vector2(10, halfButtonHeight), col, 2);
            drawList.AddLine(curLoc + new Vector2(10, halfButtonHeight), curLoc + new Vector2(20, halfButtonHeight), col, 2);
        }
    }
}
