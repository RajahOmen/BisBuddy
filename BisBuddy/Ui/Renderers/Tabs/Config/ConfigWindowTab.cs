using BisBuddy.Resources;
using System.ComponentModel.DataAnnotations;

namespace BisBuddy.Ui.Renderers.Tabs.Config
{
    public enum ConfigWindowTab
    {
        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.ConfigGeneralSectionHeader))]
        General,

        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.ConfigHighlightingSectionHeader))]
        Highlighting,

        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.ConfigInventorySectionHeader))]
        Inventory,

        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.ConfigUiThemeSectionHeader))]
        UiTheme,

        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.ConfigDebugSectionHeader))]
        Debug,
    }
}
