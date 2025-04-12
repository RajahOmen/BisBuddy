using BisBuddy.Gear;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Inventory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.EventListeners.AddonEventListeners.Containers
{
    public abstract class ContainerEventListener(Plugin plugin)
        : AddonEventListener(plugin, plugin.Configuration.HighlightInventories)
    {
        public override uint AddonCustomNodeId => throw new NotImplementedException();

        protected readonly List<int> neededItemIndexes = [];
        protected readonly List<nint> dragDropComponentNodes = [];

        // items per standard inventory container 'page'
        protected static readonly int itemsPerPage = 35;

        protected abstract int pagesPerView { get; }
        protected abstract int maxTabIndex { get; }
        protected abstract string[] dragDropGridAddonNames { get; }
        protected abstract unsafe ItemOrderModuleSorter* sorter { get; }

        protected abstract int getTabIndex();

        protected abstract unsafe List<nint> getAddons();

        protected abstract List<nint> getDragDropComponents(nint gridAddon);

        protected override unsafe void registerAddonListeners()
        {
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, AddonName, handlePostRefresh);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, AddonName, handlePostRequestedUpdate);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, AddonName, handlePostRecieveEvent);

            // ensure updates listened for for all addons (incl. children)
            foreach (var dragDropAddonName in dragDropGridAddonNames)
            {
                Services.AddonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, dragDropAddonName, handlePostRecieveEvent);
            }
        }

        protected override void unregisterAddonListeners()
        {
            Services.AddonLifecycle.UnregisterListener(handlePostRecieveEvent);
            Services.AddonLifecycle.UnregisterListener(handlePostRefresh);
            Services.AddonLifecycle.UnregisterListener(handlePostRequestedUpdate);
        }

        public override unsafe void handleManualUpdate()
        {
            try
            {
                // clear old data out
                neededItemIndexes.Clear();
                dragDropComponentNodes.Clear();

                updateAddonData();
                updateHighlights();
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, $"Error in {GetType().Name} manual update");
            }
        }

        private unsafe void handlePostRefresh(AddonEvent type, AddonArgs args)
        {
            try
            {
                // clear old data out
                neededItemIndexes.Clear();
                dragDropComponentNodes.Clear();

                updateAddonData();
                updateHighlights();
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, $"Error in {GetType().Name} post refresh");
            }
        }

        private unsafe void handlePostRecieveEvent(AddonEvent type, AddonArgs args)
        {
            try
            {
                // clear old data out
                neededItemIndexes.Clear();
                dragDropComponentNodes.Clear();

                updateAddonData();
                updateHighlights();
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, $"Error in {GetType().Name} post recieve event");
            }
        }

        private unsafe void handlePostRequestedUpdate(AddonEvent type, AddonArgs args)
        {
            try
            {
                // clear old data out
                neededItemIndexes.Clear();
                dragDropComponentNodes.Clear();

                updateAddonData();
                updateHighlights();
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, $"Error in {GetType().Name} post requested update");
            }
        }

        private unsafe void updateHighlights()
        {
            if (neededItemIndexes.Count == 0)
            {
                unmarkAllNodes();
                return;
            }

            for (var i = 0; i < dragDropComponentNodes.Count; i++)
            {
                var node = (AtkResNode*)dragDropComponentNodes[i];

                // not shown, skip
                if (!node->IsVisible()) continue;

                var nodeNeeded = neededItemIndexes.Contains(i);
                setNodeNeededMark(node, nodeNeeded, true, false);
            }
        }

        protected unsafe List<GameInventoryItem> GetItemsOrdered(ItemOrderModuleSorter* sorter, int tabIdx, int numPages, int outputPageSize)
        {
            if (sorter == null) return [];

            var orderedItemPtrs = Enumerable.Repeat(nint.Zero, sorter->Items.Count).ToList();

            for (var i = 0; i < sorter->Items.Count; i++)
            {
                var itemInfo = sorter->Items[i].Value;
                var itemIdx = GetSlotIndex(sorter, itemInfo);
                var invItem = GetInventoryItem(sorter, itemInfo);
                orderedItemPtrs[(int)itemIdx] = (nint)invItem;
            }

            var startIdx = Math.Max(tabIdx * (numPages * outputPageSize), 0);
            var endIdx = Math.Min(startIdx + (numPages * outputPageSize), orderedItemPtrs.Count);

            var visibleOrderedItems = orderedItemPtrs.Select(p => *(GameInventoryItem*)p).ToList()[startIdx..endIdx];

            return visibleOrderedItems;
        }

        protected unsafe void updateAddonData()
        {
            var tabIdx = getTabIndex();
            // not on a page with items
            if (tabIdx > maxTabIndex || tabIdx < 0) return;

            updateNeededItemIndexes();
            updateDragDropComponentNodes();
        }

        protected unsafe void updateNeededItemIndexes()
        {
            var items = GetItemsOrdered(sorter, getTabIndex(), pagesPerView, itemsPerPage);

            // calculate items needed in inventory
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var itemId = Plugin.ItemData.ConvertItemIdToHq(item.ItemId);

                if (Gearset.GetGearsetsNeedingItemById(itemId, Plugin.Gearsets, includeCollectedPrereqs: true).Count > 0)
                {
                    neededItemIndexes.Add(i);
                }
            }
        }

        protected unsafe void updateDragDropComponentNodes()
        {
            // find child addon containing the components
            var addons = getAddons();

            var itemGridAddons = addons
                .Where(a =>
                    dragDropGridAddonNames.Contains(((AtkUnitBase*)a)->NameString)
                    || ((AtkUnitBase*)a)->NameString == AddonName
                    )
                .OrderBy(a => ((AtkUnitBase*)a)->NameString);

            foreach (var gridAddon in itemGridAddons)
            {
                // Note: this works for retainers. Might be unstable
                var slots = getDragDropComponents(gridAddon);

                // construct list of drag drop nodes
                for (var i = 0; i < slots.Count; i++)
                {
                    var node = ((AtkComponentDragDrop*)slots[i])->OwnerNode;

                    // for addons that hide slots on wrong tab (retainer)
                    if (!node->IsVisible()) continue;

                    dragDropComponentNodes.Add((nint)node);
                }
            }
        }

        // thanks to @haselnussbomber for these methods
        protected unsafe long GetSlotIndex(ItemOrderModuleSorter* sorter, ItemOrderModuleSorterItemEntry* entry)
        {
            return entry->Slot + (sorter->ItemsPerPage * entry->Page);
        }

        protected unsafe InventoryItem* GetInventoryItem(ItemOrderModuleSorter* sorter, ItemOrderModuleSorterItemEntry* entry)
        {
            return GetInventoryItem(sorter, GetSlotIndex(sorter, entry));
        }

        protected unsafe InventoryItem* GetInventoryItem(ItemOrderModuleSorter* sorter, long slotIndex)
        {
            if (sorter == null)
                return null;

            if (sorter->Items.LongCount <= slotIndex)
                return null;

            var item = sorter->Items[slotIndex].Value;
            if (item == null)
                return null;

            var container = InventoryManager.Instance()->GetInventoryContainer(sorter->InventoryType + item->Page);
            if (container == null)
                return null;

            return container->GetInventorySlot(item->Slot);
        }

        protected override unsafe NodeBase? initializeCustomNode(AtkResNode* parentNodePtr, AtkUnitBase* addon)
        {
            // doesn't use custom nodes
            return null;
        }

        protected override void unlinkCustomNode(nint parentNodePtr, NodeBase node)
        {
            // doesn't use custom nodes
            return;
        }
    }
}
