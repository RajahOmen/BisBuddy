using BisBuddy.Gear;
using BisBuddy.Services;
using BisBuddy.Ui.Components.ContextMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Ui.Renderers.ContextMenus
{
    public class GearpieceContextMenu(
        ITypedLogger<GearpieceContextMenu> logger
        ) : ContextMenuBase<Gearpiece, GearpieceContextMenu>(logger)
    {
        protected override List<ContextMenuEntry> buildMenuEntries()
        {
            if (renderableComponent is not Gearpiece gearpiece)
                return [];

            List<ContextMenuEntry> entries = [
                new($"Collddect", onClick: () => gearpiece.SetIsCollectedLocked(true)),
                new($"Uncollddect", onClick: () => gearpiece.SetIsCollectedLocked(false))
                ];

            return entries;
        }
    }
}
