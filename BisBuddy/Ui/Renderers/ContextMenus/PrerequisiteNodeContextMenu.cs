using BisBuddy.Factories;
using BisBuddy.Gear.Prerequisites;
using BisBuddy.Resources;
using BisBuddy.Services;
using Dalamud.Interface;
using System.Collections.Generic;

namespace BisBuddy.Ui.Renderers.ContextMenus
{
    public class PrerequisiteNodeContextMenu(
        ITypedLogger<PrerequisiteNodeContextMenu> logger,
        IContextMenuEntryFactory factory,
        IItemFinderService itemFinderService
        ) : ContextMenuBase<PrerequisiteNode, PrerequisiteNodeContextMenu>(logger, factory)
    {
        private readonly IItemFinderService itemFinderService = itemFinderService;

        protected override List<ContextMenuEntry> buildMenuEntries(PrerequisiteNode newComponent)
        {
            if (newComponent is not PrerequisiteNode node)
                return [];

            return [
                factory.Create(
                    entryName: Resource.ContextMenuSearchInventory,
                    icon: FontAwesomeIcon.Search,
                    onClick: () => itemFinderService.SearchForItem(node.ItemId)),
                ];
        }
    }
}
