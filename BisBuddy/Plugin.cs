using BisBuddy.EventListeners;
using BisBuddy.EventListeners.AddonEventListeners;
using BisBuddy.EventListeners.AddonEventListeners.Containers;
using BisBuddy.EventListeners.AddonEventListeners.ShopExchange;
using BisBuddy.Gear;
using BisBuddy.ItemAssignment;
using BisBuddy.Items;
using BisBuddy.Windows;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BisBuddy;

public sealed partial class Plugin : IDalamudPlugin
{
    public static readonly string PluginName = "BISBuddy";
    public string PluginVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
    public static readonly int MaxGearsetCount = 25;

    private const string CommandName = "/bisbuddy";
    private const string CommandNameAlias = "/bis";

    private ulong playerContentId = 0;
    public ulong PlayerContentId
    {
        get => playerContentId;
        set
        {
            playerContentId = value;

            // update Gearsets to new character's gearsets
            Gearsets = Configuration.GetCharacterGearsets(value);
        }
    }
    public Configuration Configuration { get; set; }
    public List<Gearset> Gearsets { get; private set; } = [];
    // handle async FIFO item assignment
    internal ItemAssignmentQueue itemAssignmentQueue { get; private set; } = new();

    // plugin windows
    public readonly WindowSystem WindowSystem = new(PluginName);
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private ImportGearsetWindow ImportGearsetWindow { get; init; }
    private MeldPlanSelectorWindow MeldPlanSelectorWindow { get; init; }

    // event listeners
    // shops and other dialogs etc.
    internal readonly MateriaAttachEventListener MateriaAttachEventListener;
    internal readonly NeedGreedEventListener NeedGreedEventListener;
    internal readonly ShopExchangeItemEventListener ShopExchangeItemEventListener;
    internal readonly ShopExchangeCurrencyEventListener ShopExchangeCurrencyEventListener;
    internal readonly ItemSearchEventListener ItemSearchEventListener;
    internal readonly ItemSearchResultEventListener ItemSearchResultEventListener;

    // player inventory windows
    internal readonly InventoryEventListener InventoryEventListener;
    internal readonly InventoryLargeEventListener InventoryLargeEventListener;
    internal readonly InventoryExpansionEventListener InventoryExpansionEventListener;
    internal readonly InventoryRetainerEventListener InventoryRetainerEventListener;
    internal readonly InventoryRetainerLargeEventListener InventoryRetainerLargeEventListener;
    internal readonly InventoryBuddyEventListener InventoryBuddyEventListener;

    // item tooltip
    internal readonly ItemDetailEventListener ItemDetailEventListener;

    // non-addon related
    internal readonly InventoryItemEventListener ItemUpdateEventListener;
    internal readonly LoginLoadEventListener LoginLoadEventListener;

    // game data item sheet wrapper
    public ItemData ItemData { get; init; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        // instantiate services
        pluginInterface.Create<Services>();

        try
        {
            Configuration = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        }
        catch (Exception ex)
        {
            Services.Log.Error(ex, "Failed to load configuration");
            throw;
        }

        ItemData = new ItemData(Services.DataManager.Excel);

        // INSTANTIATE WINDOWS
        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);
        ImportGearsetWindow = new ImportGearsetWindow(this);
        MeldPlanSelectorWindow = new MeldPlanSelectorWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ImportGearsetWindow);
        WindowSystem.AddWindow(MeldPlanSelectorWindow);

        Services.PluginInterface.UiBuilder.Draw += DrawUI;

        // INSTANTIATE LISTENERS
        // when characters log in (INSTANTIATE THIS FIRST)
        LoginLoadEventListener = new LoginLoadEventListener(this);
        // materia melding windows
        MateriaAttachEventListener = new MateriaAttachEventListener(this);
        // need/greed windows
        NeedGreedEventListener = new NeedGreedEventListener(this);
        // item exchange shop windows
        ShopExchangeItemEventListener = new ShopExchangeItemEventListener(this);
        // currency exchange shop windows
        ShopExchangeCurrencyEventListener = new ShopExchangeCurrencyEventListener(this);
        // inventory windows
        InventoryEventListener = new InventoryEventListener(this);
        InventoryLargeEventListener = new InventoryLargeEventListener(this);
        InventoryExpansionEventListener = new InventoryExpansionEventListener(this);
        InventoryRetainerEventListener = new InventoryRetainerEventListener(this);
        InventoryRetainerLargeEventListener = new InventoryRetainerLargeEventListener(this);
        InventoryBuddyEventListener = new InventoryBuddyEventListener(this);
        // marketboard
        ItemSearchEventListener = new ItemSearchEventListener(this);
        ItemSearchResultEventListener = new ItemSearchResultEventListener(this);
        // when item tooltips are shown
        ItemDetailEventListener = new ItemDetailEventListener(this);
        // when items added to inventory
        ItemUpdateEventListener = new InventoryItemEventListener(this);

        Services.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = $"View existing or add new gearsets\n      [config/c] - Open {PluginName} configuration",
            ShowInHelp = true,
        });
        Services.CommandManager.AddHandler(CommandNameAlias, new CommandInfo(OnCommand)
        {
            HelpMessage = $"Alias for /bisbuddy",
            ShowInHelp = true,
        });

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        Services.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        Services.PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
    }

    public void Dispose()
    {
        Services.CommandManager.RemoveHandler(CommandName);
        Services.CommandManager.RemoveHandler(CommandNameAlias);

        WindowSystem.RemoveAllWindows();

        // Dispose of plugin windows
        ConfigWindow.Dispose();
        MainWindow.Dispose();
        ImportGearsetWindow.Dispose();
        MeldPlanSelectorWindow.Dispose();

        Services.PluginInterface.UiBuilder.Draw -= DrawUI;

        // Dispose of listeners
        MateriaAttachEventListener.Dispose();
        NeedGreedEventListener.Dispose();
        ShopExchangeItemEventListener.Dispose();
        ShopExchangeCurrencyEventListener.Dispose();
        ItemDetailEventListener.Dispose();
        InventoryEventListener.Dispose();
        InventoryLargeEventListener.Dispose();
        InventoryExpansionEventListener.Dispose();
        InventoryRetainerEventListener.Dispose();
        InventoryRetainerLargeEventListener.Dispose();
        InventoryBuddyEventListener.Dispose();
        ItemSearchResultEventListener.Dispose();
        ItemSearchEventListener.Dispose();
        ItemUpdateEventListener.Dispose();

        // dispose last (probably)
        LoginLoadEventListener.Dispose();

        // stop worker thread
        itemAssignmentQueue.Stop();
    }

    private void OnCommand(string command, string args)
    {
        if (args == "config" || args == "c")
        {
            ToggleConfigUI();
        }
        else
        {
            ToggleMainUI();
        }
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
    public void ToggleImportGearsetUI() => ImportGearsetWindow.Toggle();

    public delegate void GearsetsUpdateHandler();

    public event GearsetsUpdateHandler? OnGearsetsUpdate;

    public void TriggerGearsetsUpdate()
    {
        OnGearsetsUpdate?.Invoke();
    }

    public delegate void SelectedMeldPlanIdxChangeHandler(int newIdx);

    public event SelectedMeldPlanIdxChangeHandler? OnSelectedMeldPlanIdxChange;

    public void TriggerSelectedMeldPlanChange(int newIdx)
    {
        OnSelectedMeldPlanIdxChange?.Invoke(newIdx);
    }

    public void SaveGearsetsWithUpdate()
    {
        // save gearsets to configuration and trigger update event
        Configuration.Save();
        TriggerGearsetsUpdate();
    }

    internal void UpdateMeldPlanSelectorWindow(List<MeldPlan> meldPlans)
    {

        if (meldPlans.Count == 0)
        {
            if (MeldPlanSelectorWindow.IsOpen)
            {
                MeldPlanSelectorWindow.Toggle();
            }
        }
        else
        {
            MeldPlanSelectorWindow.MeldPlans = meldPlans;
            if (!MeldPlanSelectorWindow.IsOpen)
            {
                MeldPlanSelectorWindow.Toggle();
            }
        }
    }

    internal static void LinkItemById(uint itemId)
    {
        Services.Log.Debug($"Linking item \"{itemId}\" in chat");
        var itemIsHq = itemId > ItemData.ItemIdHqOffset;
        var shiftedItemId = itemIsHq ? itemId - ItemData.ItemIdHqOffset : itemId;
        var itemLink = SeString.CreateItemLink(shiftedItemId, itemIsHq);
        var entry = new XivChatEntry()
        {
            Message = itemLink,
            Name = SeString.Empty,
            Type = XivChatType.Echo,
        };
        Services.ChatGui.Print(entry);
    }
}
