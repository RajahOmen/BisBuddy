using BisBuddy.Items;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
using Dalamud.Plugin.Services;

namespace BisBuddy.Services.Addon
{
    public class AddonServiceDependencies<T>(
        ITypedLogger<T> logger,
        IFramework framework,
        IAddonLifecycle addonLifecycle,
        IGameGui gameGui,
        IGearsetsService gearsetsService,
        IItemDataService itemDataService,
        IConfigurationService configurationService,
        IDebugService debugService
        ) where T : class
    {
        public readonly ITypedLogger<T> logger = logger;
        public readonly IFramework framework = framework;
        public readonly IAddonLifecycle AddonLifecycle = addonLifecycle;
        public readonly IGameGui GameGui = gameGui;
        public readonly IGearsetsService GearsetsService = gearsetsService;
        public readonly IItemDataService ItemDataService = itemDataService;
        public readonly IConfigurationService ConfigurationService = configurationService;
        public readonly IDebugService DebugService = debugService;
    }
}
