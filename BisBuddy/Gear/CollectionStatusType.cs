using BisBuddy.Resources;
using System.ComponentModel.DataAnnotations;

namespace BisBuddy.Gear
{
    /// <summary>
    /// Represents the different states of collection a collectable item can be in
    /// </summary>
    public enum CollectionStatusType
    {
        /// <summary>
        /// An item is not marked collected and cannot be determined to be collectable
        /// </summary>
        [Display(ResourceType = typeof(Resource), Description = nameof(Resource.NotObtainableHelp))]
        NotObtainable,

        /// <summary>
        /// An item is not marked collected, and cannot be obtained, but has at least one prerequisite collected
        /// </summary>
        [Display(ResourceType = typeof(Resource), Description = nameof(Resource.NotObtainablePartialHelp))]
        NotObtainablePartial,

        /// <summary>
        /// An item is not marked collected, but it can be collected (via trade-in or prerequisites are collected)
        /// </summary>
        [Display(ResourceType = typeof(Resource), Description = nameof(Resource.ObtainableHelp))]
        Obtainable,

        /// <summary>
        /// An item is marked collected, but some sub-items (like materia) are not marked collected.
        /// </summary>
        [Display(ResourceType = typeof(Resource), Description = nameof(Resource.ObtainedPartialHelp))]
        ObtainedPartial,

        /// <summary>
        /// An item is marked collected and all sub-items (like materia) are marked collected.
        /// </summary>
        [Display(ResourceType = typeof(Resource), Description = nameof(Resource.ObtainedCompleteHelp))]
        ObtainedComplete,
    }
}
