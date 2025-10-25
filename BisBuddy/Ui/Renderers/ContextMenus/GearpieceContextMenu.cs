using BisBuddy.Factories;
using BisBuddy.Gear;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using Dalamud.Interface;
using System.Collections.Generic;
using System.Numerics;

namespace BisBuddy.Ui.Renderers.ContextMenus
{
    public class GearpieceContextMenu(
        ITypedLogger<GearpieceContextMenu> logger,
        IContextMenuEntryFactory factory,
        IItemFinderService itemFinderService,
        IConfigurationService configurationService
        ) : ContextMenuBase<Gearpiece, GearpieceContextMenu>(logger, factory)
    {
        private readonly IItemFinderService itemFinderService = itemFinderService;
        private readonly IConfigurationService configurationService = configurationService;

        private Vector4 textColorTheme(CollectionStatusType collectionStatusType) =>
            configurationService.UiTheme.GetCollectionStatusTheme(collectionStatusType).TextColor * TextMult;

        protected override List<ContextMenuEntry> buildMenuEntries(Gearpiece newComponent)
        {
            if (newComponent is not Gearpiece gearpiece)
                return [];

            return [
                factory.Create(
                    entryName: Resource.ContextMenuLockCollected,
                    textColor: () => textColorTheme(CollectionStatusType.ObtainedComplete),
                    icon: FontAwesomeIcon.Lock,
                    onClick: () => gearpiece.SetIsCollectedLocked(true),
                    shouldDraw: () => !gearpiece.IsCollected || !gearpiece.CollectLock),
                factory.Create(
                    entryName: Resource.ContextMenuLockUncollected,
                    textColor: () => textColorTheme(CollectionStatusType.NotObtainable),
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
