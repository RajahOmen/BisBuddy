using BisBuddy.Factories;
using BisBuddy.Gear.Prerequisites;
using BisBuddy.Resources;
using BisBuddy.Services;
using CsvHelper;
using Dalamud.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Ui.Renderers.ContextMenus
{
    public class PrerequisiteAtomNodeContextMenu(
        ITypedLogger<PrerequisiteAtomNodeContextMenu> logger,
        IContextMenuEntryFactory factory,
        IItemFinderService itemFinderService
        ) : ContextMenuBase<PrerequisiteAtomNode, PrerequisiteAtomNodeContextMenu>(logger, factory)
    {
        private readonly IItemFinderService itemFinderService = itemFinderService;

    protected override List<ContextMenuEntry> buildMenuEntries(PrerequisiteAtomNode newComponent)
    {
        if (newComponent is not PrerequisiteAtomNode materia)
            return [];

        return [
            factory.Create(
                    entryName: Resource.ContextMenuSearchInventory,
                    icon: FontAwesomeIcon.Search,
                    onClick: () => itemFinderService.SearchForItem(materia.ItemId)),
                ];
    }
}
}
