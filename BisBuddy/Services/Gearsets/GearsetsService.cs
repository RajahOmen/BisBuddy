using BisBuddy.Gear;
using BisBuddy.Gear.MeldPlanManager;
using BisBuddy.Items;
using BisBuddy.Services.ItemAssignment;
using BisBuddy.Windows;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BisBuddy.Services.Gearsets
{
    public delegate void GearsetsChangeHandler();

    /// <summary>
    /// Manages a logged in character's gearsets, gearset requirements, gearset changes & updates
    /// </summary>
    public partial class GearsetsService : IGearsetsService
    {
        private readonly IPluginLog pluginLog;
        private readonly IGameInventory gameInventory;
        private readonly IClientState clientState;
        private readonly IConfigurationService configService;
        private readonly JsonSerializerOptions jsonSerializerOptions;
        private readonly MainWindow mainWindow;
        private readonly ItemDataService itemData;
        private readonly QueueService itemAssignmentQueue;

        private ulong currentLocalContentId => clientState.LocalContentId;

        private List<Gearset> gearsets;

        public IReadOnlyList<Gearset> Gearsets => gearsets;

        private readonly List<GameInventoryType> inventorySources = [
            GameInventoryType.Inventory1,
            GameInventoryType.Inventory2,
            GameInventoryType.Inventory3,
            GameInventoryType.Inventory4,
            GameInventoryType.EquippedItems,
            GameInventoryType.ArmoryMainHand,
            GameInventoryType.ArmoryOffHand,
            GameInventoryType.ArmoryHead,
            GameInventoryType.ArmoryBody,
            GameInventoryType.ArmoryHands,
            GameInventoryType.ArmoryLegs,
            GameInventoryType.ArmoryFeets,
            GameInventoryType.ArmoryEar,
            GameInventoryType.ArmoryNeck,
            GameInventoryType.ArmoryWrist,
            GameInventoryType.ArmoryRings,
            ];

        public IReadOnlyList<GameInventoryType> InventorySources => inventorySources;

        private Dictionary<uint, List<ItemRequirement>> itemRequirements;

        public GearsetsService(
            IPluginLog pluginLog,
            IClientState clientState,
            IGameInventory gameInventory,
            IConfigurationService configurationService,
            JsonSerializerOptions jsonSerializerOptions,
            MainWindow mainWindow,
            ItemDataService itemData,
            QueueService itemAssignmentQueue
            )
        {
            this.pluginLog = pluginLog;
            this.clientState = clientState;
            this.gameInventory = gameInventory;
            this.configService = configurationService;
            this.jsonSerializerOptions = jsonSerializerOptions;
            this.mainWindow = mainWindow;
            this.itemData = itemData;
            this.itemAssignmentQueue = itemAssignmentQueue;
            gearsets = getCurrentGearsets();
            itemRequirements = buildItemRequirements();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // tracking logged in character
            clientState.Login += handleLogin;
            clientState.Logout += handleLogout;

            // tracking inventory state changes
            gameInventory.ItemAdded += handleItemAdded;
            gameInventory.ItemRemoved += handleItemRemoved;
            gameInventory.ItemChanged += handleItemChanged;
            gameInventory.ItemMoved += handleItemMoved;

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // tracking logged in character
            clientState.Login -= handleLogin;
            clientState.Logout -= handleLogout;

            // tracking inventory state changes
            gameInventory.ItemAdded -= handleItemAdded;
            gameInventory.ItemRemoved -= handleItemRemoved;
            gameInventory.ItemChanged -= handleItemChanged;
            gameInventory.ItemMoved -= handleItemMoved;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Retrieve the gearsets saved for the current logged-in character id, or an empty list if there is none
        /// </summary>
        /// <returns></returns>
        private List<Gearset> getCurrentGearsets()
        {
            if (configService.Config.CharactersData.TryGetValue(currentLocalContentId, out var characterInfo))
                return characterInfo.Gearsets;
            else
                return [];
        }

        public string ExportGearsetToJsonStr(Gearset gearset)
        {
            return JsonSerializer.Serialize(gearset, jsonSerializerOptions);
        }
    }

    public interface IGearsetsService : IHostedService
    {
        public IReadOnlyList<Gearset> Gearsets { get; }
        public IReadOnlyList<GameInventoryType> InventorySources { get; }
        public void AddGearset(Gearset gearset);
        public void AddGearsets(IEnumerable<Gearset> gearsets);
        public void RemoveGearsets(IEnumerable<Gearset> gearsets);
        public void RemoveGearsets(Gearset gearset);

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
        public IEnumerable<ItemRequirement> GetItemRequirements(
            uint itemId,
            bool includePrereqs = true,
            bool includeMateria = true,
            bool includeCollected = false,
            bool includeObtainable = false,
            bool includeCollectedPrereqs = false
        );
        public HighlightColor? GetRequirementColor(
            IEnumerable<ItemRequirement> itemRequirements
        );
        public HighlightColor? GetRequirementColor(
            uint itemId,
            bool includePrereqs = true,
            bool includeMateria = true,
            bool includeCollected = false,
            bool includeObtainable = false,
            bool includeCollectedPrereqs = false
        );
        public List<MeldPlan> GetNeededItemMeldPlans(uint itemId);
        public Dictionary<string, HighlightColor> GetUnmeldedItemColors(
            bool includePrerequisites
        );

        /// <summary>
        /// Schedule a request to update item assignment for a subset of gearsets for the current character.
        /// </summary>
        /// <param name="gearsetsToUpdate">The gearsets whose state may be changed by this update</param>
        /// <param name="saveChanges">If the changes should be saved upon completion</param>
        /// <param name="manualUpdate">If this update was triggered by direct user input</param>
        public void ScheduleUpdateFromInventory(
            List<Gearset> gearsetsToUpdate,
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
