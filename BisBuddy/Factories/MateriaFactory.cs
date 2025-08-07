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
        IItemDataService itemDataService
        ) : IMateriaFactory
    {
        private readonly IItemDataService itemDataService = itemDataService;

        public Materia Create(uint itemId, bool isMelded = false)
        {
            var materiaDetails = itemDataService.GetMateriaInfo(itemId);

            return new Materia(
                materiaDetails,
                isMelded
                );
        }
    }

    public interface IMateriaFactory
    {
        /// <summary>
        /// Creates a new materia instance.
        /// </summary>
        /// <param name="itemId">The item id of the materia to create.</param>
        /// <param name="isMelded">If the initial state of the materia should be melded</param>
        /// <returns>A new instance of the specified materia.</returns>
        Materia Create(uint itemId, bool isMelded = false);
    }
}
