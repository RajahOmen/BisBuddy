using BisBuddy.Gear;
using BisBuddy.ItemAssignment;
using BisBuddy.Items;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using Dalamud.Game.Inventory;
using System.Collections.Generic;

namespace BisBuddy.Factories
{
    public class ItemAssignmentSolverFactory(
        ITypedLogger<ItemAssigmentSolver> logger,
        IItemDataService itemDataService,
        IMateriaFactory materiaFactory,
        IConfigurationService configurationService
        ) : IItemAssignmentSolverFactory
    {
        private readonly ITypedLogger<ItemAssigmentSolver> logger = logger;
        private readonly IItemDataService itemDataService = itemDataService;
        private readonly IMateriaFactory materiaFactory = materiaFactory;
        private readonly IConfigurationService configurationService = configurationService;

        public IItemAssignmentSolver? LastCreatedSolver { get; private set; } = null;

        public IItemAssignmentSolver Create(
            IEnumerable<Gearset> allGearsets,
            IEnumerable<Gearset> assignableGearsets,
            List<GameInventoryItem> inventoryItems
            )
        {
            var solver = new ItemAssigmentSolver(
                logger,
                allGearsets,
                assignableGearsets,
                inventoryItems,
                itemDataService,
                materiaFactory,
                configurationService.StrictMateriaMatching,
                configurationService.HighlightPrerequisiteMateria
                );
            LastCreatedSolver = solver;
            return solver;
        }
    }

    public interface IItemAssignmentSolverFactory
    {
        public IItemAssignmentSolver Create(
            IEnumerable<Gearset> allGearsets,
            IEnumerable<Gearset> assignableGearsets,
            List<GameInventoryItem> inventoryItems
            );

        public IItemAssignmentSolver? LastCreatedSolver { get; }
    }
}
