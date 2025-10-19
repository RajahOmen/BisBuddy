using BisBuddy.Resources;
using System.ComponentModel.DataAnnotations;

namespace BisBuddy.Ui.Renderers.Tabs.Main
{
    public enum MainWindowTab
    {
        // displaying a user's gearsets
        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.UserGearsetsTabName))]
        UserGearsets = 0,

        // add and view items to be highlighted that don't belong to a gearset
        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.ItemTrackerTabName))]
        ItemTracker = 1,

        // map out weekly-locked item purchases
        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.ItemPlannerTabName))]
        ItemPlanner = 3,

        // keep track of items to be traded in / exchanged / opened
        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.ItemExchangesTabName))]
        ItemExchanges = 2,

        // open configuration as a tab in the main window
        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.ConfigTabName))]
        PluginConfig = 4,

        // open debugging tab
        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.DebugTabName))]
        PluginDebug = 5,
    }
}
