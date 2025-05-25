using BisBuddy.Items;
using BisBuddy.Services.Config;
using BisBuddy.Services.Gearsets;
using Dalamud.Plugin.Services;
using KamiToolKit;

namespace BisBuddy.Services.Addon
{
    public class AddonServiceDependencies<T>(
        ITypedLogger<T> logger,
        IAddonLifecycle addonLifecycle,
        IGameGui gameGui,
        NativeController nativeController,
        IGearsetsService gearsetsService,
        IItemDataService itemDataService,
        IConfigurationService configurationService
        ) where T : class
    {
        public readonly ITypedLogger<T> logger = logger;
        public readonly IAddonLifecycle AddonLifecycle = addonLifecycle;
        public readonly IGameGui GameGui = gameGui;
        public readonly NativeController NativeController = nativeController;
        public readonly IGearsetsService GearsetsService = gearsetsService;
        public readonly IItemDataService ItemDataService = itemDataService;
        public readonly IConfigurationService ConfigurationService = configurationService;
    }
}
