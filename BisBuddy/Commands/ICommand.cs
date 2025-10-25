using System.Collections.Generic;

namespace BisBuddy.Commands
{
    public interface ICommand
    {
        /// <summary>
        /// Returns the triggers that a user can provide to invoke this command.
        /// </summary>
        public IEnumerable<CommandTrigger> Triggers { get; }

        /// <summary>
        /// Returns the description of the command, which is displayed in the help menu.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Invokes the command with the given arguments.
        /// </summary>
        /// <param name="args">The user-provided argument to the main plugin command</param>
        public void Invoke(string args);
    }
}
