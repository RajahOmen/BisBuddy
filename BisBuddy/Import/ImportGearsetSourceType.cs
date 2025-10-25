using BisBuddy.Resources;
using System;
using System.ComponentModel.DataAnnotations;

namespace BisBuddy.Import
{
    [Serializable]
    public enum ImportGearsetSourceType
    {
        [Display(
            ResourceType = typeof(Resource),
            Name = nameof(Resource.ImportXivgearName),
            Description = nameof(Resource.ImportXivgearTooltip)
            )]
        Xivgear = 0,

        [Display(
            ResourceType = typeof(Resource),
            Name = nameof(Resource.ImportEtroName),
            Description = nameof(Resource.ImportEtroTooltip)
            )]
        Etro = 1,

        [Display(
            ResourceType = typeof(Resource),
            Name = nameof(Resource.ImportJsonName),
            Description = nameof(Resource.ImportJsonTooltip)
            )]
        Json = 2,

        [Display(
            ResourceType = typeof(Resource),
            Name = nameof(Resource.ImportTeamcraftName),
            Description = nameof(Resource.ImportTeamcraftTooltip)
            )]
        Teamcraft = 3,
    }
}
