using BisBuddy.Gear;
using BisBuddy.ItemAssignment;
using BisBuddy.Items;
using BisBuddy.Services;
using BisBuddy.Services.Config;
using Dalamud.Game.Inventory;
using System.Collections.Generic;

namespace BisBuddy.Factories
{
    public class ItemAssignmentSolverFactory(
        ITypedLogger<ItemAssigmentSolver> logger,
        IItemDataService itemDataService,
        IConfigurationService configurationService
        ) : IItemAssignmentSolverFactory
    {
        private readonly ITypedLogger<ItemAssigmentSolver> logger = logger;
        private readonly IItemDataService itemDataService = itemDataService;
        private readonly IConfigurationService configurationService = configurationService;

        public IItemAssignmentSolver Create(
            IEnumerable<Gearset> allGearsets,
            IEnumerable<Gearset> assignableGearsets,
            List<GameInventoryItem> inventoryItems
            )
        {
            return new ItemAssigmentSolver(
                logger,
                allGearsets,
                assignableGearsets,
                inventoryItems,
                itemDataService,
                configurationService.StrictMateriaMatching,
                configurationService.HighlightPrerequisiteMateria
                );
        }
    }

    public interface IItemAssignmentSolverFactory
    {
        public IItemAssignmentSolver Create(
            IEnumerable<Gearset> allGearsets,
            IEnumerable<Gearset> assignableGearsets,
            List<GameInventoryItem> inventoryItems
            );
    }
}
