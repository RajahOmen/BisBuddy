using BisBuddy.Gear;
using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Serilog;
using System;
using System.Collections.Generic;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace BisBuddy.EventListeners.AddonEventListeners.ShopExchange
{
    internal abstract class ShopExchangeEventListenerBase(Plugin plugin)
        : AddonEventListenerBase(plugin, plugin.Configuration.HighlightShops)
    {
        // ADDON NODE IDS
        // list of items in the shop
        protected abstract uint AddonShopItemListNodeId { get; }
        // node for the hover highlight on the shop item
        protected abstract uint AddonShopHoverNodeId { get; }
        // node for the custom highlight on the shop item
        protected abstract uint AddonCustomHighlightNodeId { get; }
        // node for hover highlight on offset shield shop items
        protected uint AddonShopShieldHoverNodeId = 13;
        // node for the scrollbar on the shop item list
        protected abstract uint AddonScrollbarNodeId { get; }
        // node for the button on the scrollbar
        protected abstract uint AddonScrollbarButtonNodeId { get; }
        // node on shield item entries that contains info, and is offset in by L bar
        protected virtual uint AddonShieldInfoResNodeId { get; } = 3;
        // if an indented is in the item list, what is item index?
        // should be constant throughout all shops now
        protected int AddonShieldIndex = 1;

        // ADDON ATKVALUE INDEXES
        // index of the number of items in shop
        protected abstract int AtkValueItemCountIndex { get; }
        // index of the first element in the item name list
        protected abstract int AtkValueItemNameListStartingIndex { get; }
        // index of the first element in the item id list
        protected abstract int AtkValueItemIdListStartingIndex { get; }
        // index of the first element in the filter display list
        protected virtual int AtkValueFilteredItemsListStartingIndex { get; } = 1551;
        // value for the filter display list indicating item is visible
        protected virtual uint AtkValueFilteredItemsListVisibleValue { get; } = 0;


        private readonly HashSet<int> neededShopItemIndexes = [];
        private bool shieldInAtkValues = false;
        // the previous Y value of the scrollbar
        private float previousScrollbarY = -1.0f;

        protected override void registerAddonListeners()
        {
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, AddonName, handlePostSetup);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, AddonName, handlePostRefresh);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, AddonName, handlePostUpdate);
        }

        protected override void unregisterAddonListeners()
        {
            Services.AddonLifecycle.UnregisterListener(handlePostSetup);
            Services.AddonLifecycle.UnregisterListener(handlePostRefresh);
            Services.AddonLifecycle.UnregisterListener(handlePostUpdate);
        }

        protected override unsafe void unlinkCustomNode(nint nodePtr)
        {
            var node = (AtkResNode*)nodePtr;
            var addon = (AtkUnitBase*)Services.GameGui.GetAddonByName(AddonName);
            if (addon == null) return; // addon isn't loaded, nothing to unlink
            if (node->ParentNode == null) return; // node isn't linked, nothing to unlink

            UiHelper.UnlinkNode(node, (AtkComponentNode*)node->ParentNode);
        }

        private unsafe void updateNeededShopItemIndexes(Span<AtkValue> atkValues)
        {
            neededShopItemIndexes.Clear();
            shieldInAtkValues = false;
            var shopItemCount = atkValues[AtkValueItemCountIndex].Int;
            var maxItemIndex = AtkValueItemNameListStartingIndex + shopItemCount;

            if (atkValues.Length <= maxItemIndex)
            {
                throw new Exception($"{AddonName} AtkValue list is too short ({atkValues.Length}, {maxItemIndex})");
            }

            // handle INDENTED shields
            var itemListCount = atkValues[AtkValueItemCountIndex].Int;
            var endOfItemIdList = atkValues[AtkValueItemIdListStartingIndex + itemListCount];
            var endOfItemFilteredList = atkValues[AtkValueFilteredItemsListStartingIndex + itemListCount];
            if (
                endOfItemIdList.Type == ValueType.UInt // has another value at the end
                && endOfItemIdList.UInt > 0             // shield at the end
                && endOfItemFilteredList.Type == ValueType.UInt // visibility value for shield
                && endOfItemFilteredList.UInt == AtkValueFilteredItemsListVisibleValue // is visible
                )
            {
                // shield found
                shieldInAtkValues = true;

                Services.Log.Verbose($"Indented shield found!");

                // add if needed
                if (Gearset.GetGearsetsNeedingItemById(endOfItemIdList.UInt, Plugin.Gearsets).Count > 0)
                {
                    neededShopItemIndexes.Add(AddonShieldIndex);
                }
            }

            // handle other items
            for (var i = 0; i < atkValues[AtkValueItemCountIndex].Int; i++)
            {
                var itemIdAtkValue = atkValues[AtkValueItemIdListStartingIndex + i];
                if (itemIdAtkValue.Type != ValueType.UInt)
                {
                    throw new Exception($"Item id at index {i} is a {itemIdAtkValue.Type}, not a UInt");
                };
                var itemId = itemIdAtkValue.UInt;
                var shieldOffset = shieldInAtkValues && i >= AddonShieldIndex
                    ? 1  // shield visible and idx after where shield goes
                    : 0; // either shield not visible or before where shield goes
                if (Gearset.GetGearsetsNeedingItemById(itemId, Plugin.Gearsets).Count > 0)
                {
                    var filteredIndex = getFilteredIndex(i, atkValues);
                    if (filteredIndex >= 0) neededShopItemIndexes.Add(filteredIndex + shieldOffset);
                }
            }

            Services.Log.Debug($"Found {neededShopItemIndexes.Count} item(s) needed in shop");
        }

        public override unsafe void handleManualUpdate()
        {
            try
            {
                // get the addon
                var addon = (AtkUnitBase*)Services.GameGui.GetAddonByName(AddonName);
                if (addon == null || !addon->IsVisible) return;

                var atkValuesSpan = new Span<AtkValue>(addon->AtkValues, addon->AtkValuesCount);

                // update list of items needed from shop
                updateNeededShopItemIndexes(atkValuesSpan);

                // reset cache of previous item names to force redraw highlight
                previousScrollbarY = -1.0f;
            }
            catch (Exception e)
            {
                Services.Log.Error(e, $"Failed to handle GearsetsUpdate for {AddonName}");
            }
        }

        private unsafe void handlePostSetup(AddonEvent type, AddonArgs args)
        {
            try
            {
                // skip if type is not correct
                if (type != AddonEvent.PostSetup) return;
                var setupArgs = (AddonSetupArgs)args;
                previousScrollbarY = -1.0f;
                updateNeededShopItemIndexes(setupArgs.AtkValueSpan);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error in handlePostSetup");
            }
        }

        private unsafe void handlePostRefresh(AddonEvent type, AddonArgs args)
        {
            try
            {
                // skip if type is not correct
                if (type != AddonEvent.PostRefresh) return;
                var receiveEventArgs = (AddonRefreshArgs)args;
                previousScrollbarY = -1.0f;
                updateNeededShopItemIndexes(receiveEventArgs.AtkValueSpan);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in handlePostRefresh");
            }
        }

        private unsafe void handlePostUpdate(AddonEvent type, AddonArgs args)
        {
            try
            {
                // skip if no items are needed in this shop
                if (neededShopItemIndexes.Count == 0)
                {
                    // remove highlight on all items if there are no items needed
                    unmarkAllNodes();
                    return;
                };

                var addon = (AtkUnitBase*)Services.GameGui.GetAddonByName(AddonName);
                if (addon == null || !addon->IsVisible) return;

                var shopExchangeItemNode = new BaseNode(addon);

                var scrollbarNode = shopExchangeItemNode.GetNestedNode<AtkResNode>([
                    AddonShopItemListNodeId,
                    AddonScrollbarNodeId,
                    AddonScrollbarButtonNodeId
                    ]);

                // check if scroll location is the same, quit out if so
                if (scrollbarNode->Y == previousScrollbarY) return;
                previousScrollbarY = scrollbarNode->Y;

                var itemTreeListComponent = (AtkComponentTreeList*)shopExchangeItemNode
                    .GetComponentNode(AddonShopItemListNodeId)
                    .GetPointer()->Component;

                for (var i = 0; i < itemTreeListComponent->Items.Count; i++)
                {
                    // get list of nodes for elements in the shop list
                    var itemNodePtr = itemTreeListComponent->Items[i].Value->Renderer->OwnerNode;
                    var itemNode = new ComponentNode(itemNodePtr);

                    var itemNodeComponent = (AtkComponentListItemRenderer*)itemNodePtr->Component;
                    var nodeListIndex = itemNodeComponent->ListItemIndex;
                    var itemNeeded = neededShopItemIndexes.Contains(nodeListIndex);

                    setNodeNeededMark((AtkResNode*)itemNodePtr, itemNeeded, true, true);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in handlePreDraw");
            }
        }

        private unsafe int getFilteredIndex(int index, Span<AtkValue> atkValues)
        {
            /*
             * When a filter is applied to the shop, the list of items in AtkValues remains the same. 
             * However, the components to be filtered are no longer rendered, throwing the ListItemIndex
             * of a ListItemRenderer component node off from that list. To fix this, there is a list of
             * ints in the AtkValues for each of the items that indicates if it is shown or not
             * (0 if shown, 2 if not). This method converts a unfiltered index to the filtered one
             * using the filter list data.
            */

            if (atkValues.Length <= AtkValueFilteredItemsListStartingIndex)
                return -1;

            // not visible
            if (
                atkValues[index + AtkValueFilteredItemsListStartingIndex].Int
                != AtkValueFilteredItemsListVisibleValue
                ) return -1;

            var itemCount = atkValues[AtkValueItemCountIndex];
            var zeroCount = 0;
            for (var i = 0; i <= itemCount.Int; i++)
            {
                var value = atkValues[i + AtkValueFilteredItemsListStartingIndex];
                if (value.Type != ValueType.UInt)
                {
                    Services.Log.Error($"[{GetType().Name}] Filter list item type \"{value.Type}\" unexpected");
                    return -1;
                }

                // reached the index
                if (i >= index) return zeroCount;

                // this index is displayed by the filter
                if (value.UInt == AtkValueFilteredItemsListVisibleValue)
                    zeroCount++;
            }

            // didnt find, must not display
            Services.Log.Warning($"[{GetType().Name}] No compressed index found for \"{index}\"");
            return -1;
        }

        private unsafe bool isIndentedShieldNode(ComponentNode parentNode)
        {
            var shieldInfoNode = parentNode.GetNode<AtkResNode>(AddonShieldInfoResNodeId);
            return (
                shieldInfoNode->Type == NodeType.Res
                && shieldInfoNode->X != 0
            );
        }

        protected override unsafe nint initializeCustomNode(nint parentNodePtr)
        {
            AtkNineGridNode* customHighlightNode = null;
            try
            {
                var parentComponentNode = new ComponentNode((AtkComponentNode*)parentNodePtr);

                // shield nodes have different structures
                var hoverNode = isIndentedShieldNode(parentComponentNode)
                    ? parentComponentNode.GetNode<AtkNineGridNode>(AddonShopShieldHoverNodeId)
                    : parentComponentNode.GetNode<AtkNineGridNode>(AddonShopHoverNodeId);

                customHighlightNode = UiHelper.CloneNineGridNode(AddonCustomNodeId, hoverNode);
                customHighlightNode->SetAlpha(255);
                customHighlightNode->AddRed = -255;
                customHighlightNode->AddBlue = -255;
                customHighlightNode->AddGreen = 255;
                customHighlightNode->DrawFlags |= 0x01; // force a redraw ("dirty flag")
                UiHelper.LinkNodeAfterTargetNode((AtkResNode*)customHighlightNode, (AtkComponentNode*)parentNodePtr, (AtkResNode*)hoverNode);
                return (nint)customHighlightNode;
            }
            catch (Exception ex)
            {
                if (customHighlightNode != null) UiHelper.FreeNineGridNode(customHighlightNode);
                Services.Log.Error(ex, "Failed to create custom highlight node");
                return nint.Zero;
            }
        }
    }
}
