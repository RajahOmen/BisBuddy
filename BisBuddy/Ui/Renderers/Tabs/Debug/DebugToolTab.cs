using BisBuddy.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
