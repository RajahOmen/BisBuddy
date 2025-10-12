using BisBuddy.Gear;
using Dalamud.Utility;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace BisBuddy.Services.Configuration
{
    /// <summary>
    /// Handles when the configuration data changes
    /// </summary>
    /// <param name="affectsAssignments">If the configuration data change may effect how items are assigned</param>
    public delegate void ConfigurationChangeDelegate(bool affectsAssignments);

    public class ConfigurationService : IConfigurationService
    {
        private readonly ITypedLogger<ConfigurationService> logger;
        private readonly IQueueService queueService;
        private readonly IFileService fileService;
        private readonly JsonSerializerOptions jsonSerializerOptions;

        public ConfigurationService(
            ITypedLogger<ConfigurationService> logger,
            IConfigurationLoaderService loadConfigurationService,
            IQueueService queueService,
            IFileService fileService,
            JsonSerializerOptions jsonSerializerOptions
        )
        {
            this.logger = logger;
            this.queueService = queueService;
            this.fileService = fileService;
            this.jsonSerializerOptions = jsonSerializerOptions;
            configuration = loadConfigurationService.LoadConfig();
            configuration.DefaultHighlightColor.OnColorChange += handleDefaultHighlightColorChange;
        }

        private IConfigurationProperties configuration { get; set; }

        public event ConfigurationChangeDelegate? OnConfigurationChange;

        private void triggerConfigurationChange(bool affectsAssignments)
        {
            logger.Verbose($"OnConfigurationChange (affectsAssignments: {affectsAssignments})");
            OnConfigurationChange?.Invoke(affectsAssignments);
        }

        private void updateConfigProperty<T>(
            Expression<Func<IConfigurationProperties, T>> propertyExp,
            T newValue,
            bool affectsAssignments
            )
        {
            if (propertyExp.Body is not MemberExpression memberExpr)
                throw new ArgumentException(
                    "Expression must be a simple property access",
                    nameof(propertyExp)
                    );

            if (memberExpr.Member is not PropertyInfo propInfo)
                throw new ArgumentException(
                    "Expression must point to a property",
                    nameof(propertyExp)
                    );

            propInfo.SetValue(configuration, newValue);
            scheduleSave();
            triggerConfigurationChange(affectsAssignments: affectsAssignments);
        }

        public int Version
        {
            get => configuration.Version;
            set => updateConfigProperty(cfg => cfg.Version, value, affectsAssignments: false);
        }

        public bool HighlightNeedGreed
        {
            get => configuration.HighlightNeedGreed;
            set => updateConfigProperty(cfg => cfg.HighlightNeedGreed, value, affectsAssignments: false);
        }

        public bool HighlightShops
        {
            get => configuration.HighlightShops;
            set => updateConfigProperty(cfg => cfg.HighlightShops, value, affectsAssignments: false);
        }

        public bool HighlightMateriaMeld
        {
            get => configuration.HighlightMateriaMeld;
            set => updateConfigProperty(cfg => cfg.HighlightMateriaMeld, value, affectsAssignments: false);
        }

        public bool HighlightNextMateria
        {
            get => configuration.HighlightNextMateria;
            set => updateConfigProperty(cfg => cfg.HighlightNextMateria, value, affectsAssignments: false);
        }

        public bool HighlightUncollectedItemMateria
        {
            get => configuration.HighlightUncollectedItemMateria;
            set => updateConfigProperty(cfg => cfg.HighlightUncollectedItemMateria, value, affectsAssignments: true);
        }

        public bool HighlightPrerequisiteMateria
        {
            get => configuration.HighlightPrerequisiteMateria;
            set => updateConfigProperty(cfg => cfg.HighlightPrerequisiteMateria, value, affectsAssignments: false);
        }

        public bool HighlightInventories
        {
            get => configuration.HighlightInventories;
            set => updateConfigProperty(cfg => cfg.HighlightInventories, value, affectsAssignments: false);
        }

        public bool HighlightCollectedInInventory
        {
            get => configuration.HighlightCollectedInInventory;
            set => updateConfigProperty(cfg => cfg.HighlightCollectedInInventory, value, false);
        }

        public bool HighlightMarketboard
        {
            get => configuration.HighlightMarketboard;
            set => updateConfigProperty(cfg => cfg.HighlightMarketboard, value, affectsAssignments: false);
        }

        public bool AnnotateTooltips
        {
            get => configuration.AnnotateTooltips;
            set => updateConfigProperty(cfg => cfg.AnnotateTooltips, value, affectsAssignments: false);
        }

        public bool AutoCompleteItems
        {
            get => configuration.AutoCompleteItems;
            set => updateConfigProperty(cfg => cfg.AutoCompleteItems, value, affectsAssignments: false);
        }

        public bool AutoScanInventory
        {
            get => configuration.AutoScanInventory;
            set => updateConfigProperty(cfg => cfg.AutoScanInventory, value, affectsAssignments: true);
        }

        public bool PluginUpdateInventoryScan
        {
            get => configuration.PluginUpdateInventoryScan;
            set => updateConfigProperty(cfg => cfg.PluginUpdateInventoryScan, value, affectsAssignments: true);
        }

        public bool StrictMateriaMatching
        {
            get => configuration.StrictMateriaMatching;
            set => updateConfigProperty(cfg => cfg.StrictMateriaMatching, value, affectsAssignments: true);
        }

        public bool BrightListItemHighlighting
        {
            get => configuration.BrightListItemHighlighting;
            set => updateConfigProperty(cfg => cfg.BrightListItemHighlighting, value, affectsAssignments: false);
        }

        public HighlightColor DefaultHighlightColor
        {
            get => configuration.DefaultHighlightColor;
        }

        public UiTheme UiTheme
        {
            get => configuration.UiTheme;
            set => updateConfigProperty(cfg => cfg.UiTheme, value, affectsAssignments: false);
        }

        public void ResetUiTheme()
        {
            logger.Info($"Resetting UI theme to default");
            configuration.UiTheme = new UiTheme();
        }

        private void handleDefaultHighlightColorChange()
        {
            scheduleSave();
            triggerConfigurationChange(affectsAssignments: false);
        }

        private void saveConfig()
        {
            try
            {
                logger.Verbose($"Saving configuration file");
                var configText = JsonSerializer.Serialize(configuration, jsonSerializerOptions);
                fileService.WriteConfigString(configText);
            }
            catch (Exception ex)
            {
                logger.Error($"Error saving config file", ex);
            }
        }

        public void scheduleSave()
        {
            logger.Verbose($"Enqueuing save of configuration file");
            queueService.Enqueue(saveConfig);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                logger.Verbose($"Saving configuration file");
                var configText = JsonSerializer.Serialize(configuration, jsonSerializerOptions);
                await fileService.WriteConfigStringAsync(configText, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.Error($"Error saving config file", ex);
            }
        }
    }

    public interface IConfigurationService : IHostedService, IConfigurationProperties
    {
        public event ConfigurationChangeDelegate? OnConfigurationChange;

        /// <summary>
        /// Resets the UI theme to whatever is default
        /// </summary>
        public void ResetUiTheme();
    }
}
