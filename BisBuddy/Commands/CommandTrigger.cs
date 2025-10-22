namespace BisBuddy.Commands
{
    /// <summary>
    /// Represents a command trigger that can be used to invoke commands in the plugin.
    /// </summary>
    public readonly struct CommandTrigger(string fullString, string? shortString = null)
    {
        public readonly string FullString = fullString;
        public readonly string? ShortString = shortString;
    }
}
