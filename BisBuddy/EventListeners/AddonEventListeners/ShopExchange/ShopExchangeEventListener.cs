using BisBuddy.Gear;
using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Serilog;
using System;
using System.Collections.Generic;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace BisBuddy.EventListeners.AddonEventListeners.ShopExchange
{
    public abstract class ShopExchangeEventListener(Plugin plugin)
        : AddonEventListener(plugin, plugin.Configuration.HighlightShops)
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

        private unsafe void updateNeededShopItemIndexes(Span<AtkValue> atkValues)
        {
            neededShopItemIndexes.Clear();
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
                && Plugin.ItemData.ItemIsShield(endOfItemIdList.UInt) // item is a shield
                )
            {
                // shield found
                shieldInAtkValues = true;

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
                    Services.Log.Error($"[{GetType().Name}] Filter list item type \"{value.Type}\" unexpected");
                    return -1;
                }

                // reached the index
                if (i >= index) return visibleCount;

                // this index is displayed by the filter
                if (value.UInt <= AtkValueFilteredItemsListVisibleMaxValue)
                    visibleCount++;
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

        protected override unsafe NodeBase? initializeCustomNode(AtkResNode* parentNodePtr, AtkUnitBase* addon)
        {
            NineGridNode? customNode = null;
            try
            {
                var parentNode = new ComponentNode((AtkComponentNode*)parentNodePtr);

                var hoverNode = isIndentedShieldNode(parentNode)
                    ? parentNode.GetNode<AtkNineGridNode>(AddonShopShieldHoverNodeId)
                    : parentNode.GetNode<AtkNineGridNode>(AddonShopHoverNodeId);

                customNode = UiHelper.CloneNineGridNode(
                    AddonCustomNodeId,
                    hoverNode,
                    Plugin.Configuration.CustomNodeAddColor,
                    Plugin.Configuration.CustomNodeMultiplyColor,
                    Plugin.Configuration.CustomNodeAlpha
                    ) ?? throw new Exception($"Could not clone node \"{hoverNode->NodeId}\"");

                // mark as dirty
                customNode.InternalNode->DrawFlags |= 0x1;

                // attach it to the addon
                Services.NativeController.AttachToComponent(customNode, addon, ((AtkComponentNode*)parentNodePtr)->Component, (AtkResNode*)hoverNode, NodePosition.BeforeTarget);

                return customNode;
            }
            catch (Exception ex)
            {
                customNode?.Dispose();
                Services.Log.Error(ex, "Failed to create custom highlight node");
                return null;
            }
        }

        protected override unsafe void unlinkCustomNode(nint parentNodePtr, NodeBase node)
        {
            var addon = Services.GameGui.GetAddonByName(AddonName);

            if (addon == nint.Zero)
                return;

            if (parentNodePtr == nint.Zero)
                return;

            Services.NativeController.DetachFromComponent(node, (AtkUnitBase*)addon, ((AtkComponentNode*)parentNodePtr)->Component);
        }
    }
}
