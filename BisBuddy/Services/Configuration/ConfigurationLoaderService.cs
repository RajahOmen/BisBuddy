using BisBuddy.Gear;
using BisBuddy.Items;
using BisBuddy.Util;
using Dalamud.Utility;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JsonException = System.Text.Json.JsonException;

namespace BisBuddy.Services.Configuration
{
    public class ConfigurationLoaderService(
        ITypedLogger<ConfigurationLoaderService> logger,
        IFileService fileService,
        IItemDataService itemDataService,
        IJsonSerializerService jsonSerializerService
        ) : IConfigurationLoaderService
    {
        private readonly ITypedLogger<ConfigurationLoaderService> logger = logger;
        private readonly IFileService fileService = fileService;
        private readonly IItemDataService itemDataService = itemDataService;
        private readonly IJsonSerializerService jsonSerializerService = jsonSerializerService;

        public IConfigurationProperties LoadConfig()
        {
            try
            {
                logger.Verbose($"Loading config...");
                using var configStream = fileService.OpenReadConfigStream();
                using var configJson = JsonDocument.Parse(configStream);
                var configVersion = configJson
                    .RootElement
                    .GetProperty(nameof(IConfigurationProperties.Version))
                    .GetInt32();

                if (configVersion != Configuration.CurrentVersion)
                {
                    logger.Warning($"Config version {configVersion} found, current {Configuration.CurrentVersion}. Attempting migration");
                    return migrateOldConfig(configJson, configStream, configVersion);
                }
                return jsonSerializerService.Deserialize<Configuration>(configJson) ?? new Configuration();
            }
            catch (FileNotFoundException)
            {
                return new Configuration();
            }
            catch (JsonException ex)
            {
                logger.Error(ex, "Error loading/converting config file, creating new");
                return new Configuration();
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, $"Error loading {Constants.PluginName} configuration");
                throw;
            }
        }

        private Configuration migrateOldConfig(
            JsonDocument configJson,
            Stream configStream,
            int configVersion
            )
        {
            var newConfig = new Configuration();
            for (var version = configVersion; version < Configuration.CurrentVersion; version++)
            {
                newConfig = version switch
                {
                    1 => migrate1To2(configStream),
                    2 => migrate2To3(configJson),
                    3 => migrate3To4(configJson),
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
        private Configuration migrate1To2(Stream configStream)
        {
            /// <summary>
            /// migrate from newtonsoft to stj
            /// migrate from PrerequisiteItems to PrerequisiteTree
            /// </summary>
            try
            {
                var reader = new StreamReader(configStream);
                var configText = reader.ReadToEnd();
                var config = JsonConvert.DeserializeObject<Configuration>(configText)
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
                            gearpiece.IsCollected = gearpiece.IsCollected;
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
        private Configuration migrate2To3(JsonDocument configJson)
        {
            HighlightColor? highlightColor = null;
            if (configJson.RootElement.TryGetProperty("HighlightColor", out var highlightColorProperty))
            {
                var highlightColorVector = jsonSerializerService.Deserialize<Vector4>(highlightColorProperty);
                highlightColor = new HighlightColor(highlightColorVector);
            }

            var config = jsonSerializerService.Deserialize<Configuration>(configJson) ?? new Configuration();
            if (highlightColor is not null)
                config.DefaultHighlightColor = highlightColor;

            return config;
        }

        /// <summary>
        /// Migrate old version with gearsets stored in configuration file to one stored seperately
        /// </summary>
        /// <param name="configJson">JsonDocument config</param>
        /// <returns></returns>
        private Configuration migrate3To4(JsonDocument configJson)
        {
            var config = jsonSerializerService.Deserialize<Configuration>(configJson)
                ?? new Configuration();

            // no gearsets stored in configuration
            if (config.CharactersData.Count == 0)
                return config;

            // move gearsets to actual file
            foreach (var charData in config.CharactersData)
            {
                var gearsets = jsonSerializerService.Serialize(charData.Value.Gearsets);
                fileService.WriteGearsetsString(charData.Key, gearsets);
            }

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
