using BisBuddy.Resources;
using System.ComponentModel.DataAnnotations;

namespace BisBuddy.Ui.Renderers.Tabs.Debug
{
    public enum DebugToolTab
    {
        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.DebugItemRequirementsTab))]
        ItemRequirements,

        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.DebugSolverResultsTab))]
        SolverResults,

        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.DebugAddonsTab))]
        Addons,

        [Display(ResourceType = typeof(Resource), Name = nameof(Resource.DebugPrerequisitesTab))]
        Prerequisites,
    }
}
