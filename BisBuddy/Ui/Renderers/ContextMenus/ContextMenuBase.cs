using BisBuddy.Services;
using BisBuddy.Ui.Renderers;
using BisBuddy.Ui.Renderers.ContextMenus;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Ui.Components.ContextMenus
{
    public abstract class ContextMenuBase<T, TSelf>(
        ITypedLogger<TSelf> logger,
        ImGuiMouseButton activationButton = ImGuiMouseButton.Right
        ) : ITypedRenderer<T> where TSelf : ContextMenuBase<T, TSelf>
    {
        public static RendererType RendererType => RendererType.ContextMenu;

        protected readonly ImGuiMouseButton activationButton = activationButton;
        protected readonly ITypedLogger<TSelf> logger = logger;
        protected T? renderableComponent;
        protected List<ContextMenuEntry> menuEntries = [];

        protected abstract List<ContextMenuEntry> buildMenuEntries();

        public void Draw()
        {
            if (renderableComponent == null)
            {
                logger.Error("Attempted to draw uninitialized context menu");
                return;
            }

            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(activationButton))
                ImGui.OpenPopup("Context Menu");

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
            this.renderableComponent ??= renderableComponent;
            menuEntries = buildMenuEntries();
        }
    }
}
