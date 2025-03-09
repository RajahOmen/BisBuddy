using BisBuddy.Gear;
using BisBuddy.ItemAssignment;
using BisBuddy.Items;
using Dalamud.Game.Inventory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy
{
    public sealed partial class Plugin
    {
        public static readonly GameInventoryType[] InventorySources =
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

        public void ScheduleUpdateFromInventory(List<Gearset> gearsetsToUpdate, bool saveChanges = true, bool manualUpdate = false)
        {
            // don't block main thread, queue for execution instead
            itemAssignmentQueue.Enqueue(() =>
            {
                // returns number of gearpiece status changes after update
                try
                {
                    if (!Services.ClientState.IsLoggedIn) return;
                    if (gearsetsToUpdate.Count == 0) return;

                    // display loading state in main menu
                    MainWindow.InventoryScanRunning = true;

                    var itemsList = ItemData.GetGameInventoryItems(InventorySources);
                    var gearpiecesToUpdate = Gearset.GetGearpiecesFromGearsets(gearsetsToUpdate);

                    // add ALL active gearsets to solver (not just ones being updated)
                    var activeGearsets = Gearsets.Where(g => g.IsActive).ToList();

                    var solver = new ItemAssigmentSolver(activeGearsets, itemsList, ItemData, Configuration.StrictMateriaMatching);

                    var solveResult = solver.Solve();

                    var updatedGearpieces = ItemAssigner.makeItemAssignments(solveResult, gearpiecesToUpdate, ItemData);

                    Services.Log.Debug($"Updated {updatedGearpieces.Count} gearpieces from inventories");

                    if (updatedGearpieces.Count > 0 && saveChanges)
                    {
                        SaveGearsetsWithUpdate(false);
                    }

                    if (manualUpdate)
                    {
                    MainWindow.InventoryScanUpdateCount = updatedGearpieces.Count;
                }
                }
                catch (Exception ex)
                {
                    Services.Log.Error(ex, "Failed to update gearsets from inventory");

                    if (manualUpdate)
                    {
                    MainWindow.InventoryScanUpdateCount = 0;
                }
                }
                finally
                {
                    MainWindow.InventoryScanRunning = false;
                }
            });
        }
    }
}
