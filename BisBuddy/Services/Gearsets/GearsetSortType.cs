using BisBuddy.Resources;
using System.ComponentModel.DataAnnotations;

namespace BisBuddy.Services.Gearsets
{
    public enum GearsetSortType
    {
        // Alphabetically by gearset name
        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.GearsetSortName))]
        Name,

        // Alphabetically by job abbreviation
        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.GearsetSortJob))]
        Job,

        // When a gearset was imported
        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.GearsetSortImportDate))]
        ImportDate,

        //// Priority number given by user
        //[Display(ResourceType = typeof(Resource), Name = nameof(Resource.GearsetSortPriority))]
        //Priority,

        // If a gearset is enabled
        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.GearsetSortActive))]
        Active,
    }
}
