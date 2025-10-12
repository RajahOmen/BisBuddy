using BisBuddy.Commands;
using BisBuddy.Resources;
using BisBuddy.Util;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BisBuddy.Services
{
    public class CommandService : ICommandService
    {
        private readonly ICommandManager commandManager;
        private readonly ITypedLogger<CommandService> logger;
        private readonly IEnumerable<ICommand> commands;
        private readonly Dictionary<string, ICommand> commandHandlers;
        private readonly string commandDescriptions;

        public CommandService(
            ICommandManager commandManager,
            ITypedLogger<CommandService> logger,
            IEnumerable<ICommand> commands
            )
        {
            this.commandManager = commandManager;
            this.logger = logger;
            this.commands = commands;
            this.commandHandlers = buildCommandHandlers();
            this.commandDescriptions = buildCommandDescriptions();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            commandManager.AddHandler(Constants.FullChatCommand, new CommandInfo(onCommand)
            {
                ShowInHelp = true,
                HelpMessage = string.Format(Resource.MainChatCommandHelpMessage, commandDescriptions)
            });

            commandManager.AddHandler(Constants.ShortChatCommand, new CommandInfo(onCommand)
            {
                ShowInHelp = true,
                HelpMessage = string.Format(Resource.AliasChatCommandHelpMessage, Constants.FullChatCommand)
            });
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            commandManager.RemoveHandler(Constants.FullChatCommand);
            commandManager.RemoveHandler(Constants.ShortChatCommand);
            return Task.CompletedTask;
        }

        public void ExecuteCommand(string command, string args)
        {
            onCommand(command, args);
        }

        private void onCommand(string command, string args)
        {
            var subCommand = args.Split(' ')[0];
            if (commandHandlers.TryGetValue(subCommand, out var commandHandler))
            {
                commandHandler.Invoke(args);
                return;
            }
            logger.Debug($"Unknown command: \"{subCommand}\"");
        }

        private Dictionary<string, ICommand> buildCommandHandlers()
        {
            var handlers = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase);
            foreach (var command in commands)
                foreach (var trigger in command.Triggers)
                {
                    if (!handlers.TryAdd(trigger.FullString, command))
                        logger.Warning($"Command full trigger collision: \"{trigger.FullString}\"");
                    if (trigger.ShortString is string shortString)
                        if (!handlers.TryAdd(shortString, command))
                            logger.Warning($"Command short trigger collision: \"{shortString}\"");
                }

            return handlers;
        }
        
        private string buildCommandDescriptions()
        {
            var commandSpacer = "\n        ";
            List<string> commandDescriptions = [""];
            foreach (var command in commands)
            {
                List<string> triggerStrings = [];
                foreach (var trigger in command.Triggers)
                {
                    if (!string.IsNullOrEmpty(trigger.FullString))
                        triggerStrings.Add(trigger.FullString);
                    if (trigger.ShortString is string shortTrigger)
                        triggerStrings.Add(shortTrigger);
                }

                if (triggerStrings.Count == 0)
                    continue;

                var triggers = string.Join('/', triggerStrings);
                commandDescriptions.Add($"[{triggers}] - {command.Description}");
            }

            return string.Join(commandSpacer, commandDescriptions);
        }
    }

    public interface ICommandService : IHostedService
    {
        public void ExecuteCommand(string command, string args);
    }
}
