using BisBuddy.Factories;
using BisBuddy.Gear;
using BisBuddy.Gear.Melds;
using BisBuddy.Import;
using BisBuddy.Mediators;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.ImportGearset;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BisBuddy.Services.Gearsets
{
    public delegate void GearsetsChangeHandler();

    /// <summary>
    /// Manages a logged in character's gearsets, gearset requirements, gearset changes & updates
    /// </summary>
    public partial class GearsetsService(
        ITypedLogger<GearsetsService> logger,
        IFramework framework,
        IClientState clientState,
        IGameInventory gameInventory,
        IConfigurationService configurationService,
        IFileService fileService,
        JsonSerializerOptions jsonSerializerOptions,
        IItemAssignmentSolverFactory itemAssignmentSolverFactory,
        IQueueService queueService,
        IInventoryUpdateDisplayService inventoryUpdateDisplayService,
        IImportGearsetService importGearsetService
        ) : IGearsetsService
    {
        private readonly ITypedLogger<GearsetsService> logger = logger;
        private readonly IFramework framework = framework;
        private readonly IGameInventory gameInventory = gameInventory;
        private readonly IClientState clientState = clientState;
        private readonly IConfigurationService configurationService = configurationService;
        private readonly IFileService fileService = fileService;
        private readonly JsonSerializerOptions jsonSerializerOptions = jsonSerializerOptions;
        private readonly IItemAssignmentSolverFactory itemAssignmentSolverFactory = itemAssignmentSolverFactory;
        private readonly IQueueService queueService = queueService;
        private readonly IInventoryUpdateDisplayService inventoryUpdateDisplayService = inventoryUpdateDisplayService;
        private readonly IImportGearsetService importGearsetService = importGearsetService;

        private bool gearsetsDirty = false;
        private bool assignmentsDirty = false;
        private bool gearsetsChangeLocked = false;

        private ulong currentLocalContentId => clientState.LocalContentId;

        private List<Gearset> currentGearsets = [];
        private Dictionary<uint, List<ItemRequirementOwned>> currentItemRequirements = [];
        private GearsetSortType currentGearsetsSortType = GearsetSortType.ImportDate;
        private bool currentGearsetsSortDescending = false;

        public IReadOnlyList<Gearset> CurrentGearsets
        {
            get => currentGearsets;
            private set
            {
                currentGearsets = value.ToList();
                scheduleGearsetsChange(updateAssignments: true);
            }
        }

        public bool GearsetsLoaded { get; private set; } = false;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // updating gearset data per framework update
            framework.Update += onUpdate;

            // note: load after framework event registration
            loadGearsets();

            // tracking logged in character
            clientState.Login += handleLogin;
            clientState.Logout += handleLogout;

            // tracking config changes
            configurationService.OnConfigurationChange += handleConfigChange;

            // tracking internal gearset changes
            foreach (var gearset in currentGearsets)
                gearset.OnGearsetChange += handleGearsetChange;

            // if configured to, run scan to ensure up-to-date
            if (GearsetsLoaded && configurationService.AutoScanInventory)
                ScheduleUpdateFromInventory();

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // updating gearset data per framework update
            framework.Update -= onUpdate;

            // tracking logged in character
            clientState.Login -= handleLogin;
            clientState.Logout -= handleLogout;

            // tracking config changes
            configurationService.OnConfigurationChange -= handleConfigChange;

            // tracking internal gearset changes
            foreach (var gearset in currentGearsets)
                gearset.OnGearsetChange -= handleGearsetChange;

            await saveCurrentGearsetsAsync();
        }

        /// <summary>
        /// Retrieve the gearsets saved for the current logged-in character id, or an empty list if there is none
        /// If there isn't a character logged in, an empty list is returned
        /// If the character has no saved gearsets, a new empty gearsets file is created and an empty list is returned
        /// </summary>
        /// <returns>The gearsets for the logged in character id</returns>
        private void loadGearsets()
        {
            try
            {
                logger.Debug($"Loading gearsets for \"{currentLocalContentId}\"");
                // unregister change listening from old gearsets
                foreach (var gearset in currentGearsets)
                    gearset.OnGearsetChange -= handleGearsetChange;

                if (currentLocalContentId == 0)
                    currentGearsets = [];
                else
                {
                    using var gearsetsReadStream = fileService.OpenReadGearsetsStream(currentLocalContentId);
                    currentGearsets = JsonSerializer.Deserialize<List<Gearset>>(
                        gearsetsReadStream,
                        jsonSerializerOptions
                        ) ?? [];
                    GearsetsLoaded = true;
                }
            }
            catch (FileNotFoundException)
            {
                logger.Warning($"No gearsets file found for \"{currentLocalContentId}\", creating new");
                currentGearsets = [];
                _ = saveCurrentGearsetsAsync();
                GearsetsLoaded = true;
            }
            finally
            {
                foreach (var gearset in currentGearsets)
                    gearset.OnGearsetChange += handleGearsetChange;
                triggerGearsetsChange(saveToFile: false);
                logger.Verbose($"finish triggerGearsetsChange");
            }
        }

        private void scheduleSaveCurrentGearsets()
        {
            logger.Verbose($"Enqueuing save of current gearsets for \"{currentLocalContentId}\"");
            queueService.Enqueue(saveCurrentGearsets);
        }

        private void saveCurrentGearsets()
        {
            // don't save anything if no gearsets are actually loaded
            if (!GearsetsLoaded || currentLocalContentId == 0)
                return;

            var gearsetsListStr = serializeGearsets(currentGearsets);

            logger.Verbose($"Saving {currentLocalContentId}'s current gearsets");

            fileService.WriteGearsetsString(
                currentLocalContentId,
                gearsetsListStr
                );
        }

        private async Task saveCurrentGearsetsAsync()
        {
            // don't save anything if no gearsets are actually loaded
            if (!GearsetsLoaded || currentLocalContentId == 0)
                return;

            var gearsetsListStr = serializeGearsets(currentGearsets);

            logger.Verbose($"Saving {currentLocalContentId}'s current gearsets");

            await fileService.WriteGearsetsStringAsync(
                currentLocalContentId,
                gearsetsListStr
                );
        }

        private string serializeGearsets(List<Gearset> gearsets)
        {
            return JsonSerializer.Serialize(
                gearsets,
                jsonSerializerOptions
                );
        }

        public string ExportGearsetToJsonStr(Gearset gearset) =>
            JsonSerializer.Serialize(gearset, jsonSerializerOptions);
    }

    public interface IGearsetsService : IHostedService
    {
        public IReadOnlyList<Gearset> CurrentGearsets { get; }
        /// <summary>
        /// Indicates if gearsets for the current character have been loaded
        /// </summary>
        public bool GearsetsLoaded { get; }
        public Task<ImportGearsetsResult> AddGearsetsFromSource(ImportGearsetSourceType sourceType, string sourceString);
        public void RemoveGearset(Gearset gearset);
        public string ExportGearsetToJsonStr(Gearset gearset);
        public void ChangeGearsetSortOrder(GearsetSortType? newSortType = null, bool? sortDescending = null);

        /// <summary>
        /// Fires whenever a change to the current gearsets is made
        /// </summary>
        public event GearsetsChangeHandler? OnGearsetsChange;

        public bool RequirementsNeedItemId(
            uint itemId,
            bool includePrereqs = true,
            bool includeMateria = true,
            bool includeCollected = false,
            bool includeObtainable = false,
            bool includeCollectedPrereqs = false
        );
        public IEnumerable<ItemRequirementOwned> GetItemRequirements(
            uint itemId,
            bool includePrereqs = true,
            bool includeMateria = true,
            bool includeCollected = false,
            bool includeObtainable = false,
            bool includeCollectedPrereqs = false
        );
        public HighlightColor? GetRequirementColor(
            IEnumerable<ItemRequirementOwned> itemRequirements
        );
        public HighlightColor? GetRequirementColor(
            uint itemId,
            bool includePrereqs = true,
            bool includeMateria = true,
            bool includeCollected = false,
            bool includeObtainable = false,
            bool includeCollectedPrereqs = false
        );
        public List<(Gearset, MateriaGroup)> GetNeededItemMeldPlans(uint itemId);
        public Dictionary<string, HighlightColor> GetUnmeldedMateriaColors();

        /// <summary>
        /// Schedule a request to update item assignment for a subset of gearsets for the current character.
        /// </summary>
        /// <param name="gearsetsToUpdate">The gearsets whose state may be changed by this update</param>
        /// <param name="saveChanges">If the changes should be saved upon completion</param>
        /// <param name="manualUpdate">If this update was triggered by direct user input</param>
        public void ScheduleUpdateFromInventory(
            IEnumerable<Gearset> gearsetsToUpdate,
            bool saveChanges = true,
            bool manualUpdate = false
            );

        /// <summary>
        /// Schedule a request to update item assignment for all gearsets for the current character.
        /// </summary>
        /// <param name="saveChanges">If the changes should be saved upon completion</param>
        /// <param name="manualUpdate">If this update was triggered by direct user input</param>
        public void ScheduleUpdateFromInventory(
            bool saveChanges = true,
            bool manualUpdate = false
            );
    }
}
