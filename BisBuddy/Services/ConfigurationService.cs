using BisBuddy.Gear;
using BisBuddy.Items;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace BisBuddy.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly IDalamudPluginInterface pluginInterface;
        private readonly IPluginLog pluginLog;
        private readonly IItemDataService itemDataService;
        private readonly JsonSerializerOptions jsonSerializerOptions;

        public Configuration Config { get; init; }

        public ConfigurationService(
            IDalamudPluginInterface pluginInterface,
            IPluginLog pluginLog,
            IItemDataService itemDataService,
            JsonSerializerOptions jsonSerializerOptions
            )
        {
            this.pluginInterface = pluginInterface;
            this.pluginLog = pluginLog;
            this.itemDataService = itemDataService;
            this.jsonSerializerOptions = jsonSerializerOptions;
            this.Config = loadConfig();
        }

        private Configuration loadConfig()
        {
            try
            {
                var configText = File.ReadAllText(pluginInterface.ConfigFile.FullName);
                using (var configJson = JsonDocument.Parse(configText))
                {
                    var configVersion = configJson
                        .RootElement
                        .GetProperty(nameof(Version))
                        .GetInt32();

                    if (configVersion != Configuration.CurrentVersion)
                    {
                        pluginLog.Warning($"Config version {configVersion} found, current {Configuration.CurrentVersion}. Attempting migration");
                        return migrateOldConfig(configJson, configText, configVersion);
                    }
                }

                pluginLog.Verbose($"Loading config...");
                return JsonSerializer.Deserialize<Configuration>(configText, jsonSerializerOptions) ?? new Configuration();
            }
            catch (FileNotFoundException)
            {
                return new Configuration();
            }
            catch (JsonException ex)
            {
                pluginLog.Error(ex, "Error loading/converting config file, creating new");
                return new Configuration();
            }
            catch (Exception ex)
            {
                pluginLog.Fatal(ex, $"Error loading {Plugin.PluginName} configuration");
                throw;
            }
        }

        private Configuration migrateOldConfig(
            JsonDocument configJson,
            string configText,
            int configVersion
            )
        {
            var newConfig = new Configuration();
            for (var version = configVersion; version < Configuration.CurrentVersion; version++)
            {
                newConfig = version switch
                {
                    1 => migrate1To2(configText),
                    2 => migrate2To3(configJson),
                    _ => throw new JsonException($"Invalid version number \"{configVersion}\"")
                };
                newConfig.Version = version + 1;
            }

            pluginLog.Info("Config migration success");

            return newConfig;
        }

        /// <summary>
        /// migrate from newtonsoft to stj
        /// migrate from PrerequisiteItems to PrerequisiteTree
        /// </summary>
        /// <param name="configText">The string of text for the serialized config to migrate</param>
        /// <returns></returns>
        /// <exception cref="JsonException">If the config fails to deserialize with the old system</exception>
        private Configuration migrate1To2(string configText)
        {
            /// <summary>
            /// migrate from newtonsoft to stj
            /// migrate from PrerequisiteItems to PrerequisiteTree
            /// </summary>
            try
            {
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
        /// <param name="jsonOptions">options instance for deserializing</param>
        /// <returns>The migrated and deserialized config</returns>
        /// <exception cref="JsonException">If the config cannot be migrated or deserialized</exception>
        private Configuration migrate2To3(JsonDocument configJson)
        {
            HighlightColor? highlightColor = null;
            if (configJson.RootElement.TryGetProperty("HighlightColor", out var highlightColorProperty))
            {
                var highlightColorVector = JsonSerializer.Deserialize<Vector4>(highlightColorProperty, jsonSerializerOptions);
                highlightColor = new HighlightColor(highlightColorVector);
            }

            var config = JsonSerializer.Deserialize<Configuration>(configJson, jsonSerializerOptions) ?? new Configuration();
            if (highlightColor is not null)
                config.DefaultHighlightColor = highlightColor;

            return config;
        }

        public void Save()
        {
            var configText = JsonSerializer.Serialize(Config, jsonSerializerOptions);
            File.WriteAllText(pluginInterface.ConfigFile.FullName, configText);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Save();
            
            return Task.CompletedTask;
        }

        public IReadOnlyList<Gearset> GetCharacterGearsets(ulong characterId, JsonSerializerOptions jsonOptions)
        {
            // if not logged in, return a dummy list
            if (characterId == 0)
                return [];

            if (Config.CharactersData.TryGetValue(characterId, out var charInfo))
                return charInfo.Gearsets;

            pluginLog.Debug($"No existing gearsets found for character {characterId}, creating new one");
            // no existing gearsets found for character, creating new one
            var newCharacterInfo = new CharacterInfo(characterId, []);
            Config.CharactersData.Add(characterId, newCharacterInfo);
            Save();
            return newCharacterInfo.Gearsets;
        }
    }

    public interface IConfigurationService : IHostedService
    {
        public Configuration Config { get; }
        public IReadOnlyList<Gearset> GetCharacterGearsets();
        public void Save();
    }
}
