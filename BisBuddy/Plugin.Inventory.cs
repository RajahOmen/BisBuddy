using BisBuddy.Gear;
using BisBuddy.ItemAssignment;
using BisBuddy.Items;
using Dalamud.Game.Inventory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BisBuddy
{
    public sealed partial class Plugin
    {
        internal static readonly GameInventoryType[] InventorySources =
        [
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

        public void UpdateFromInventory(List<Gearset> gearsetsToUpdate)
        {
            // returns number of gearpiece status changes after update
            if (!Services.ClientState.IsLoggedIn) return;
            if (gearsetsToUpdate.Count == 0) return;

            try
            {
                Services.Log.Verbose("Updating gearsets from inventory");

                var itemsList = ItemData.GetGameInventoryItems(InventorySources);
                var gearpiecesToUpdate = Gearset.GetGearpiecesFromGearsets(gearsetsToUpdate);

                // add ALL active gearsets to solver (not just ones being updated)
                var activeGearsets = Gearsets.Where(g => g.IsActive).ToList();

                var solver = new ItemAssigmentSolver(activeGearsets, itemsList, ItemData);

                // don't block main thread
                Task.Run(() =>
                {
                    var solveResult = solver.Solve();

                    var updatedGearpieces = ItemAssigner.makeItemAssignments(solveResult, gearpiecesToUpdate, ItemData);

                    Services.Log.Debug($"Updated {updatedGearpieces.Count} gearpieces from inventories");

                    if (updatedGearpieces.Count > 0)
                    {
                        SaveGearsetsWithUpdate();
                    }
                    MainWindow.InventoryScanUpdateCount = updatedGearpieces.Count;
                    MainWindow.InventoryScanRunning = false;
                });
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Failed to update gearsets from inventory");
            }
        }
    }
}
