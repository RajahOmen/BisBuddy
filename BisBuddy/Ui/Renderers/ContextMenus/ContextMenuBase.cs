using BisBuddy.Factories;
using BisBuddy.Services;
using BisBuddy.Ui.Renderers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BisBuddy.Ui.Renderers.ContextMenus
{
    public abstract class ContextMenuBase<T, TSelf>(
        ITypedLogger<TSelf> logger,
        IContextMenuEntryFactory menuEntryFactory,
        ImGuiMouseButton activationButton = ImGuiMouseButton.Right,
        ImGuiHoveredFlags hoveredFlags = ImGuiHoveredFlags.AllowWhenDisabled
        ) : ITypedRenderer<T> where TSelf : ContextMenuBase<T, TSelf>
    {
        public static RendererType RendererType => RendererType.ContextMenu;

        protected readonly IContextMenuEntryFactory factory = menuEntryFactory;
        protected readonly ImGuiMouseButton activationButton = activationButton;
        protected readonly ImGuiHoveredFlags hoveredFlags = hoveredFlags;
        protected readonly ITypedLogger<TSelf> logger = logger;
        protected T? renderableComponent;
        protected List<ContextMenuEntry> menuEntries = [];

        protected abstract List<ContextMenuEntry> buildMenuEntries(T newComponent);

        public void Draw()
        {
            if (renderableComponent is null || menuEntries.Count == 0)
            {
                logger.Error("Attempted to draw uninitialized/empty context menu");
                return;
            }

            // ensure unique id for this context menu
            using var menuId = ImRaii.PushId($"{GetHashCode()}|{(int)activationButton}");

            if (ImGui.IsItemHovered(hoveredFlags) && ImGui.IsMouseClicked(activationButton))
            {
                logger.Debug($"Opening {menuEntries.Count} option context menu for {renderableComponent.GetType().Name}");
                ImGui.OpenPopup("Context Menu");
            }

            using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(8, 8)))
            using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 1))
            using (var popup = ImRaii.Popup("Context Menu"))
            {
                if (!popup)
                    return;

                foreach (var (idx, entry) in menuEntries.Index())
                {
                    using (ImRaii.PushId(idx))
                    using (ImRaii.ContextPopupItem("popup_item"))
                    {
                        if (entry.ShouldDraw() && entry.Draw())
                            entry.OnClick();
                    }
                }
            }
        }

        public void Initialize(T renderableComponent)
        {
            var newMenuEntries = buildMenuEntries(renderableComponent);
            if (newMenuEntries.Count == 0)
                throw new InvalidOperationException($"Attempted to initialize context menu for {renderableComponent} with no entries");

            this.renderableComponent ??= renderableComponent;
            menuEntries = newMenuEntries;
        }
    }
}
