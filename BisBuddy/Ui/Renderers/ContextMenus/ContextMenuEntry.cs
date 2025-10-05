using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Ui.Components.ContextMenus
{
    public record class ContextMenuEntry
    {
        public Func<bool> Draw { get; init; }
        public Func<bool> ShouldDraw { get; init; }
        public Action OnClick { get; init; }

        public ContextMenuEntry(
            string entryName = "???",
            Func<bool>? drawFunc = null,
            Func<bool>? shouldDraw = null,
            Action? onClick = null
            )
        {
            Draw = drawFunc ?? (() => ImGui.MenuItem(entryName));
            ShouldDraw = shouldDraw ?? (() => true);
            OnClick = onClick ?? (() => { });
        }
    }
}
