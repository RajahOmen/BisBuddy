using Autofac.Core;
using BisBuddy.Gear;
using BisBuddy.Items;
using BisBuddy.Services.ItemAssignment;
using BisBuddy.Util;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace BisBuddy.Services
{
    /// <summary>
    /// Handles when the configuration data changes
    /// </summary>
    /// <param name="effectsAssignments">If the configuration data change may effect how items are assigned</param>
    public delegate void ConfigurationChangeDelegate(bool effectsAssignments);

    public class ConfigurationService(
        ITypedLogger<ConfigurationService> logger,
        IConfigurationLoaderService loadConfigurationService,
        IQueueService queueService,
        IFileService fileService,
        JsonSerializerOptions jsonSerializerOptions
        ) : IConfigurationService
    {
        private readonly ITypedLogger<ConfigurationService> logger = logger;
        private readonly IQueueService queueService = queueService;
        private readonly IFileService fileService = fileService;
        private readonly JsonSerializerOptions jsonSerializerOptions = jsonSerializerOptions;

        private IConfigurationProperties configuration { get; set; } = loadConfigurationService.LoadConfig();

        public event ConfigurationChangeDelegate? OnConfigurationChange;

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
            OnConfigurationChange?.Invoke(effectsAssignments: effectsAssignments);
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

        public void UpdateDefaultHighlightColor(Vector4 newColor)
        {
            configuration.DefaultHighlightColor.UpdateColor(newColor);
            OnConfigurationChange?.Invoke(effectsAssignments: false);
        }

        private async Task saveAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var configText = JsonSerializer.Serialize(configuration, jsonSerializerOptions);
                await fileService.WriteConfigAsync(configText, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.Error($"Error saving config file", ex);
            }
        }

        public void ScheduleSave() =>
            queueService.Enqueue(async () => await saveAsync());

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
        public void UpdateDefaultHighlightColor(Vector4 newColor);
        public void ScheduleSave();
    }
}
