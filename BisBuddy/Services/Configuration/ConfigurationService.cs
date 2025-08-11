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
    /// <param name="effectsAssignments">If the configuration data change may effect how items are assigned</param>
    public delegate void ConfigurationChangeDelegate(bool effectsAssignments);

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

        private void triggerConfigurationChange(bool effectsAssignments)
        {
            logger.Verbose($"OnConfigurationChange (effectsAssignments: {effectsAssignments})");
            OnConfigurationChange?.Invoke(effectsAssignments);
        }

        private void updateConfigProperty<T>(
            Expression<Func<IConfigurationProperties, T>> propertyExp,
            T newValue,
            bool effectsAssignments
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
            triggerConfigurationChange(effectsAssignments: effectsAssignments);
        }

        public int Version
        {
            get => configuration.Version;
            set => updateConfigProperty(cfg => cfg.Version, value, false);
        }

        public bool HighlightNeedGreed
        {
            get => configuration.HighlightNeedGreed;
            set => updateConfigProperty(cfg => cfg.HighlightNeedGreed, value, false);
        }

        public bool HighlightShops
        {
            get => configuration.HighlightShops;
            set => updateConfigProperty(cfg => cfg.HighlightShops, value, false);
        }

        public bool HighlightMateriaMeld
        {
            get => configuration.HighlightMateriaMeld;
            set => updateConfigProperty(cfg => cfg.HighlightMateriaMeld, value, false);
        }

        public bool HighlightNextMateria
        {
            get => configuration.HighlightNextMateria;
            set => updateConfigProperty(cfg => cfg.HighlightNextMateria, value, false);
        }

        public bool HighlightUncollectedItemMateria
        {
            get => configuration.HighlightUncollectedItemMateria;
            set => updateConfigProperty(cfg => cfg.HighlightUncollectedItemMateria, value, true);
        }

        public bool HighlightPrerequisiteMateria
        {
            get => configuration.HighlightPrerequisiteMateria;
            set => updateConfigProperty(cfg => cfg.HighlightPrerequisiteMateria, value, false);
        }

        public bool HighlightInventories
        {
            get => configuration.HighlightInventories;
            set => updateConfigProperty(cfg => cfg.HighlightInventories, value, false);
        }

        public bool HighlightMarketboard
        {
            get => configuration.HighlightMarketboard;
            set => updateConfigProperty(cfg => cfg.HighlightMarketboard, value, false);
        }

        public bool AnnotateTooltips
        {
            get => configuration.AnnotateTooltips;
            set => updateConfigProperty(cfg => cfg.AnnotateTooltips, value, false);
        }

        public bool AutoCompleteItems
        {
            get => configuration.AutoCompleteItems;
            set => updateConfigProperty(cfg => cfg.AutoCompleteItems, value, false);
        }

        public bool AutoScanInventory
        {
            get => configuration.AutoScanInventory;
            set => updateConfigProperty(cfg => cfg.AutoScanInventory, value, true);
        }

        public bool PluginUpdateInventoryScan
        {
            get => configuration.PluginUpdateInventoryScan;
            set => updateConfigProperty(cfg => cfg.PluginUpdateInventoryScan, value, true);
        }

        public bool StrictMateriaMatching
        {
            get => configuration.StrictMateriaMatching;
            set => updateConfigProperty(cfg => cfg.StrictMateriaMatching, value, true);
        }

        public bool BrightListItemHighlighting
        {
            get => configuration.BrightListItemHighlighting;
            set => updateConfigProperty(cfg => cfg.BrightListItemHighlighting, value, false);
        }

        public HighlightColor DefaultHighlightColor
        {
            get => configuration.DefaultHighlightColor;
        }

        private void handleDefaultHighlightColorChange()
        {
            scheduleSave();
            triggerConfigurationChange(effectsAssignments: false);
        }

        private async Task saveAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                logger.Verbose($"Saving configuration file");
                var configText = JsonSerializer.Serialize(configuration, jsonSerializerOptions);
                await fileService.WriteConfigAsync(configText, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.Error($"Error saving config file", ex);
            }
        }

        public void scheduleSave() =>
            queueService.Enqueue(() => saveAsync().WaitSafely());

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await saveAsync(cancellationToken);
        }
    }

    public interface IConfigurationService : IHostedService, IConfigurationProperties
    {
        public event ConfigurationChangeDelegate? OnConfigurationChange;
    }
}
