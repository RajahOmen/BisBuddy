using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Ui.Renderers.Tabs.Main;
using BisBuddy.Ui.Windows;
using System.Collections.Generic;

namespace BisBuddy.Commands
{
    public class OpenDebugCommand(
        IConfigurationService configurationService,
        IWindowService windowService
        ) : ICommand
    {
        private static readonly IEnumerable<CommandTrigger> DebugCommandTriggers = [
            new CommandTrigger("debug", "d"),
            ];

        private readonly IConfigurationService configurationService = configurationService;
        private readonly IWindowService windowService = windowService;

        public IEnumerable<CommandTrigger> Triggers => DebugCommandTriggers;

        public string Description => Resource.CommandDescriptionOpenDebug;

        public void Invoke(string args)
        {
            if (!configurationService.EnableDebugging)
                return;

            windowService.SetMainWindowTab(MainWindowTab.PluginDebug);
            windowService.ToggleWindow(WindowType.Main);
        }
    }
}
