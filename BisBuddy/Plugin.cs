using Autofac;
using Autofac.Extensions.DependencyInjection;
using Autofac.Features.AttributeFilters;
using BisBuddy.Commands;
using BisBuddy.Converters;
using BisBuddy.Factories;
using BisBuddy.Gear;
using BisBuddy.Gear.Melds;
using BisBuddy.Gear.Prerequisites;
using BisBuddy.Items;
using BisBuddy.Mappers;
using BisBuddy.Mediators;
using BisBuddy.Services;
using BisBuddy.Services.Addon;
using BisBuddy.Services.Addon.Containers;
using BisBuddy.Services.Addon.ShopExchange;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
using BisBuddy.Services.ImportGearset;
using BisBuddy.Ui.Renderers;
using BisBuddy.Ui.Renderers.Components;
using BisBuddy.Ui.Renderers.ContextMenus;
using BisBuddy.Ui.Renderers.Tabs;
using BisBuddy.Ui.Renderers.Tabs.Config;
using BisBuddy.Ui.Renderers.Tabs.Main;
using BisBuddy.Ui.Windows;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Networking.Http;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiToolKit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BisBuddy;

public sealed partial class Plugin : IDalamudPlugin
{
    private readonly IHost host;
    private readonly ITypedLogger<Plugin> logger;

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
        ITextureProvider textureProvider,
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
                builder.RegisterInstance(textureProvider).As<ITextureProvider>().SingleInstance();
                builder.RegisterInstance(framework).As<IFramework>().SingleInstance();

                // wrap item finder module into service
                builder.RegisterType<ItemFinderService>().As<IItemFinderService>().SingleInstance();

                // more rich logging information
                builder.RegisterGeneric(typeof(TypedLogger<>)).As(typeof(ITypedLogger<>)).InstancePerDependency();

                // commands
                builder.RegisterType<OpenMainCommand>().AsImplementedInterfaces().SingleInstance();
                builder.RegisterType<OpenConfigCommand>().AsImplementedInterfaces().SingleInstance();
                builder.RegisterType<AddGearsetCommand>().AsImplementedInterfaces().SingleInstance();

                // item data service wrapper over game excel data
                builder.RegisterType<ItemDataService>().As<IItemDataService>().SingleInstance();

                // kamitoolkit
                builder.RegisterType<NativeController>().AsSelf().SingleInstance();

                // memory cache
                builder.RegisterType<MemoryCache>().As<IMemoryCache>().SingleInstance();

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
                builder.RegisterType<GearsetFactory>().As<IGearsetFactory>().SingleInstance();
                builder.RegisterType<ItemAssignmentSolverFactory>().As<IItemAssignmentSolverFactory>().SingleInstance();
                builder.RegisterType<MateriaFactory>().As<IMateriaFactory>().SingleInstance();
                builder.RegisterType<ContextMenuEntryFactory>().As<IContextMenuEntryFactory>().SingleInstance();

                // de/serialization
                builder.RegisterType<FileSystem>().As<IFileSystem>().SingleInstance();
                builder.RegisterType<FileService>().As<IFileService>().SingleInstance();
                builder.RegisterType<ConfigurationLoaderService>().As<IConfigurationLoaderService>().SingleInstance();

                // cached attribute retrieval
                builder.RegisterType<AttributeService>().As<IAttributeService>().SingleInstance();

                // mappers
                builder.RegisterType<GearpieceTypeMapper>().AsImplementedInterfaces().SingleInstance();

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

                // converters
                builder.RegisterType<GearpieceConverter>().As<JsonConverter>().As<JsonConverter<Gearpiece>>().SingleInstance();
                builder.RegisterType<MateriaGroupConverter>().As<JsonConverter>().As<JsonConverter<MateriaGroup>>().SingleInstance();
                builder.RegisterType<MateriaConverter>().As<JsonConverter>().As<JsonConverter<Materia>>().SingleInstance();
                builder.RegisterType<PrerequisiteNodeConverter>().As<JsonConverter>().As<JsonConverter<IPrerequisiteNode>>().SingleInstance();
                builder.RegisterType<PrerequisiteAndNodeConverter>().As<JsonConverter>().As<JsonConverter<PrerequisiteAndNode>>().SingleInstance();
                builder.RegisterType<PrerequisiteAtomNodeConverter>().As<JsonConverter>().As<JsonConverter<PrerequisiteAtomNode>>().SingleInstance();
                builder.RegisterType<PrerequisiteOrNodeConverter>().As<JsonConverter>().As<JsonConverter<PrerequisiteOrNode>>().SingleInstance();
                builder.RegisterType<GearsetsListConverter>().As<JsonConverter>().As<JsonConverter<List<Gearset>>>().SingleInstance();
                builder.RegisterType<GearsetConverter>().As<JsonConverter>().As<JsonConverter<Gearset>>().SingleInstance();

                // windows
                builder.RegisterType<WindowSystem>().AsSelf().SingleInstance();

                var windowScopeTag = "WindowScope";

                builder.RegisterType<MainWindow>().AsSelf().InstancePerMatchingLifetimeScope(windowScopeTag);
                builder.RegisterType<ConfigWindow>().AsSelf().InstancePerMatchingLifetimeScope(windowScopeTag).WithAttributeFiltering();
                builder.RegisterType<ImportGearsetWindow>().AsSelf().InstancePerMatchingLifetimeScope(windowScopeTag);
                builder.RegisterType<MeldPlanSelectorWindow>().AsSelf().InstancePerMatchingLifetimeScope(windowScopeTag);

                builder.Register(resolveWithScopeTagged<MainWindow>(windowScopeTag)).Keyed<Func<Window>>(WindowType.Main).SingleInstance();
                builder.Register(resolveWithScopeTagged<ConfigWindow>(windowScopeTag)).Keyed<Func<Window>>(WindowType.Config).SingleInstance();
                builder.Register(resolveWithScopeTagged<ImportGearsetWindow>(windowScopeTag)).Keyed<Func<Window>>(WindowType.ImportGearset).SingleInstance();
                builder.Register(resolveWithScopeTagged<MeldPlanSelectorWindow>(windowScopeTag)).Keyed<Func<Window>>(WindowType.MeldPlanSelector).SingleInstance();

                // main window tabs
                builder.RegisterType<UserGearsetsTab>().Keyed<TabRenderer<MainWindowTab>>(MainWindowTab.UserGearsets).InstancePerMatchingLifetimeScope(windowScopeTag);
                //builder.RegisterType<ItemPlannerTab>().Keyed<TabRenderer>(MainWindowTab.ItemPlanner).InstancePerMatchingLifetimeScope(windowScopeTag);
                //builder.RegisterType<ItemExchangesTab>().Keyed<TabRenderer>(MainWindowTab.ItemExchanges).InstancePerMatchingLifetimeScope(windowScopeTag);
                //builder.RegisterType<ItemTrackerTab>().Keyed<TabRenderer>(MainWindowTab.ItemTracker).InstancePerMatchingLifetimeScope(windowScopeTag);
                builder.RegisterType<ConfigTab>().Keyed<TabRenderer<MainWindowTab>>(MainWindowTab.PluginConfig).InstancePerMatchingLifetimeScope(windowScopeTag);
                builder.RegisterType<DebugTab>().Keyed<TabRenderer<MainWindowTab>>(MainWindowTab.PluginDebug).InstancePerMatchingLifetimeScope(windowScopeTag);


                // config window tabs
                builder.RegisterType<GeneralSettingsTab>().Keyed<TabRenderer<ConfigWindowTab>>(ConfigWindowTab.General).InstancePerMatchingLifetimeScope(windowScopeTag);
                builder.RegisterType<HighlightingSettingsTab>().Keyed<TabRenderer<ConfigWindowTab>>(ConfigWindowTab.Highlighting).InstancePerMatchingLifetimeScope(windowScopeTag);
                builder.RegisterType<InventorySettingsTab>().Keyed<TabRenderer<ConfigWindowTab>>(ConfigWindowTab.Inventory).InstancePerMatchingLifetimeScope(windowScopeTag);
                builder.RegisterType<UiThemeSettingsTab>().Keyed<TabRenderer<ConfigWindowTab>>(ConfigWindowTab.UiTheme).InstancePerMatchingLifetimeScope(windowScopeTag);
                //builder.RegisterType<DebugSettingsTab>().Keyed<TabRenderer<ConfigWindowTab>>(ConfigWindowTab.Debug).InstancePerMatchingLifetimeScope(windowScopeTag);

                // other ui elements
                builder.RegisterType<UiComponents>().AsSelf().SingleInstance();

                // renderer factory
                builder.RegisterType<CachingRendererFactory>().As<IRendererFactory>().InstancePerMatchingLifetimeScope(windowScopeTag);

                List<Type> renderers = [
                    typeof(GearsetComponentRenderer),
                    typeof(GearpieceComponentRenderer),
                    typeof(PrerequisiteNodeComponentRenderer),
                    typeof(MateriaGroupComponentRenderer),
                    typeof(GearpieceContextMenu),
                    typeof(GearsetContextMenu),
                    typeof(MateriaContextMenu),
                    typeof(PrerequisiteAtomNodeContextMenu),
                    ];

                foreach (var renderer in renderers)
                {
                    var interfaceType = renderer.GetInterfaces()
                        .FirstOrDefault(i =>
                            i.IsGenericType &&
                            i.IsConstructedGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IRenderer<>))
                        ?? throw new InvalidOperationException($"{renderer.Name} does not implement IRenderer<T>.");
                    var rendererType = renderer
                        .GetProperty("RendererType", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                        ?.GetValue(null)
                        ?? throw new InvalidOperationException($"{renderer.Name} Static property 'RendererType' not found.");

                    builder.RegisterType(renderer).Keyed(rendererType, interfaceType).InstancePerLifetimeScope();
                }

                // display update count on inventory window
                builder.RegisterType<InventoryUpdateDisplayMediator>().As<IInventoryUpdateDisplayService>().SingleInstance();

                // display and pick meld plans
                builder.RegisterType<MeldPlanMediator>().As<IMeldPlanService>().SingleInstance();

                // event listener dependencies
                builder.RegisterGeneric(typeof(AddonServiceDependencies<>)).AsSelf().InstancePerDependency();
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

                // handle active language
                builder.RegisterType<LanguageService>().AsImplementedInterfaces().SingleInstance();

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
            .ConfigureServices(services =>
            {
                // register converters to json options
                services.AddOptions<JsonSerializerOptions>()
                .Configure<IServiceProvider>((opts, serviceProvider) =>
                {
                    opts.PropertyNameCaseInsensitive = true;
                    opts.IncludeFields = true;

                    foreach (var converter in serviceProvider.GetServices<JsonConverter>())
                        opts.Converters.Add(converter);
                });
            })
            .Build();

        logger = host.Services.GetRequiredService<ITypedLogger<Plugin>>();

        logger.Info($"Initialization complete, starting...");
        try
        {
            host.Start();
            logger.Info($"Started successfully");

#if DEBUG
            var commandService = host.Services.GetRequiredService<ICommandService>();
            commandService.ExecuteCommand("/bis", "c");
#endif
        }
        catch (Exception ex)
        {
            logger.Fatal(ex, $"Failed to start");
            Dispose();
        }
    }

    private static Func<IComponentContext, Func<T>> resolveWithScopeTagged<T>(object scopeTag) where T : notnull
    {
        return (IComponentContext context) =>
        {
            var lifetime = context.Resolve<ILifetimeScope>();
            return () =>
            {
                var scope = lifetime.BeginLifetimeScope(scopeTag);
                return scope.Resolve<T>();
            };
        };
    }

    public void Dispose()
    {
        logger.Info($"Teardown start");
        host.StopAsync().GetAwaiter().GetResult();
        host.Dispose();
        logger.Info($"Teardown finish");
    }
}
