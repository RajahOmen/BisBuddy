using BisBuddy.Factories;
using BisBuddy.Gear;
using BisBuddy.Resources;
using BisBuddy.Services;
using Dalamud.Interface;
using System.Collections.Generic;

namespace BisBuddy.Ui.Renderers.ContextMenus
{
    public class GearpieceContextMenu(
        ITypedLogger<GearpieceContextMenu> logger,
        IContextMenuEntryFactory factory,
        IItemFinderService itemFinderService
        ) : ContextMenuBase<Gearpiece, GearpieceContextMenu>(logger, factory)
    {
        private readonly IItemFinderService itemFinderService = itemFinderService;

        protected override List<ContextMenuEntry> buildMenuEntries(Gearpiece newComponent)
        {
            if (newComponent is not Gearpiece gearpiece)
                return [];

            return [
                factory.Create(
                    entryName: Resource.ContextMenuLockCollected,
                    icon: FontAwesomeIcon.Lock,
                    onClick: () => gearpiece.SetIsCollectedLocked(true),
                    shouldDraw: () => !gearpiece.IsCollected || !gearpiece.CollectLock),
                factory.Create(
                    entryName: Resource.ContextMenuLockUncollected,
                    icon: FontAwesomeIcon.Lock,
                    onClick: () => gearpiece.SetIsCollectedLocked(false),
                    shouldDraw: () => gearpiece.IsCollected || !gearpiece.CollectLock),
                factory.Create(
                    entryName: Resource.ContextMenuUnlock,
                    icon: FontAwesomeIcon.Unlock,
                    onClick: () => gearpiece.CollectLock = false,
                    shouldDraw: () => gearpiece.CollectLock),
                factory.Create(
                    entryName: Resource.ContextMenuSearchInventory,
                    icon: FontAwesomeIcon.Search,
                    onClick: () => itemFinderService.SearchForItem(gearpiece.ItemId))
                ];
        }
    }
}
