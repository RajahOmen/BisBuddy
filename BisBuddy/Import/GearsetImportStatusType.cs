using BisBuddy.Resources;
using System.ComponentModel.DataAnnotations;

namespace BisBuddy.Import
{
    public enum GearsetImportStatusType
    {
        [Display(ResourceType = typeof(Resource), Description = nameof(Resource.ImportSuccess))]
        Success,

        [Display(ResourceType = typeof(Resource), Description = nameof(Resource.ImportFailInternalError))]
        InternalError,

        [Display(ResourceType = typeof(Resource), Description = nameof(Resource.ImportFailInvalidInput))]
        InvalidInput,

        [Display(ResourceType = typeof(Resource), Description = nameof(Resource.ImportFailInvalidResponse))]
        NoResponse,

        [Display(ResourceType = typeof(Resource), Description = nameof(Resource.ImportFailNoGearsets))]
        InvalidResponse,

        [Display(ResourceType = typeof(Resource), Description = nameof(Resource.ImportFailNoGearsets))]
        NoGearsets,

        [Display(ResourceType = typeof(Resource), Description = nameof(Resource.ImportFailTooManyGearsets))]
        TooManyGearsets,

        [Display(ResourceType = typeof(Resource), Description = nameof(Resource.ImportFailNotLoggedIn))]
        NotLoggedIn,
    }
}
