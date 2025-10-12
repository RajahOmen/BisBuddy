using BisBuddy.Gear.Melds;
using BisBuddy.Items;
using BisBuddy.Mappers;
using BisBuddy.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Factories
{
    public class MateriaFactory(
        ITypedLogger<Materia> materiaLogger,
        IItemDataService itemDataService,
        IAttributeService attributeService
        ) : IMateriaFactory
    {
        private readonly ITypedLogger<Materia> materiaLogger = materiaLogger;
        private readonly IItemDataService itemDataService = itemDataService;
        private readonly IAttributeService attributeService = attributeService;

        public Materia Create(uint itemId, bool isCollected = false, bool collectLock = false)
        {
            var materiaDetails = itemDataService.GetMateriaInfo(itemId);

            return new Materia(
                materiaLogger,
                attributeService,
                materiaDetails,
                isCollected,
                collectLock
                );
        }
    }

    public interface IMateriaFactory
    {
        /// <summary>
        /// Creates a new materia instance.
        /// </summary>
        /// <param name="itemId">The item id of the materia to create.</param>
        /// <param name="isCollected">If the initial state of the materia should be melded</param>
        /// <param name="collectLock">If the initial collect state of the materia should be locked</param>
        /// <returns>A new instance of the specified materia.</returns>
        Materia Create(uint itemId, bool isCollected = false, bool collectLock = false);
    }
}
