using BisBuddy.Gear;
using BisBuddy.Items;
using BisBuddy.Util;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace BisBuddy.Services.Configuration
{
    public class ConfigurationLoaderService(
        ITypedLogger<ConfigurationLoaderService> logger,
        IFileService fileService,
        IItemDataService itemDataService,
        JsonSerializerOptions jsonSerializerOptions
        ) : IConfigurationLoaderService
    {
        private readonly ITypedLogger<ConfigurationLoaderService> logger = logger;
        private readonly IFileService fileService = fileService;
        private readonly IItemDataService itemDataService = itemDataService;
        private readonly JsonSerializerOptions jsonSerializerOptions = jsonSerializerOptions;

        public IConfigurationProperties LoadConfig()
            => loadConfigAsync().Result;

        private async Task<BisBuddy.Configuration> loadConfigAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                logger.Verbose($"Loading config...");
                var configStream = fileService.OpenReadConfigStream();
                using var configJson = await JsonDocument.ParseAsync(configStream, cancellationToken: cancellationToken);
                var configVersion = configJson
                    .RootElement
                    .GetProperty(nameof(IConfigurationProperties.Version))
                    .GetInt32();

                if (configVersion != BisBuddy.Configuration.CurrentVersion)
                {
                    logger.Warning($"Config version {configVersion} found, current {BisBuddy.Configuration.CurrentVersion}. Attempting migration");
                    return await migrateOldConfig(configJson, configStream, configVersion, cancellationToken);
                }
                return configJson.Deserialize<BisBuddy.Configuration>(jsonSerializerOptions) ?? new BisBuddy.Configuration();
            }
            catch (FileNotFoundException)
            {
                return new BisBuddy.Configuration();
            }
            catch (JsonException ex)
            {
                logger.Error(ex, "Error loading/converting config file, creating new");
                return new BisBuddy.Configuration();
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, $"Error loading {Constants.PluginName} configuration");
                throw;
            }
        }

        private async Task<BisBuddy.Configuration> migrateOldConfig(
            JsonDocument configJson,
            Stream configStream,
            int configVersion,
            CancellationToken cancellationToken = default
            )
        {
            var newConfig = new BisBuddy.Configuration();
            for (var version = configVersion; version < BisBuddy.Configuration.CurrentVersion; version++)
            {
                newConfig = version switch
                {
                    1 => await migrate1To2(configStream, cancellationToken),
                    2 => migrate2To3(configJson),
                    3 => await migrate3To4(configJson, cancellationToken),
                    _ => throw new JsonException($"Invalid version number \"{configVersion}\"")
                };
                newConfig.Version = version + 1;
            }

            logger.Info("Config migration success");

            return newConfig;
        }

        /// <summary>
        /// migrate from newtonsoft to stj
        /// migrate from PrerequisiteItems to PrerequisiteTree
        /// </summary>
        /// <param name="configStream">The stream of text for the serialized config to migrate</param>
        /// <returns></returns>
        /// <exception cref="JsonException">If the config fails to deserialize with the old system</exception>
        private async Task<BisBuddy.Configuration> migrate1To2(Stream configStream, CancellationToken cancellationToken = default)
        {
            /// <summary>
            /// migrate from newtonsoft to stj
            /// migrate from PrerequisiteItems to PrerequisiteTree
            /// </summary>
            try
            {
                var reader = new StreamReader(configStream);
                var configText = await reader.ReadToEndAsync(cancellationToken);
                var config = JsonConvert.DeserializeObject<BisBuddy.Configuration>(configText)
                    ?? throw new JsonException("Old config result is null");

                foreach (var charData in config.CharactersData)
                {
                    foreach (var gearset in charData.Value.Gearsets)
                    {
                        foreach (var gearpiece in gearset.Gearpieces)
                        {
                            // rebuild prereqs with new system
                            gearpiece.PrerequisiteTree = itemDataService.BuildGearpiecePrerequisiteTree(gearpiece.ItemId);

                            // if gearpiece is collected, this will collect prereqs as well
                            gearpiece.SetCollected(gearpiece.IsCollected, gearpiece.IsManuallyCollected);
                        }
                    }
                }
                return config;
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                throw new JsonException(ex.Message);
            }
        }

        /// <summary>
        /// Migrate old Vector4 HighlightColor to new HighlightColor type
        /// </summary>
        /// <param name="configJson">JsonDocument config</param>
        /// <returns>The migrated and deserialized config</returns>
        /// <exception cref="JsonException">If the config cannot be migrated or deserialized</exception>
        private BisBuddy.Configuration migrate2To3(JsonDocument configJson)
        {
            HighlightColor? highlightColor = null;
            if (configJson.RootElement.TryGetProperty("HighlightColor", out var highlightColorProperty))
            {
                var highlightColorVector = highlightColorProperty.Deserialize<Vector4>(jsonSerializerOptions);
                highlightColor = new HighlightColor(highlightColorVector);
            }

            var config = configJson.Deserialize<BisBuddy.Configuration>(jsonSerializerOptions) ?? new BisBuddy.Configuration();
            if (highlightColor is not null)
                config.DefaultHighlightColor = highlightColor;

            return config;
        }

        /// <summary>
        /// Migrate old version with gearsets stored in configuration file to one stored seperately
        /// </summary>
        /// <param name="configJson">JsonDocument config</param>
        /// <returns></returns>
        private async Task<BisBuddy.Configuration> migrate3To4(JsonDocument configJson, CancellationToken cancellationToken = default)
        {
            var config = configJson.Deserialize<BisBuddy.Configuration>(jsonSerializerOptions)
                ?? new BisBuddy.Configuration();

            // no gearsets stored in configuration
            if (config.CharactersData.Count == 0)
                return config;

            // move gearsets to actual file
            var writeGearsetsTasks = config.CharactersData.Select(async charData =>
            {
                var gearsets = JsonSerializer.Serialize(charData.Value.Gearsets, jsonSerializerOptions);
                await fileService.WriteGearsetsAsync(charData.Key, gearsets, cancellationToken);
            });
            await Task.WhenAll(writeGearsetsTasks);

            // delete data in the config
            config.CharactersData.Clear();
            return config;
        }
    }

    public interface IConfigurationLoaderService
    {
        public IConfigurationProperties LoadConfig();
    }
}
