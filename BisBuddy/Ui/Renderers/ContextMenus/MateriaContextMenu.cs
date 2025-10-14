using BisBuddy.Factories;
using BisBuddy.Gear;
using BisBuddy.Gear.Melds;
using BisBuddy.Resources;
using BisBuddy.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System.Collections.Generic;

namespace BisBuddy.Ui.Renderers.ContextMenus
{
    public class MateriaContextMenu(
        ITypedLogger<MateriaContextMenu> logger,
        IContextMenuEntryFactory factory,
        IItemFinderService itemFinderService
        ) : ContextMenuBase<Materia, MateriaContextMenu>(logger, factory)
    {
        private readonly IItemFinderService itemFinderService = itemFinderService;

        protected override List<ContextMenuEntry> buildMenuEntries(Materia newComponent)
        {
            if (newComponent is not Materia materia)
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
