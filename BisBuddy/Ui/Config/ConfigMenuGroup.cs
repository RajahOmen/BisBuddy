using BisBuddy.Resources;
using System.ComponentModel.DataAnnotations;

namespace BisBuddy.Ui.Config
{
    public enum ConfigMenuGroup
    {
        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.ConfigGeneralSectionHeader))]
        General,

        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.ConfigHighlightingSectionHeader))]
        Highlighting,

        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.ConfigInventorySectionHeader))]
        Inventory,

        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.ConfigUiThemeSectionHeader))]
        UiTheme,
    }
}
