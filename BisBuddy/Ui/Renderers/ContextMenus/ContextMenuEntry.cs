using BisBuddy.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Numerics;

namespace BisBuddy.Ui.Renderers.ContextMenus
{
    public record class ContextMenuEntry
    {
        private static readonly Vector4 HoveredAlpha = new(1, 1, 1, 0.8f);
        private static readonly Vector4 SelectedAlpha = new(1, 1, 1, 0.4f);

        private readonly ITypedLogger<ContextMenuEntry> logger;
        public string EntryName { get; init; }
        public Func<bool> Draw { get; init; }
        public Func<bool> ShouldDraw { get; init; }
        public Action OnClick { get; init; }

        public ContextMenuEntry(
            ITypedLogger<ContextMenuEntry> logger,
            string entryName,
            FontAwesomeIcon? icon,
            Func<bool>? drawFunc,
            Func<bool>? shouldDraw,
            Action? onClick,
            Func<Vector4>? textColor,
            Func<Vector4>? backgroundColor
            )
        {
            this.logger = logger;
            EntryName = entryName;
            Draw = drawFunc ?? (() => DefaultDrawFunc(entryName, icon, textColor, backgroundColor));
            ShouldDraw = shouldDraw ?? (() => true);
            OnClick = onClickWrapped(onClick ?? (() => { }));
        }

        private Action onClickWrapped(Action onClick)
        {
            return () =>
            {
                try
                {
                    logger.Debug($"Context menu \"{EntryName}\" clicked, executing action");
                    onClick();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error in context menu \"{EntryName}\" onClick");
                }
            };
        }

        private static bool DefaultDrawFunc(
            string entryName,
            FontAwesomeIcon? entryIcon,
            Func<Vector4>? textColor = null,
            Func<Vector4>? backgroundColor = null
            )
        {
            var text = textColor is not null ? textColor() : ImGuiColors.DalamudWhite2;
            using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 1))
            using (ImRaii.PushColor(ImGuiCol.Text, text))
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(6, 6)))
            {
                var pos = ImGui.GetCursorPos();

                if (backgroundColor is Func<Vector4> bgFunc)
                {
                    var bgColor = bgFunc();
                    ImGui.PushStyleColor(ImGuiCol.Header, bgColor * SelectedAlpha);
                    ImGui.PushStyleColor(ImGuiCol.HeaderHovered, bgColor * HoveredAlpha);
                    ImGui.PushStyleColor(ImGuiCol.HeaderActive, bgColor);
                }

                var menuItem = ImGui.Selectable("", selected: backgroundColor is not null);

                if (backgroundColor is not null)
                {
                    ImGui.PopStyleColor();
                    ImGui.PopStyleColor();
                    ImGui.PopStyleColor();
                }

                ImGui.SetCursorPos(pos);

                if (entryIcon is FontAwesomeIcon icon)
                {
                    using (ImRaii.PushFont(UiBuilder.IconFont))
                    {
                        ImGui.Text(icon.ToIconString());
                        ImGui.SameLine();
                    }
                }

                ImGui.Text(entryName);

                return menuItem;
            }
        }
    }
}
