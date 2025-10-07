using BisBuddy.Gear;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Inventory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.System;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Services.Addon.Containers
{
    public abstract class ContainerService<T>(
        AddonServiceDependencies<T> deps
        ) : AddonService<T>(deps) where T : class
    {
        protected readonly Dictionary<int, HighlightColor> neededItemColors = [];
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

        protected override float CustomNodeMaxY => float.MaxValue;

        protected override unsafe void registerAddonListeners()
        {
            addonLifecycle.RegisterListener(AddonEvent.PreDraw, AddonName, handlePreDraw);

            // ensure updates listened for for all addons (incl. children)
            foreach (var dragDropAddonName in dragDropGridAddonNames)
            {
                addonLifecycle.RegisterListener(AddonEvent.PreDraw, dragDropAddonName, handlePreDraw);
            }
        }

        protected override void unregisterAddonListeners()
        {
            addonLifecycle.UnregisterListener(handlePreDraw);
        }

        protected override void updateListeningStatus(bool effectsAssignments)
            => setListeningStatus(configurationService.HighlightInventories);

        private unsafe void handlePreDraw(AddonEvent type, AddonArgs args)
        {
            try
            {
                updateAddonData();
                updateHighlights();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error in post refresh");
            }
        }

        private unsafe void updateHighlights()
        {
            if (neededItemColors.Count == 0)
            {
                unmarkNodes();
                return;
            }

            for (var i = 0; i < dragDropComponentNodes.Count; i++)
            {
                var node = (AtkResNode*)dragDropComponentNodes[i];

                // not shown, skip
                if (!node->IsVisible()) continue;

                var nodeColor = neededItemColors.GetValueOrDefault(i);

                setNodeNeededMark(node, nodeColor, true, false);
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

            var startIdx = Math.Max(tabIdx * numPages * outputPageSize, 0);
            var endIdx = Math.Min(startIdx + numPages * outputPageSize, orderedItemPtrs.Count);

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
            neededItemColors.Clear();
            var items = GetItemsOrdered(sorter, getTabIndex(), pagesPerView, itemsPerPage);

            // calculate items needed in inventory
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var itemId = itemDataService.ConvertItemIdToHq(item.ItemId);
                var itemColor = gearsetsService.GetRequirementColor(
                    itemId,
                    includeCollected: configurationService.HighlightCollectedInInventory,
                    includeCollectedPrereqs: true,
                    includeObtainable: true
                    );

                if (itemColor is not null)
                    neededItemColors.Add(i, itemColor);
            }
        }

        protected unsafe void updateDragDropComponentNodes()
        {
            dragDropComponentNodes.Clear();

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
            return entry->Slot + sorter->ItemsPerPage * entry->Page;
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

        protected override unsafe NodeBase? initializeCustomNode(AtkResNode* parentNodePtr, AtkUnitBase* addon, HighlightColor color)
        {
            // doesn't use custom nodes
            return null;
        }
    }
}
