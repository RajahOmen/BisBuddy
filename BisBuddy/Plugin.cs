using Autofac;
using BisBuddy.Converters;
using BisBuddy.Factories;
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
using BisBuddy.Windows.Config;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiToolKit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Networking.Http;
using Autofac.Extensions.DependencyInjection;
using Dalamud.Interface;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Reflection.Metadata.Ecma335;
using BisBuddy.Gear.MeldPlanManager;

namespace BisBuddy;

public sealed partial class Plugin : IDalamudPlugin
{
    private readonly IHost host;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IChatGui chatGui,
        ICommandManager commandManager,
        IGameInventory gameInventory,
        IGameGui gameGui,
        IPluginLog pluginLog,
        IAddonLifecycle addonLifecycle,
        IDataManager dataManager,
        IClientState clientState,
        IFramework framework
        )
    {
        host = new HostBuilder()
            .UseContentRoot(pluginInterface.ConfigDirectory.FullName)
            .ConfigureLogging(lb =>
            {
                lb.ClearProviders();
                lb.SetMinimumLevel(LogLevel.Trace);
            })
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .ConfigureContainer<ContainerBuilder>(builder =>
            {
                builder.RegisterInstance(pluginInterface).As<IDalamudPluginInterface>().SingleInstance();
                builder.RegisterInstance(pluginInterface.UiBuilder).As<IUiBuilder>().SingleInstance();
                builder.RegisterInstance(chatGui).As<IChatGui>().SingleInstance();
                builder.RegisterInstance(commandManager).As<ICommandManager>().SingleInstance();
                builder.RegisterInstance(gameInventory).As<IGameInventory>().SingleInstance();
                builder.RegisterInstance(gameGui).As<IGameGui>().SingleInstance();
                builder.RegisterInstance(pluginLog).As<IPluginLog>().SingleInstance();
                builder.RegisterInstance(addonLifecycle).As<IAddonLifecycle>().SingleInstance();
                builder.RegisterInstance(dataManager).As<IDataManager>().SingleInstance();
                builder.RegisterInstance(clientState).As<IClientState>().SingleInstance();
                builder.RegisterInstance(framework).As<IFramework>().SingleInstance();

                // more rich logging information
                builder.RegisterGeneric(typeof(TypedLogger<>)).As(typeof(ITypedLogger<>)).InstancePerDependency();

                // item data service wrapper over game excel data
                builder.RegisterType<ItemDataService>().As<IItemDataService>().SingleInstance();

                // kamitoolkit
                builder.RegisterType<NativeController>().AsSelf().SingleInstance();

                // importing gearsets
                builder.RegisterType<HappyEyeballsCallback>().AsSelf().SingleInstance();
                builder.Register(
                    c => new HttpClient(
                        new SocketsHttpHandler
                        {
                            ConnectCallback = c.Resolve<HappyEyeballsCallback>().ConnectCallback
                        })
                    ).As<HttpClient>()
                    .SingleInstance();
                builder.RegisterType<ImportGearsetService>().As<IImportGearsetService>().SingleInstance();
                builder.RegisterType<XivgearSource>().As<IImportGearsetSource>().SingleInstance();
                builder.RegisterType<EtroSource>().As<IImportGearsetSource>().SingleInstance();
                builder.RegisterType<TeamcraftPlaintextSource>().As<IImportGearsetSource>().SingleInstance();
                builder.RegisterType<JsonSource>().As<IImportGearsetSource>().SingleInstance();

                // creating runtime factories
                builder.RegisterType<GearpieceFactory>().As<IGearpieceFactory>().SingleInstance();

                // de/serialization
                builder.RegisterType<FileSystem>().As<IFileSystem>().SingleInstance();
                builder.RegisterType<FileService>().As<IFileService>().SingleInstance();
                builder.RegisterType<ConfigurationLoaderService>().As<IConfigurationLoaderService>().SingleInstance();

                // options for the serializer
                builder.Register((c) =>
                {
                    JsonSerializerOptions jsonSerializerOptions = new()
                    {
                        PropertyNameCaseInsensitive = true,
                        IncludeFields = true
                    };

                    var converters = c.Resolve<IEnumerable<JsonConverter>>();
                    foreach (var converter in converters)
                        jsonSerializerOptions.Converters.Add(converter);

                    return jsonSerializerOptions;
                }).As<JsonSerializerOptions>()
                .InstancePerDependency();

                builder.RegisterType<GearpieceConverter>().As<JsonConverter>().As<JsonConverter<Gearpiece>>().SingleInstance();
                builder.RegisterType<MateriaConverter>().As<JsonConverter>().As<JsonConverter<Materia>>().SingleInstance();
                builder.RegisterType<PrerequisiteNodeConverter>().As<JsonConverter>().As<JsonConverter<IPrerequisiteNode>>().SingleInstance();
                builder.RegisterType<PrerequisiteAndNodeConverter>().As<JsonConverter>().As<JsonConverter<PrerequisiteAndNode>>().SingleInstance();
                builder.RegisterType<PrerequisiteAtomNodeConverter>().As<JsonConverter>().As<JsonConverter<PrerequisiteAtomNode>>().SingleInstance();
                builder.RegisterType<PrerequisiteOrNodeConverter>().As<JsonConverter>().As<JsonConverter<PrerequisiteOrNode>>().SingleInstance();

                // windows
                builder.RegisterType<MainWindow>().As<Window>().AsSelf().SingleInstance();
                builder.RegisterType<ConfigWindow>().As<Window>().AsSelf().SingleInstance();
                builder.RegisterType<ImportGearsetWindow>().As<Window>().AsSelf().SingleInstance();
                builder.RegisterType<MeldPlanSelectorWindow>().As<Window>().AsSelf().SingleInstance();

                // display update count on inventory window
                builder.RegisterType<InventoryUpdateDisplayService>().As<IInventoryUpdateDisplayService>().SingleInstance();

                // display and pick meld plans
                builder.RegisterType<MeldPlanService>().As<IMeldPlanService>().SingleInstance();

                // event listener dependencies
                builder.RegisterGeneric(typeof(AddonServiceDependencies<>)).AsSelf().InstancePerDependency();

                // item assignment solver
                builder.RegisterType<ItemAssignmentSolverFactory>().As<IItemAssignmentSolverFactory>().SingleInstance();
            })
            // hosted services
            .ConfigureContainer<ContainerBuilder>(builder =>
            {
                // manages windows
                builder.RegisterType<WindowService>().AsImplementedInterfaces().SingleInstance();

                // manage current gearsets
                builder.RegisterType<GearsetsService>().AsImplementedInterfaces().SingleInstance();

                // handles changes to player inventories
                builder.RegisterType<InventoryChangeService>().AsImplementedInterfaces().SingleInstance();

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
                .Configure<IServiceProvider>((opts, serviceProvider) =>
                {
                    opts.PropertyNameCaseInsensitive = true;
                    opts.IncludeFields = true;

                    foreach (var converter in serviceProvider.GetServices<JsonConverter>())
                        opts.Converters.Add(converter);
                });
            }).Build();

        _ = host.StartAsync();
    }

    public void Dispose()
    {
        host.StopAsync().GetAwaiter().GetResult();
        host.Dispose();
    }
}
