using BisBuddy.Gear;
using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.System;
using Serilog;
using System;
using System.Collections.Generic;
using ComponentNode = BisBuddy.Util.ComponentNode;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace BisBuddy.Services.Addon.ShopExchange
{
    public abstract class ShopExchangeService<T>(AddonServiceDependencies<T> deps)
        : AddonService<T>(deps) where T : class
    {
        // ADDON NODE IDS
        // list of items in the shop
        protected abstract uint AddonShopItemListNodeId { get; }
        // node for the hover highlight on the shop item
        protected abstract uint AddonShopHoverNodeId { get; }
        // node for the custom highlight on the shop item
        protected abstract uint AddonCustomHighlightNodeId { get; }
        // node for hover highlight on offset shield shop items
        protected abstract uint AddonShopShieldHoverNodeId { get; }
        // node for the scrollbar on the shop item list
        protected abstract uint AddonScrollbarNodeId { get; }
        // node for the button on the scrollbar
        protected abstract uint AddonScrollbarButtonNodeId { get; }
        // node on shield item entries that contains info, and is offset in by L bar
        protected abstract uint AddonShieldInfoResNodeId { get; }
        // text node that displays when there are no items to be listed
        protected abstract uint NoItemsTextNodeId { get; }
        // if an indented is in the item list, what is item index?
        // should be constant throughout all shops now
        protected int AddonShieldIndex = 1;

        // ADDON ATKVALUE INDEXES
        // index of the number of items in shop
        protected abstract int AtkValueItemCountIndex { get; }
        // index of the first element in the item id list
        protected abstract int AtkValueItemIdListStartingIndex { get; }
        // index of the first element in the filter display list
        protected abstract int AtkValueFilteredItemsListStartingIndex { get; }
        // max value for the filter display list indicating item is visible (diff shops have diff values)
        protected abstract uint AtkValueFilteredItemsListVisibleMaxValue { get; }

        private readonly Dictionary<int, HighlightColor> neededShopItemIndexColors = [];
        private bool shieldInAtkValues = false;

        protected override void registerAddonListeners()
        {
            addonLifecycle.RegisterListener(AddonEvent.PreDraw, AddonName, handlePreDraw);
        }

        protected override void unregisterAddonListeners()
        {
            addonLifecycle.UnregisterListener(handlePreDraw);
        }

        protected override void updateListeningStatus(bool effectsAssignments)
            => setListeningStatus(configurationService.HighlightShops);

        private unsafe void handlePreDraw(AddonEvent type, AddonArgs args)
        {
            try
            {
                var addon = (AtkUnitBase*)((AddonDrawArgs)args).Addon.Address;

                var shopExchangeNode = new BaseNode(addon);

                var noEntriesTextNode = shopExchangeNode.GetNode<AtkTextNode>(NoItemsTextNodeId);

                updateNeededShopItemIndexes(addon->AtkValuesSpan);
                // skip if no items are needed in this shop
                if (neededShopItemIndexColors.Count == 0 || noEntriesTextNode != null && noEntriesTextNode->IsVisible())
                {
                    // remove highlight on all items if there are no items needed
                    unmarkNodes();
                    return;
                };



                if (addon == null || !addon->IsVisible) return;

                var itemTreeListComponent = (AtkComponentTreeList*)shopExchangeNode
                    .GetComponentNode(AddonShopItemListNodeId)
                    .GetPointer()->Component;

                for (var i = 0; i < itemTreeListComponent->Items.Count; i++)
                {
                    // get list of nodes for elements in the shop list
                    var itemNodePtr = itemTreeListComponent->Items[i].Value->Renderer->OwnerNode;
                    var itemNode = new ComponentNode(itemNodePtr);

                    var itemNodeComponent = (AtkComponentListItemRenderer*)itemNodePtr->Component;
                    var nodeListIndex = itemNodeComponent->ListItemIndex;

                    var itemColor = neededShopItemIndexColors.GetValueOrDefault(nodeListIndex);

                    setNodeNeededMark((AtkResNode*)itemNodePtr, itemColor, true, true);
                }

                foreach (var entry in CustomNodes)
                {
                    if (!entry.Value.Node.IsVisible)
                        continue;

                    var parentNode = (AtkComponentNode*)entry.Key;
                    var parentNodeComponent = (AtkComponentListItemRenderer*)parentNode->Component;

                    if (parentNodeComponent->ListItemIndex >= itemTreeListComponent->Items.Count)
                        setNodeNeededMark((AtkResNode*)parentNode, null, true, true);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in handlePreDraw");
            }
        }

        private unsafe void updateNeededShopItemIndexes(Span<AtkValue> atkValues)
        {
            neededShopItemIndexColors.Clear();
            shieldInAtkValues = false;
            var shopItemCount = atkValues[AtkValueItemCountIndex].Int;
            var maxItemIndex = AtkValueItemIdListStartingIndex + shopItemCount;

            if (atkValues.Length <= maxItemIndex)
            {
                throw new Exception($"{AddonName} AtkValue list is too short ({atkValues.Length}, {maxItemIndex})");
            }

            // handle INDENTED shields
            var endOfItemIdList = atkValues[maxItemIndex];
            var endOfItemFilteredList = atkValues[AtkValueFilteredItemsListStartingIndex + shopItemCount];
            if (
                endOfItemIdList.Type == ValueType.UInt // has another value at the end
                && endOfItemIdList.UInt > 0             // shield at the end
                && endOfItemFilteredList.Type == ValueType.UInt // visibility value for shield
                && endOfItemFilteredList.UInt <= AtkValueFilteredItemsListVisibleMaxValue // is visible
                && itemDataService.ItemIsShield(endOfItemIdList.UInt) // item is a shield
                )
            {
                // shield found
                shieldInAtkValues = true;

                // add if needed
                var shopItemColor = gearsetsService.GetRequirementColor(
                    endOfItemIdList.UInt,
                    includeObtainable: true
                    );
                if (shopItemColor is not null)
                    neededShopItemIndexColors.Add(AddonShieldIndex, shopItemColor);
            }

            // handle other items
            for (var i = 0; i < atkValues[AtkValueItemCountIndex].Int; i++)
            {
                var itemIdAtkValue = atkValues[AtkValueItemIdListStartingIndex + i];
                if (itemIdAtkValue.Type != ValueType.UInt)
                {
                    throw new Exception($"Item id at index {i}/{AtkValueItemIdListStartingIndex + i} is a {itemIdAtkValue.Type}, not a UInt");
                };
                var itemId = itemIdAtkValue.UInt;
                var shieldOffset = shieldInAtkValues && i >= AddonShieldIndex
                    ? 1  // shield visible and idx after where shield goes
                    : 0; // either shield not visible or before where shield goes

                var itemColor = gearsetsService.GetRequirementColor(
                    itemId,
                    includeObtainable: true
                    );

                if (itemColor is not null)
                {
                    var filteredIndex = getFilteredIndex(i, atkValues);
                    if (filteredIndex >= 0)
                        neededShopItemIndexColors.Add(filteredIndex + shieldOffset, itemColor);
                }
            }
        }

        private unsafe int getFilteredIndex(int index, Span<AtkValue> atkValues)
        {
            /*
             * When a filter is applied to the shop, the list of items in AtkValues remains the same. 
             * However, the components to be filtered are no longer rendered, throwing the ListItemIndex
             * of a ListItemRenderer component node off from that list. To fix this, there is a list of
             * ints in the AtkValues for each of the items that indicates if it is shown or not.
             * This method converts a unfiltered index to the filtered one using the filter list data.
            */

            if (atkValues.Length <= AtkValueFilteredItemsListStartingIndex)
                return -1;

            // not visible
            if (
                atkValues[index + AtkValueFilteredItemsListStartingIndex].Int
                > AtkValueFilteredItemsListVisibleMaxValue
                ) return -1;

            var itemCount = atkValues[AtkValueItemCountIndex];
            var visibleCount = 0;
            for (var i = 0; i <= itemCount.Int; i++)
            {
                var value = atkValues[i + AtkValueFilteredItemsListStartingIndex];
                if (value.Type != ValueType.UInt)
                {
                    logger.Error($"Filter list item type \"{value.Type}\" unexpected at index {i}/{AtkValueFilteredItemsListStartingIndex + i}");
                    return -1;
                }

                // reached the index
                if (i >= index) return visibleCount;

                // this index is displayed by the filter
                if (value.UInt <= AtkValueFilteredItemsListVisibleMaxValue)
                    visibleCount++;
            }

            // didnt find, must not display
            logger.Warning($"No compressed index found for \"{index}\"");
            return -1;
        }

        private unsafe bool isIndentedShieldNode(ComponentNode parentNode)
        {
            var shieldInfoNode = parentNode.GetNode<AtkResNode>(AddonShieldInfoResNodeId);
            return
                shieldInfoNode->Type == NodeType.Res
                && shieldInfoNode->X != 0
            ;
        }

        protected override unsafe NodeBase? initializeCustomNode(AtkResNode* parentNodePtr, AtkUnitBase* addon, HighlightColor color)
        {
            NineGridNode? customNode = null;
            try
            {
                var parentNode = new ComponentNode((AtkComponentNode*)parentNodePtr);

                var hoverNode = isIndentedShieldNode(parentNode)
                    ? parentNode.GetNode<AtkNineGridNode>(AddonShopShieldHoverNodeId)
                    : parentNode.GetNode<AtkNineGridNode>(AddonShopHoverNodeId);

                customNode = UiHelper.CloneHighlightNineGridNode(
                    hoverNode,
                    color.CustomNodeColor,
                    color.CustomNodeAlpha(configurationService.BrightListItemHighlighting)
                    ) ?? throw new Exception($"Could not clone node \"{hoverNode->NodeId}\"");

                // mark as dirty
                customNode.MarkDirty();

                // attach it to the addon
                nativeController.AttachNode(customNode, addon->RootNode, NodePosition.AsLastChild);

                return customNode;
            }
            catch (Exception ex)
            {
                customNode?.Dispose();
                logger.Error(ex, "Failed to create custom highlight node");
                return null;
            }
        }

        protected override unsafe void unlinkCustomNode(nint parentNodePtr, NodeBase node)
        {
            var addon = gameGui.GetAddonByName(AddonName);

            if (addon == nint.Zero)
                return;

            if (parentNodePtr == nint.Zero)
                return;

            nativeController.DetachNode(node);
        }
    }
}
