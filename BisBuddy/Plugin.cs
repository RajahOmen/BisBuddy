using Autofac;
using BisBuddy.Converters;
using BisBuddy.Gear;
using BisBuddy.Gear.Prerequisites;
using BisBuddy.Import;
using BisBuddy.Items;
using BisBuddy.Services;
using BisBuddy.Services.Addon;
using BisBuddy.Services.Addon.Containers;
using BisBuddy.Services.Addon.ShopExchange;
using BisBuddy.Services.Gearsets;
using BisBuddy.Services.ImportGearset;
using BisBuddy.Services.ItemAssignment;
using BisBuddy.Windows;
using BisBuddy.Windows.ConfigWindow;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using KamiToolKit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy;

public sealed partial class Plugin : IDalamudPlugin
{
    private readonly IHost host;
    public static readonly string PluginName = "BISBuddy";
    public static readonly int MaxGearsetCount = 25;

    // handle async FIFO item assignment

    // event listeners
    // shops and other dialogs etc.
    //public readonly MateriaAttachEventListener MateriaAttachEventListener;
    //public readonly NeedGreedEventListener NeedGreedEventListener;
    //public readonly ShopExchangeItemEventListener ShopExchangeItemEventListener;
    //public readonly ShopExchangeCurrencyEventListener ShopExchangeCurrencyEventListener;
    //public readonly ItemSearchEventListener ItemSearchEventListener;
    //public readonly ItemSearchResultEventListener ItemSearchResultEventListener;

    //// player inventory windows
    //public readonly InventoryEventListener InventoryEventListener;
    //public readonly InventoryLargeEventListener InventoryLargeEventListener;
    //public readonly InventoryExpansionEventListener InventoryExpansionEventListener;
    //public readonly InventoryRetainerEventListener InventoryRetainerEventListener;
    //public readonly InventoryRetainerLargeEventListener InventoryRetainerLargeEventListener;
    //public readonly InventoryBuddyEventListener InventoryBuddyEventListener;

    //// item tooltip
    //public readonly ItemDetailEventListener ItemDetailEventListener;

    //// non-addon related
    //public readonly InventoryItemEventListener ItemUpdateEventListener;
    //public readonly LoginLoadEventListener LoginLoadEventListener;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IChatGui chatGui,
        ICommandManager commandManager,
        IGameInventory gameInventory,
        IGameGui gameGui,
        IPluginLog pluginLog,
        IAddonLifecycle addonLifecycle,
        IDataManager dataManager,
        IClientState clientState
        )
    {
        host = new HostBuilder()
            .ConfigureLogging(lb =>
            {
                lb.ClearProviders();
                lb.SetMinimumLevel(LogLevel.Trace);
            })
            .ConfigureContainer<ContainerBuilder>(builder =>
            {
                builder.RegisterInstance(pluginInterface).AsSelf().SingleInstance();
                builder.RegisterInstance(pluginInterface.UiBuilder).AsSelf().SingleInstance();
                builder.RegisterInstance(chatGui).AsSelf().SingleInstance();
                builder.RegisterInstance(commandManager).AsSelf().SingleInstance();
                builder.RegisterInstance(gameInventory).AsSelf().SingleInstance();
                builder.RegisterInstance(gameGui).AsSelf().SingleInstance();
                builder.RegisterInstance(pluginLog).AsSelf().SingleInstance();
                builder.RegisterInstance(addonLifecycle).AsSelf().SingleInstance();
                builder.RegisterInstance(dataManager).AsSelf().SingleInstance();
                builder.RegisterInstance(clientState).AsSelf().SingleInstance();

                // item data service wrapper over game excel data
                builder.RegisterType<ItemDataService>().As<IItemDataService>().SingleInstance();

                // kamitoolkit
                builder.RegisterType<NativeController>().AsSelf().SingleInstance();

                // importing gearsets
                builder.RegisterType<HttpClient>().AsSelf().SingleInstance();
                builder.RegisterType<ImportGearsetService>().As<IImportGearsetService>().SingleInstance();
                builder.RegisterType<XivgearSource>().Keyed<IImportGearsetSource>(ImportGearsetSourceType.Xivgear).SingleInstance();
                builder.RegisterType<EtroSource>().Keyed<IImportGearsetSource>(ImportGearsetSourceType.Etro).SingleInstance();
                builder.RegisterType<TeamcraftPlaintextSource>().Keyed<IImportGearsetSource>(ImportGearsetSourceType.Teamcraft).SingleInstance();
                builder.RegisterType<JsonSource>().Keyed<IImportGearsetSource>(ImportGearsetSourceType.Json).SingleInstance();

                // de/serialization
                // register as string-keyed JsonConverter, and also the typed JsonConverter
                builder.RegisterType<GearpieceConverter>().As<JsonConverter>().As<JsonConverter<Gearpiece>>().SingleInstance();
                builder.RegisterType<MateriaConverter>().As<JsonConverter>().As<JsonConverter<Materia>>().SingleInstance();
                builder.RegisterType<PrerequisiteNodeConverter>().As<JsonConverter>().As<JsonConverter<IPrerequisiteNode>>().SingleInstance();
                builder.RegisterType<PrerequisiteAndNodeConverter>().As<JsonConverter>().As<JsonConverter<PrerequisiteAndNode>>().SingleInstance();
                builder.RegisterType<PrerequisiteAtomNodeConverter>().As<JsonConverter>().As<JsonConverter<PrerequisiteAtomNode>>().SingleInstance();
                builder.RegisterType<PrerequisiteOrNodeConverter>().As<JsonConverter>().As<JsonConverter<PrerequisiteOrNode>>().SingleInstance();

                // windows
                builder.RegisterType<MainWindow>().As<Window>().AsSelf().SingleInstance();
                builder.RegisterType<ConfigWindow>().As<Window>().AsSelf().SingleInstance();
                builder.RegisterType<ImportGearsetWindow>().As<Window>().SingleInstance();
                builder.RegisterType<MeldPlanSelectorWindow>().As<Window>().AsSelf().SingleInstance();
                builder.RegisterType<WindowService>().As<IWindowService>().SingleInstance();

                // event listener dependencies
                builder.RegisterType<AddonServiceDependencies>().AsSelf().SingleInstance();

            })
            // hosted services
            .ConfigureContainer<ContainerBuilder>(builder =>
            {
                // manage current gearsets, modifying providing and updating
                builder.RegisterType<IGearsetsService>().AsImplementedInterfaces().SingleInstance();

                // a FIFO queue for executing item assignment tasks off thread
                builder.RegisterType<QueueService>().AsImplementedInterfaces().SingleInstance();

                // plugin configuration
                builder.RegisterType<ConfigurationService>().AsImplementedInterfaces().SingleInstance();

                // commands
                builder.RegisterType<CommandService>().AsImplementedInterfaces().SingleInstance();

                // addon listeners
                //   inventories
                builder.RegisterType<InventoryBuddyService>().AsImplementedInterfaces().AsSelf().SingleInstance();
                builder.RegisterType<InventoryExpansionService>().AsImplementedInterfaces().AsSelf().SingleInstance();
                builder.RegisterType<InventoryLargeService>().AsImplementedInterfaces().AsSelf().SingleInstance();
                builder.RegisterType<InventoryRetainerLargeService>().AsImplementedInterfaces().AsSelf().SingleInstance();
                builder.RegisterType<InventoryRetainerService>().AsImplementedInterfaces().AsSelf().SingleInstance();
                builder.RegisterType<InventoryService>().AsImplementedInterfaces().AsSelf().SingleInstance();
                //   shops
                builder.RegisterType<ShopExchangeCurrencyService>().AsImplementedInterfaces().AsSelf().SingleInstance();
                builder.RegisterType<ShopExchangeItemService>().AsImplementedInterfaces().AsSelf().SingleInstance();
                //   tooltip
                builder.RegisterType<ItemDetailService>().AsImplementedInterfaces().AsSelf().SingleInstance();
                //   marketboard
                builder.RegisterType<ItemSearchService>().AsImplementedInterfaces().AsSelf().SingleInstance();
                builder.RegisterType<ItemSearchResultService>().AsImplementedInterfaces().AsSelf().SingleInstance();
                //   materia melding
                builder.RegisterType<MateriaAttachService>().AsImplementedInterfaces().AsSelf().SingleInstance();
                //   need greed
                builder.RegisterType<NeedGreedService>().AsImplementedInterfaces().AsSelf().SingleInstance();
            })
            .ConfigureServices(col =>
            {
                // register converters to json options
                col.AddOptions<JsonSerializerOptions>()
                .Configure<IItemDataService, IServiceProvider>((opts, itemData, serviceProvider) =>
                {
                    opts.PropertyNameCaseInsensitive = true;
                    opts.IncludeFields = true;

                    foreach (var converter in serviceProvider.GetServices<JsonConverter>())
                        opts.Converters.Add(converter);
                });
            }).Build();

        _ = host.StartAsync();



        //Services.HttpClient = new System.Net.Http.HttpClient();
        //Services.ImportGearsetService = new ImportGearsetService(this)
        //    .RegisterSource(ImportSourceType.Xivgear, new XivgearSource(ItemData, Services.HttpClient))
        //    .RegisterSource(ImportSourceType.Etro, new EtroSource(ItemData, Services.HttpClient))
        //    .RegisterSource(ImportSourceType.Teamcraft, new TeamcraftPlaintextSource(ItemData))
        //    .RegisterSource(ImportSourceType.Json, new JsonSource(jsonOptions));
        //Services.NativeController = new NativeController(pluginInterface);

        //Configuration = Configuration.LoadConfig(ItemData, jsonOptions);

        // INSTANTIATE WINDOWS
        //ConfigWindow = new ConfigWindow(this);
        //MainWindow = new MainWindow(this);
        //ImportGearsetWindow = new ImportGearsetWindow(this);
        //MeldPlanSelectorWindow = new MeldPlanSelectorWindow(this);

        //WindowSystem.AddWindow(ConfigWindow);
        //WindowSystem.AddWindow(MainWindow);
        //WindowSystem.AddWindow(ImportGearsetWindow);
        //WindowSystem.AddWindow(MeldPlanSelectorWindow);

        //Services.PluginInterface.UiBuilder.Draw += DrawUI;

        // INSTANTIATE LISTENERS
        // when characters log in (INSTANTIATE THIS FIRST)
        //LoginLoadEventListener = new LoginLoadEventListener(this);
        //// materia melding windows
        //MateriaAttachEventListener = new MateriaAttachEventListener(this);
        //// need/greed windows
        //NeedGreedEventListener = new NeedGreedEventListener(this);
        //// item exchange shop windows
        //ShopExchangeItemEventListener = new ShopExchangeItemEventListener(this);
        //// currency exchange shop windows
        //ShopExchangeCurrencyEventListener = new ShopExchangeCurrencyEventListener(this);
        //// inventory windows
        //InventoryEventListener = new InventoryEventListener(this);
        //InventoryLargeEventListener = new InventoryLargeEventListener(this);
        //InventoryExpansionEventListener = new InventoryExpansionEventListener(this);
        //InventoryRetainerEventListener = new InventoryRetainerEventListener(this);
        //InventoryRetainerLargeEventListener = new InventoryRetainerLargeEventListener(this);
        //InventoryBuddyEventListener = new InventoryBuddyEventListener(this);
        //// marketboard
        //ItemSearchEventListener = new ItemSearchEventListener(this);
        //ItemSearchResultEventListener = new ItemSearchResultEventListener(this);
        //// when item tooltips are shown
        //ItemDetailEventListener = new ItemDetailEventListener(this);
        //// when items added to inventory
        //ItemUpdateEventListener = new InventoryItemEventListener(this);

        //Services.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        //{
        //    HelpMessage = MainCommandHelpMessage,
        //    ShowInHelp = true,
        //});
        //Services.CommandManager.AddHandler(CommandNameAlias, new CommandInfo(OnCommand)
        //{
        //    HelpMessage = AliasCommandHelpMessage,
        //    ShowInHelp = true,
        //});

        //// This adds a button to the plugin installer entry of this plugin which allows
        //// to toggle the display status of the configuration ui
        //Services.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        //// Adds another button that is doing the same but for the main ui of the plugin
        //Services.PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
    }

    public void Dispose()
    {
        host.StopAsync().GetAwaiter().GetResult();
        host.Dispose();


        //Services.CommandManager.RemoveHandler(CommandName);
        //Services.CommandManager.RemoveHandler(CommandNameAlias);

        //WindowSystem.RemoveAllWindows();

        //// Dispose of plugin windows
        //ConfigWindow.Dispose();
        //MainWindow.Dispose();
        //ImportGearsetWindow.Dispose();
        //MeldPlanSelectorWindow.Dispose();

        //Services.PluginInterface.UiBuilder.Draw -= DrawUI;

        // Dispose of listeners
        //MateriaAttachEventListener.Dispose();
        //NeedGreedEventListener.Dispose();
        //ShopExchangeItemEventListener.Dispose();
        //ShopExchangeCurrencyEventListener.Dispose();
        //ItemDetailEventListener.Dispose();
        //InventoryEventListener.Dispose();
        //InventoryLargeEventListener.Dispose();
        //InventoryExpansionEventListener.Dispose();
        //InventoryRetainerEventListener.Dispose();
        //InventoryRetainerLargeEventListener.Dispose();
        //InventoryBuddyEventListener.Dispose();
        //ItemSearchResultEventListener.Dispose();
        //ItemSearchEventListener.Dispose();
        //ItemUpdateEventListener.Dispose();

        //// dispose last (probably)
        //LoginLoadEventListener.Dispose();

        // stop worker thread
        //itemAssignmentQueue.Stop();

        // dispose of http client
        //Services.HttpClient.Dispose();

        //// dispose of native UI controller
        //Services.NativeController.Dispose();
    }

    public delegate void SelectedMeldPlanIdxChangeHandler(int newIdx);

    public event SelectedMeldPlanIdxChangeHandler? OnSelectedMeldPlanIdxChange;

    public void TriggerSelectedMeldPlanChange(int newIdx)
    {
        OnSelectedMeldPlanIdxChange?.Invoke(newIdx);
    }

    //public void SaveGearsetsWithUpdate(bool pluginChange = false)
    //{
    //    // save gearsets to configuration and trigger update event
    //    SaveConfiguration(pluginChange);
    //    TriggerGearsetsUpdate();
    //}

    //public void UpdateMeldPlanSelectorWindow(List<MeldPlan> meldPlans)
    //{
    //    MeldPlanSelectorWindow.MeldPlans = meldPlans;
    //    MeldPlanSelectorWindow.IsOpen = meldPlans.Count > 0;
    //}

    public static unsafe void SearchItemById(uint itemId)
    {
        try
        {
            pluginLog.Debug($"Searching for item \"{itemId}\"");
            ItemFinderModule.Instance()->SearchForItem(itemId, false);
        }
        catch (Exception ex)
        {
            pluginLog.Error(ex, $"Error searching for \"{itemId}\"");
        }
    }
}
