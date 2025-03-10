using BisBuddy.Import;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Net.Http;

namespace BisBuddy
{
    // thanks to @midorikami for this pattern
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public sealed class Services
    {
        [PluginService] public static IDalamudPluginInterface PluginInterface { get; set; }
        [PluginService] public static IChatGui ChatGui { get; set; }
        [PluginService] public static ICommandManager CommandManager { get; set; }
        [PluginService] public static IGameInventory GameInventory { get; set; }
        [PluginService] public static IGameGui GameGui { get; set; }
        [PluginService] public static IPluginLog Log { get; set; }
        [PluginService] public static IAddonLifecycle AddonLifecycle { get; set; }
        [PluginService] public static IDataManager DataManager { get; set; }
        [PluginService] public static IClientState ClientState { get; set; }
        public static ImportGearsetService ImportGearsetService { get; set; }
        public static HttpClient HttpClient { get; set; }
    }
}
