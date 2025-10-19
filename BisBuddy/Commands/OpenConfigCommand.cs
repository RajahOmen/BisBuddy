using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Ui.Renderers.Tabs.Main;
using BisBuddy.Ui.Windows;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Commands
{
    public class OpenConfigCommand(IWindowService windowService) : ICommand
    {
        private static readonly IEnumerable<CommandTrigger> ConfigTriggers = [
            new CommandTrigger("config", "c"),
            ];

        private readonly IWindowService windowService = windowService;

        public IEnumerable<CommandTrigger> Triggers => ConfigTriggers;

        public string Description => Resource.CommandDescriptionOpenConfig;

        public void Invoke(string args)
        {
            if (args.Split(" ").Any(arg => arg == "popout"))
            {
                windowService.ToggleWindow(WindowType.Config);
            }
            else
            {
                windowService.SetMainWindowTab(MainWindowTab.PluginConfig);

                if (windowService.IsWindowOpen(WindowType.Main))
                    windowService.SetWindowOpenState(WindowType.Config, false);

                windowService.ToggleWindow(WindowType.Main);
            }
        }
    }
}
