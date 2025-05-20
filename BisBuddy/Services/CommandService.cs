using BisBuddy.Resources;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace BisBuddy.Services
{
    public class CommandService : ICommandService
    {
        private readonly ICommandManager commandManager;
        private readonly IWindowService windowService;

        public CommandService(
            ICommandManager commandManager,
            IWindowService windowService
            )
        {
            this.commandManager = commandManager;
            this.windowService = windowService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            commandManager.AddHandler(Resource.MainChatCommandName, new CommandInfo(onCommand)
            {
                ShowInHelp = true,
                HelpMessage = string.Format(Resource.MainChatCommandHelpMessage, Resource.PluginDisplayName)
            });

            commandManager.AddHandler(Resource.AliasChatCommandName, new CommandInfo(onCommand)
            {
                ShowInHelp = true,
                HelpMessage = string.Format(Resource.AliasChatCommandHelpMessage, Resource.MainChatCommandName)
            });
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            commandManager.RemoveHandler(Resource.MainChatCommandName);
            commandManager.RemoveHandler(Resource.AliasChatCommandName);
            return Task.CompletedTask;
        }

        private void onCommand(string command, string args)
        {
            if (args == "config" || args == "c")
                windowService.ToggleConfigWindow();
            else if (args == "new" || args == "n")
                windowService.ToggleImportGearsetWindow();
            else
                windowService.ToggleMainWindow();
        }
    }

    public interface ICommandService : IHostedService
    {

    }
}
