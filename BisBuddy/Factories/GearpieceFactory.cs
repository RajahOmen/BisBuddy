using BisBuddy.Gear;
using BisBuddy.Gear.Melds;
using BisBuddy.Gear.Prerequisites;
using BisBuddy.Items;
using BisBuddy.Services;
using System;
using System.Collections.Generic;

namespace BisBuddy.Factories
{
    public class GearpieceFactory(
        ITypedLogger<GearpieceFactory> logger,
        ITypedLogger<Gearpiece> gearpieceLogger,
        IItemDataService itemDataService
        ) : IGearpieceFactory
    {
        private readonly ITypedLogger<GearpieceFactory> logger = logger;
        private readonly ITypedLogger<Gearpiece> gearpieceLogger = gearpieceLogger;
        private readonly IItemDataService itemDataService = itemDataService;

        public Gearpiece Create(
            uint itemId,
            MateriaGroup? itemMateria,
            bool isCollected = false,
            bool collectLock = false
            )
        {
            var prerequisiteTree = itemDataService.BuildGearpiecePrerequisiteTree(
                itemId,
                isCollected,
                collectLock
                );

            return Create(
                itemId,
                itemMateria,
                prerequisiteTree,
                isCollected,
                collectLock,
                false
                );
        }

        public Gearpiece Create(
            uint itemId,
            MateriaGroup? itemMateria,
            IPrerequisiteNode? prerequisiteTree,
            bool isCollected = false,
            bool collectLock = false,
            bool extendTree = true
            )
        {
            try
            {
                var itemName = itemDataService.GetItemNameById(itemId);
                var gearpieceType = itemDataService.GetItemGearpieceType(itemId);

                // extend prereq tree with new data that didn't exist last population
                if (extendTree)
                    prerequisiteTree = itemDataService.ExtendItemPrerequisites(
                        itemId,
                        prerequisiteTree,
                        isCollected,
                        collectLock
                        );

                var newGearpiece = new Gearpiece(
                    gearpieceLogger,
                    itemId,
                    itemName,
                    gearpieceType,
                    prerequisiteTree,
                    itemMateria,
                    isCollected,
                    collectLock
                    );

                return newGearpiece;
            }
            catch (Exception ex)
            {
                logger.Error($"Unable to create gearpiece for item id \"{itemId}\": {ex.Message}");
                throw new ArgumentException(ex.Message);
            }
        }
    }

    public interface IGearpieceFactory
    {
        /// <summary>
        /// Create a new Gearpiece with the provided details. Generate a new prerequisite tree for the item
        /// </summary>
        /// <param name="itemId">The item id of the gearpiece</param>
        /// <param name="itemMateria">The materia this gearpiece needs melded</param>
        /// <param name="isCollected">If the item should be marked as collected</param>
        /// <param name="collectLock">If the item's collection state should be locked</param>
        /// <returns>The created gearpiece</returns>
        /// <exception cref="ArgumentException">If the gearpiece could not be created due to invalid inputs</exception>
        public Gearpiece Create(
            uint itemId,
            MateriaGroup? itemMateria,
            bool isCollected = false,
            bool collectLock = false
            );

        /// <summary>
        /// Create a new Gearpiece with the provided details. Use provided prerequisite tree instead of generating a new one
        /// </summary>
        /// <param name="itemId">The item id of the gearpiece</param>
        /// <param name="itemMateria">The materia this gearpiece needs melded</param>
        /// <param name="prerequisiteTree">The prerequisite items needed to obtain this gearpiece</param>
        /// <param name="isCollected">If the item should be marked as collected</param>
        /// <param name="collectLock">If the item's collection state should be locked</param>
        /// <param name="extendTree">If extending the provided prerequisite tree with more leaves should be attempted</param>
        /// <returns>The created gearpiece</returns>
        /// <exception cref="ArgumentException">If the gearpiece could not be created due to invalid inputs</exception>
        public Gearpiece Create(
            uint itemId,
            MateriaGroup? itemMateria,
            IPrerequisiteNode? prerequisiteTree,
            bool isCollected = false,
            bool collectLock = false,
            bool extendTree = true
            );
    }
}
