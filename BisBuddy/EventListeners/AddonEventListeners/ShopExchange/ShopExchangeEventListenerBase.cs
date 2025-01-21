using BisBuddy.Gear;
using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace BisBuddy.EventListeners.AddonEventListeners.ShopExchange
{
    internal abstract class ShopExchangeEventListenerBase(Plugin plugin)
        : AddonEventListenerBase(plugin, plugin.Configuration.HighlightShops)
    {
        // ADDON NODE IDS
        // list of items in the shop
        protected abstract uint AddonShopItemListNodeId { get; }
        // amount of child nodes for a valid displayed item in a shop
        protected abstract int AddonShopItemChildNodeCount { get; }
        // node containing the name of the item in the shop
        protected abstract uint AddonShopItemTextNameNodeId { get; }
        // node for the hover highlight on the shop item
        protected abstract uint AddonShopHoverNode { get; }
        // node for the custom highlight on the shop item
        protected abstract uint AddonCustomHighlightNodeId { get; }
        // node for the scrollbar on the shop item list
        protected abstract uint AddonScrollbarNodeId { get; }
        // node for the button on the scrollbar
        protected abstract uint AddonScrollbarButtonNodeId { get; }

        // ADDON ATKVALUE INDEXES
        // index of the number of items in shop
        protected abstract int AtkValueItemCountIndex { get; }
        // index of the first element in the item name list
        protected abstract int AtkValueItemNameListStartingIndex { get; }
        // index of the first element in the item id list
        protected abstract int AtkValueItemIdListStartingIndex { get; }

        private List<string> previousItemNameList = [];
        private List<string> neededShopItems = [];
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

        private unsafe void updateNeededShopItems(Span<AtkValue> atkValues)
        {
            var newNeededShopItems = new List<string>();
            var shopItemCount = atkValues[AtkValueItemCountIndex].Int;
            var maxItemIndex = AtkValueItemNameListStartingIndex + shopItemCount;

            if (atkValues.Length <= maxItemIndex)
            {
                throw new Exception($"{AddonName} AtkValue list is too short ({atkValues.Length}, {maxItemIndex})");
            }

            for (var i = 0; i < atkValues[AtkValueItemCountIndex].Int; i++)
            {
                var itemIdAtkValue = atkValues[AtkValueItemIdListStartingIndex + i];
                if (itemIdAtkValue.Type != ValueType.UInt)
                {
                    throw new Exception($"Item id at index {i} is a {itemIdAtkValue.Type}, not a UInt");
                };
                var itemId = itemIdAtkValue.UInt;

                if (Gearset.GetGearsetsNeedingItemById(itemId, Plugin.Gearsets).Count > 0)
                {
                    var itemNameAtkValue = atkValues[AtkValueItemNameListStartingIndex + i];
                    if (itemNameAtkValue.Type != ValueType.String
                        && itemNameAtkValue.Type != ValueType.ManagedString
                    )
                    {
                        throw new Exception($"Item name at index {i} is a {itemNameAtkValue.Type}, not a String/ManagedString");
                    };
                    newNeededShopItems.Add(SeString.Parse(itemNameAtkValue.String).TextValue);
                }
            }

            Services.Log.Debug($"Found {newNeededShopItems.Count} item(s) needed in shop");
            neededShopItems = newNeededShopItems;
        }

        public override unsafe void handleManualUpdate()
        {
            try
            {
                // get the addon
                var addon = (AtkUnitBase*)Services.GameGui.GetAddonByName(AddonName);
                if (addon == null || !addon->IsVisible) return;

                Services.Log.Verbose($"Updating {AddonName} after manual update");

                var atkValuesSpan = new Span<AtkValue>(addon->AtkValues, addon->AtkValuesCount);

                // update list of items needed from shop
                updateNeededShopItems(atkValuesSpan);

                // reset cache of previous item names to force redraw highlight
                previousItemNameList = [];
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
                previousItemNameList = [];
                previousScrollbarY = -1.0f;
                updateNeededShopItems(setupArgs.AtkValueSpan);
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
                previousItemNameList = [];
                previousScrollbarY = -1.0f;
                updateNeededShopItems(receiveEventArgs.AtkValueSpan);
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
                if (neededShopItems.Count == 0)
                {
                    // remove highlight on all items if there are no items needed
                    unmarkAllNodes();
                    return;
                };

                var shopExchangeItem = (AtkUnitBase*)Services.GameGui.GetAddonByName(AddonName);
                if (shopExchangeItem == null || !shopExchangeItem->IsVisible) return;

                var shopExchangeItemNode = new BaseNode(shopExchangeItem);

                var scrollbarNode = shopExchangeItemNode.GetNestedNode<AtkResNode>([
                    AddonShopItemListNodeId,
                    AddonScrollbarNodeId,
                    AddonScrollbarButtonNodeId
                    ]);

                // check if scroll location is the same, quit out if so
                if (scrollbarNode->Y == previousScrollbarY) return;
                previousScrollbarY = scrollbarNode->Y;

                var itemList = shopExchangeItemNode.GetComponentNode(AddonShopItemListNodeId).GetComponentNodes();

                // initialize previousItemNameList if it is not the same size as the current shop list
                if (itemList.Count != previousItemNameList.Count)
                {
                    previousItemNameList = Enumerable.Repeat(string.Empty, itemList.Count).ToList();
                }

                for (var i = 0; i < itemList.Count; i++)
                {
                    // get list of nodes for elements in the shop list
                    var itemNode = itemList[i];
                    var itemNodePtr = itemNode.GetPointer();

                    // skip if component does not correspond to an item (has shopItemNodeListSize children)
                    if (
                        itemNode.GetChildCount() != AddonShopItemChildNodeCount
                        && itemNode.GetNode<AtkTextNode>(AddonCustomNodeId) == null
                        )
                    {
                        continue;
                    }

                    // get the text node of the item that contains the name
                    var itemNameNodePtr = itemNode.GetComponentNode(AddonShopItemTextNameNodeId).GetPointer()->GetAsAtkTextNode();

                    var itemName = MemoryHelper
                        .ReadSeStringNullTerminated((nint)itemNameNodePtr->GetText())
                        .TextValue;

                    // same item, skip
                    if (previousItemNameList[i] == itemName) continue;

                    previousItemNameList[i] = itemName;

                    var itemNeeded = neededShopItems
                        .Any(
                            n => itemName.EndsWith("...") // is the item name displayed truncated?
                            ? n.StartsWith(itemName.Replace("...", "")) // only check starts with
                            : n == itemName // check whole thing
                        );

                    setNodeNeededMark((AtkResNode*)itemNodePtr, itemNeeded, true, true);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in handlePreDraw");
            }
        }

        protected override unsafe nint initializeCustomNode(nint parentNodePtr)
        {
            AtkNineGridNode* customHighlightNode = null;
            try
            {
                var hoverNode = ((AtkComponentNode*)parentNodePtr)->GetComponent()->UldManager.SearchNodeById(AddonShopHoverNode);
                customHighlightNode = UiHelper.CloneNineGridNode(AddonCustomNodeId, (AtkNineGridNode*)hoverNode);
                customHighlightNode->SetAlpha(255);
                customHighlightNode->AddRed = -255;
                customHighlightNode->AddBlue = -255;
                customHighlightNode->AddGreen = 255;
                customHighlightNode->DrawFlags |= 0x01; // force a redraw ("dirty flag")
                UiHelper.LinkNodeAfterTargetNode((AtkResNode*)customHighlightNode, (AtkComponentNode*)parentNodePtr, hoverNode);
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
