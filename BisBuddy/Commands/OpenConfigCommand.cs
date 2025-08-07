using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Ui;
using System.Collections.Generic;

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
            windowService.ToggleWindow(WindowType.Config);
        }
    }
}
