using BisBuddy.Services;
using BisBuddy.Ui.Renderers.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using System;
using System.Numerics;

namespace BisBuddy.Ui.Renderers.ContextMenus
{
    public record class ContextMenuEntry
    {
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
            Action? onClick
            )
        {
            this.logger = logger;
            EntryName = entryName;
            Draw = drawFunc ?? (() => DefaultDrawFunc(entryName, icon));
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

        public static bool DefaultDrawFunc(
            string entryName,
            FontAwesomeIcon? entryIcon,
            bool setSelected = false
            )
        {
            using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 1))
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudWhite2))
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(6, 6)))
            {
                var pos = ImGui.GetCursorPos();

                var menuItem = ImGui.Selectable("", selected: setSelected);

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
