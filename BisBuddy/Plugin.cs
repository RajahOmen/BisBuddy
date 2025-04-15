using BisBuddy.Converters;
using BisBuddy.EventListeners;
using BisBuddy.EventListeners.AddonEventListeners;
using BisBuddy.EventListeners.AddonEventListeners.Containers;
using BisBuddy.EventListeners.AddonEventListeners.ShopExchange;
using BisBuddy.Gear;
using BisBuddy.Import;
using BisBuddy.ItemAssignment;
using BisBuddy.Items;
using BisBuddy.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using KamiToolKit;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace BisBuddy;

public sealed partial class Plugin : IDalamudPlugin
{
    public static readonly string PluginName = "BISBuddy";
    public static readonly int MaxGearsetCount = 25;
    private readonly JsonSerializerOptions jsonOptions;

    private const string CommandName = "/bisbuddy";
    private const string CommandNameAlias = "/bis";
    private static readonly string MainCommandHelpMessage = $"View existing or add new gearsets\n      [config/c] - Open {PluginName} configuration\n      [new/n] - Add a new gearset";
    private static readonly string AliasCommandHelpMessage = "Alias for /bisbuddy";

    private ulong playerContentId = 0;
    public ulong PlayerContentId
    {
        get => playerContentId;
        set
        {
            playerContentId = value;

            // update Gearsets to new character's gearsets
            Gearsets = Configuration.GetCharacterGearsets(value, jsonOptions);
        }
    }
    public Configuration Configuration { get; set; }
    public List<Gearset> Gearsets { get; private set; } = [];
    // handle async FIFO item assignment
    public ItemAssignmentQueue itemAssignmentQueue { get; private set; } = new();

    // plugin windows
    public readonly WindowSystem WindowSystem = new(PluginName);
    private ConfigWindow ConfigWindow { get; init; }
    public MainWindow MainWindow { get; init; }
    private ImportGearsetWindow ImportGearsetWindow { get; init; }
    private MeldPlanSelectorWindow MeldPlanSelectorWindow { get; init; }

    // event listeners
    // shops and other dialogs etc.
    public readonly MateriaAttachEventListener MateriaAttachEventListener;
    public readonly NeedGreedEventListener NeedGreedEventListener;
    public readonly ShopExchangeItemEventListener ShopExchangeItemEventListener;
    public readonly ShopExchangeCurrencyEventListener ShopExchangeCurrencyEventListener;
    public readonly ItemSearchEventListener ItemSearchEventListener;
    public readonly ItemSearchResultEventListener ItemSearchResultEventListener;

    // player inventory windows
    public readonly InventoryEventListener InventoryEventListener;
    public readonly InventoryLargeEventListener InventoryLargeEventListener;
    public readonly InventoryExpansionEventListener InventoryExpansionEventListener;
    public readonly InventoryRetainerEventListener InventoryRetainerEventListener;
    public readonly InventoryRetainerLargeEventListener InventoryRetainerLargeEventListener;
    public readonly InventoryBuddyEventListener InventoryBuddyEventListener;

    // item tooltip
    public readonly ItemDetailEventListener ItemDetailEventListener;

    // non-addon related
    public readonly InventoryItemEventListener ItemUpdateEventListener;
    public readonly LoginLoadEventListener LoginLoadEventListener;

    // game data item sheet wrapper
    public ItemData ItemData { get; init; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        // instantiate services
        pluginInterface.Create<Services>();

        ItemData = new ItemData(Services.DataManager.Excel);

        jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
        };
        jsonOptions.Converters.Add(new GearpieceConverter(ItemData));
        jsonOptions.Converters.Add(new MateriaConverter(ItemData));
        jsonOptions.Converters.Add(new PrerequisiteNodeConverter());
        jsonOptions.Converters.Add(new PrerequisiteAndNodeConverter(ItemData));
        jsonOptions.Converters.Add(new PrerequisiteAtomNodeConverter(ItemData));
        jsonOptions.Converters.Add(new PrerequisiteOrNodeConverter(ItemData));

        Services.HttpClient = new System.Net.Http.HttpClient();
        Services.ImportGearsetService = new ImportGearsetService(this)
            .RegisterSource(ImportSourceType.Xivgear, new XivgearSource(ItemData, Services.HttpClient))
            .RegisterSource(ImportSourceType.Etro, new EtroSource(ItemData, Services.HttpClient))
            .RegisterSource(ImportSourceType.Teamcraft, new TeamcraftPlaintextSource(ItemData))
            .RegisterSource(ImportSourceType.Json, new JsonSource(jsonOptions));
        Services.NativeController = new NativeController(pluginInterface);

        Configuration = Configuration.LoadConfig(ItemData, jsonOptions);

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
            HelpMessage = MainCommandHelpMessage,
            ShowInHelp = true,
        });
        Services.CommandManager.AddHandler(CommandNameAlias, new CommandInfo(OnCommand)
        {
            HelpMessage = AliasCommandHelpMessage,
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

        // dispose of http client
        Services.HttpClient.Dispose();

        // dispose of native UI controller
        Services.NativeController.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        if (args == "config" || args == "c")
        {
            ToggleConfigUI();
        }
        else if (args == "new" || args == "n")
        {
            ToggleImportGearsetUI();
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

    public void SaveGearsetsWithUpdate(bool pluginChange = false)
    {
        // save gearsets to configuration and trigger update event
        SaveConfiguration(pluginChange);
        TriggerGearsetsUpdate();
    }

    public void UpdateMeldPlanSelectorWindow(List<MeldPlan> meldPlans)
    {
        MeldPlanSelectorWindow.MeldPlans = meldPlans;
        MeldPlanSelectorWindow.IsOpen = meldPlans.Count > 0;
    }

    public static unsafe void SearchItemById(uint itemId)
    {
        try
        {
            Services.Log.Debug($"Searching for item \"{itemId}\"");
            ItemFinderModule.Instance()->SearchForItem(itemId, false);
        }
        catch (Exception ex)
        {
            Services.Log.Error(ex, $"Error searching for \"{itemId}\"");

        }
    }

    public void SaveConfiguration(bool pluginUpdate = false)
    {
        if (Configuration.PluginUpdateInventoryScan && pluginUpdate)
            ScheduleUpdateFromInventory(Gearsets, saveChanges: true);
        else
            Configuration.Save(jsonOptions);
    }

    public string ExportGearsetToJsonStr(Gearset gearset)
    {
        return JsonSerializer.Serialize(gearset, jsonOptions);
    }
}
