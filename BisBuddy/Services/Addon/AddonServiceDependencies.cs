using BisBuddy.Items;
using BisBuddy.Services;
using BisBuddy.Services.Gearsets;
using Dalamud.Plugin.Services;
using KamiToolKit;

namespace BisBuddy.Services.Addon
{
    public class AddonServiceDependencies(
        IAddonLifecycle addonLifecycle,
        IGameGui gameGui,
        NativeController nativeController,
        IPluginLog pluginLog,
        IGearsetsService gearsetsService,
        IItemDataService itemDataService,
        IConfigurationService configService
        )
    {
        public readonly IAddonLifecycle AddonLifecycle = addonLifecycle;
        public readonly IGameGui GameGui = gameGui;
        public readonly NativeController NativeController = nativeController;
        public readonly IPluginLog PluginLog = pluginLog;
        public readonly IGearsetsService GearsetsService = gearsetsService;
        public readonly IItemDataService ItemDataService = itemDataService;
        public readonly IConfigurationService ConfigService = configService;
    }
}
