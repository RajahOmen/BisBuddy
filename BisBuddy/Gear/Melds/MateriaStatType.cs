using BisBuddy.Resources;
using System.ComponentModel.DataAnnotations;
namespace BisBuddy.Gear.Melds
{
    /// <summary>
    /// Represents the type of materia that can be melded onto gear.
    /// Enum value corresponds to the "Materia" table RowId
    /// </summary>
    public enum MateriaStatType : uint
    {
        // DoW/DoM
        [Display(ResourceType = typeof(Resource), ShortName = nameof(Resource.MateriaAbbrevDirectHitRate))]
        DirectHitRate = 14u,
        [Display(ResourceType = typeof(Resource), ShortName = nameof(Resource.MateriaAbbrevCriticalHit))]
        CriticalHit = 15u,
        [Display(ResourceType = typeof(Resource), ShortName = nameof(Resource.MateriaAbbrevDetermination))]
        Determination = 16u,
        [Display(ResourceType = typeof(Resource), ShortName = nameof(Resource.MateriaAbbrevPiety))]
        Piety = 7u,
        [Display(ResourceType = typeof(Resource), ShortName = nameof(Resource.MateriaAbbrevTenacity))]
        Tenacity = 17u,
        [Display(ResourceType = typeof(Resource), ShortName = nameof(Resource.MateriaAbbrevSkillSpeed))]
        SkillSpeed = 24u,
        [Display(ResourceType = typeof(Resource), ShortName = nameof(Resource.MateriaAbbrevSpellSpeed))]
        SpellSpeed = 25u,

        // DoL (abbrevs from etro)
        [Display(ResourceType = typeof(Resource), ShortName = nameof(Resource.MateriaAbbrevGathering))]
        Gathering = 18u,
        [Display(ResourceType = typeof(Resource), ShortName = nameof(Resource.MateriaAbbrevPerception))]
        Perception = 19u,
        [Display(ResourceType = typeof(Resource), ShortName = nameof(Resource.MateriaAbbrevGatheringPoints))]
        GatheringPoints = 20u,

        // DoH (abbrevs from etro)
        [Display(ResourceType = typeof(Resource), ShortName = nameof(Resource.MateriaAbbrevCraftsmanship))]
        Craftsmanship = 21u,
        [Display(ResourceType = typeof(Resource), ShortName = nameof(Resource.MateriaAbbrevCraftingPoints))]
        CraftingPoints = 22u,
        [Display(ResourceType = typeof(Resource), ShortName = nameof(Resource.MateriaAbbrevControl))]
        Control = 23u,
    }
}
