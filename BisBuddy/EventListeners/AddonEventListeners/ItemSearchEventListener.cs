using BisBuddy.Gear;
using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.EventListeners.AddonEventListeners
{
    // marketboard
    internal class ItemSearchEventListener(Plugin plugin)
        : AddonEventListenerBase(plugin, plugin.Configuration.HighlightMarketboard)
    {
        public override string AddonName => "ItemSearch";

        // ADDON NODE IDS
        // id of the text node for the list item name
        internal static readonly uint AddonItemNameTextNodeId = 13;
        // id of the hover highlight node
        internal static readonly uint AddonHoverHighlightNodeId = 15;
        // id of the list of items being displayed (and the scrollbar)
        internal static readonly uint AddonItemListNodeId = 139;
        // id of the scrollbar in the list of items
        internal static readonly uint AddonItemScrollbarNodeId = 4;
        // id of the scroll button on the bar
        internal static readonly uint AddonItemScrollButtonNodeId = 2;

        // what items are needed from the marketboard listings
        private readonly HashSet<int> neededItemIndexes = [];
        // the item list from the last refresh
        private List<string> previousItemNames = [];
        // the previous Y value of the scrollbar
        private float previousScrollbarY = -1.0f;

        protected override void registerAddonListeners()
        {
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, AddonName, handlePostRequestedUpdate);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, AddonName, handlePostRefresh);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, AddonName, handlePreDraw);
        }
        protected override void unregisterAddonListeners()
        {
            Services.AddonLifecycle.UnregisterListener(handlePostRequestedUpdate);
            Services.AddonLifecycle.UnregisterListener(handlePostRefresh);
            Services.AddonLifecycle.UnregisterListener(handlePreDraw);
        }

        public override void handleManualUpdate()
        {
            updateNeededItems();
        }

        private unsafe void handlePostRequestedUpdate(AddonEvent type, AddonArgs args)
        {
            updateNeededItems();
        }

        private unsafe void handlePostRefresh(AddonEvent type, AddonArgs args)
        {
            updateNeededItems();
        }

        private void handlePreDraw(AddonEvent type, AddonArgs args)
        {
            handleUpdate();
        }

        private unsafe void handleUpdate()
        {
            try
            {
                if (neededItemIndexes.Count == 0)
                { // no items needed from list, return
                    unmarkAllNodes();
                    return;
                };
                var addon = (AddonItemSearch*)Services.GameGui.GetAddonByName(AddonName);
                if (addon == null || !addon->IsVisible) return; // addon not visible/rendered
                if (addon->ResultsList == null || addon->ResultsList->ListLength == 0) return; // no items in search

                var itemList = addon->ResultsList;

                // list length doesn't match up, something must have changed
                if (itemList->ListLength != previousItemNames.Count)
                {
                    previousItemNames = Enumerable.Repeat(string.Empty, itemList->ListLength).ToList();
                }

                var addonNode = new BaseNode(&addon->AtkUnitBase);
                var scrollbarNode = addonNode.GetNestedNode<AtkResNode>([
                    AddonItemListNodeId,
                    AddonItemScrollbarNodeId,
                    AddonItemScrollButtonNodeId
                    ]);

                if (scrollbarNode == null)
                {
                    Services.Log.Warning($"{GetType().Name}: Failed to find scrollbar node");
                }
                else
                {
                    // scrollbar in same position, nodes haven't changed
                    if (scrollbarNode->Y == previousScrollbarY) return;
                    previousScrollbarY = scrollbarNode->Y;
                }

                var firstListItemIndex = -1;
                for (var i = 0; i < itemList->ListLength; i++)
                {
                    var listItem = itemList->ItemRendererList[i];
                    var listItemIndex = listItem.AtkComponentListItemRenderer->ListItemIndex;
                    if (i == 0) firstListItemIndex = listItemIndex;
                    // display list has "looped back" to the beginning (too many items to display), break out
                    else if (listItemIndex == firstListItemIndex) break;

                    var itemNeeded = neededItemIndexes.Contains(listItemIndex);
                    setNodeNeededMark((AtkResNode*)listItem.AtkComponentListItemRenderer->OwnerNode, itemNeeded, true, true);
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, $"{GetType().Name}: Failed to update PostDraw");
            }
        }

        private unsafe void updateNeededItems()
        {
            try
            {
                neededItemIndexes.Clear();
                var agent = AgentItemSearch.Instance();

                if (agent == null) return;

                for (var i = 0; i < agent->ListingPageItemCount; i++)
                {
                    var nqItemId = agent->ListingPageItems[i].ItemId;
                    var hqItemId = Plugin.ItemData.ConvertItemIdToHq(nqItemId);

                    var nqOrHqNeeded = (
                        Gearset.GetGearsetsNeedingItemById(nqItemId, Plugin.Gearsets).Count > 0 // either the NQ is needed
                        || (hqItemId != nqItemId // or there is a HQ variant and it is needed
                            && Gearset.GetGearsetsNeedingItemById(hqItemId, Plugin.Gearsets).Count > 0)
                        );
                    if (nqOrHqNeeded) // needed at some level
                    {
                        var itemName = Plugin.ItemData.GetItemNameById(nqItemId);
                        neededItemIndexes.Add(i);
                    }
                }

                // ensure this update refreshes the draws by resetting caches
                previousItemNames.Clear();
                previousScrollbarY = -1.0f;
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Failed to update needed items");
            }
        }

        protected override unsafe nint initializeCustomNode(nint parentNodePtr)
        {
            AtkNineGridNode* customHighlightNode = null;
            try
            {
                var parentNode = (AtkComponentNode*)parentNodePtr;
                var hoverNode = ((AtkComponentNode*)parentNodePtr)
                    ->GetComponent()
                    ->UldManager
                    .SearchNodeById(AddonHoverHighlightNodeId)
                    ->GetAsAtkNineGridNode();
                customHighlightNode = UiHelper.CloneNineGridNode(AddonCustomNodeId, hoverNode);
                customHighlightNode->SetAlpha(255);
                customHighlightNode->AddRed = -255;
                customHighlightNode->AddBlue = -255;
                customHighlightNode->AddGreen = 255;
                customHighlightNode->DrawFlags |= 0x01; // force a redraw ("dirty flag")
                UiHelper.LinkNodeAfterTargetNode((AtkResNode*)customHighlightNode, parentNode, (AtkResNode*)hoverNode);

                return (nint)customHighlightNode;
            }
            catch (Exception ex)
            {
                if (customHighlightNode != null) UiHelper.FreeNineGridNode(customHighlightNode);
                Services.Log.Error(ex, "Failed to initialize custom node");
                return 0;
            }
        }
        protected override unsafe void unlinkCustomNode(nint nodePtr)
        {
            var node = (AtkResNode*)nodePtr;
            var addon = (AtkUnitBase*)Services.GameGui.GetAddonByName(AddonName);
            if (addon == null) return; // addon isn't loaded, nothing to unlink
            if (node->ParentNode == null) return; // node isn't linked, nothing to unlink

            UiHelper.UnlinkNode(node, (AtkComponentNode*)node->ParentNode);
        }
    }
}
