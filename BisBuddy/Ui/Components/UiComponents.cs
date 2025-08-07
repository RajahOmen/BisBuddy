using BisBuddy.Extensions;
using BisBuddy.Services;
using BisBuddy.Util;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Ui.Components
{
    public class UiComponents(IAttributeService attributeService)
    {
        private static Vector2 AutoAdjustSize = new(0, 0);

        private readonly IAttributeService attributeService = attributeService;

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
        public T DrawCachedEnumSelectableDropdown<T>(
            T enumToDraw,
            Vector2? size = null,
            Vector2? itemSpacing = null,
            bool selected = true,
            ImGuiSelectableFlags selectableFlags = ImGuiSelectableFlags.None,
            ImGuiPopupFlags popupFlags = ImGuiPopupFlags.None,
            ImGuiWindowFlags popupWindowFlags = ImGuiWindowFlags.None
            ) where T : Enum
        {
            using var id = ImRaii.PushId("uicomponent_draw_dropdown");

            var selectedVal = enumToDraw;

            var buttonName = attributeService
                .GetEnumAttribute<DisplayAttribute>(enumToDraw)!
                .GetName()!;
            var buttonSize = size ?? AutoAdjustSize;
            var selectablePos = ImGui.GetWindowPos() + ImGui.GetCursorPos();
            var selectableHeight = ImGui.CalcTextSize(buttonName).Y + (ImGui.GetStyle().ItemSpacing.Y * 2);

            if (SelectableCentered(
                label: $"{buttonName}##draw_dropdown_button",
                selected: selected,
                flags: selectableFlags,
                size: buttonSize,
                labelPosOffset: new(3, 0),
                centerX: false
                ))
            {
                var popupLocation = selectablePos;
                popupLocation.Y += selectableHeight;
                ImGui.SetNextWindowPos(popupLocation);

                ImGui.OpenPopup($"draw_dropdown_popup", popupFlags);
            }

            var dropdownWidth = Math.Max(buttonSize.X, 70 * ImGuiHelpers.GlobalScale);
            ImGui.SetNextWindowSize(new(dropdownWidth, 0));
            using (var dropdownPopup = ImRaii.Popup($"draw_dropdown_popup"))
            {
                if (dropdownPopup)
                {
                    var spacing = itemSpacing ?? Constants.SelectableListSpacing;
                    spacing *= ImGuiHelpers.GlobalScale;

                    using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing);
                    var selectableSize = new Vector2(dropdownWidth, 0);
                    foreach (T val in Enum.GetValues(typeof(T)))
                    {
                        var optionName = attributeService
                            .GetEnumAttribute<DisplayAttribute>(val)!
                            .GetName()!;
                        var optionSelected = val.Equals(selectedVal);
                        if (ImGui.Selectable($"{optionName}##draw_dropdown", optionSelected, ImGuiSelectableFlags.None, selectableSize))
                            selectedVal = val;
                    }
                }
            }

            return selectedVal;
        }

        public T DrawCachedEnumComboDropdown<T>(
            T enumToDraw,
            bool selected = true,
            ImGuiComboFlags flags = ImGuiComboFlags.None
            ) where T : Enum
        {
            using var id = ImRaii.PushId($"uicomponent_draw_combo_{enumToDraw}");

            var comboName = attributeService
                .GetEnumAttribute<DisplayAttribute>(enumToDraw)!
                .GetName()!;
            using var combo = ImRaii.Combo("##uicomponent_combo", comboName, flags);

            if (!combo.Success)
                return enumToDraw;

            var selectedVal = enumToDraw;
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(5, 5)))
            {
                foreach (T val in Enum.GetValues(typeof(T)))
                {
                    var optionName = attributeService
                        .GetEnumAttribute<DisplayAttribute>(val)!
                        .GetName()!;
                    if (ImGui.Selectable($"{optionName}##draw_dropdown"))
                        selectedVal = val;
                }
            }

            return selectedVal;
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
            var labelOffset = labelPosOffset + (labelPosOffsetScaled * ImGuiHelpers.GlobalScale) ?? Vector2.Zero;

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
    }
}
