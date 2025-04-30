using BisBuddy.Gear;
using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkComponentList;

namespace BisBuddy.EventListeners.AddonEventListeners
{
    // marketboard
    public class ItemSearchResultEventListener(Plugin plugin)
        : AddonEventListener(plugin, plugin.Configuration.HighlightMarketboard)
    {
        public override string AddonName => "ItemSearchResult";

        // ADDON NODE IDS
        // id of the text node for the list item name
        public static readonly uint AddonItemQualityImageNodeId = 3;
        // id of the hover highlight node
        public static readonly uint AddonHoverHighlightNodeId = 14;
        // id of the text node containing the quantity of the item being sold in the result
        public static readonly uint AddonItemQuantityNodeId = 6;

        // if nq or hq is needed for the selected item
        private int nqNeeded = 0;
        private int hqNeeded = 0;

        protected override float CustomNodeMaxY => 240f;

        protected override void registerAddonListeners()
        {
            Services.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, AddonName, handlePreDraw);
        }
        protected override void unregisterAddonListeners()
        {
            Services.AddonLifecycle.UnregisterListener(handlePreDraw);
        }

        private unsafe void handlePreDraw(AddonEvent type, AddonArgs args)
        {
            try
            {
                updateNeedQualities();
                if (hqNeeded == 0 && nqNeeded == 0)
                { // no variant needed for this selection, unmark and quit
                    unmarkNodes();
                    return;
                };
                var addon = (AddonItemSearchResult*)Services.GameGui.GetAddonByName(AddonName);

                // addon not visible/rendered
                if (addon == null || !addon->IsVisible)
                    return;

                // no items for sale
                if (addon->Results == null || addon->Results->ListLength == 0)
                    return;

                var listingList = addon->Results;

                var addonNode = new BaseNode(&addon->AtkUnitBase);

                var listings = new List<ListItem>();

                var firstListItemIndex = 0;
                for (var i = 0; i < listingList->ListLength; i++)
                {
                    var listItem = listingList->ItemRendererList[i];
                    var listItemIndex = listItem.AtkComponentListItemRenderer->ListItemIndex;

                    if (i == 0)
                        firstListItemIndex = listItemIndex;
                    // display list has "looped back" to the beginning (too many items to display), break out
                    else if (listItemIndex == firstListItemIndex)
                        break;

                    listings.Add(listItem);
                }

                // sort such that first list item at beginning
                listings.Sort(
                    (item1, item2) =>
                    (item1.AtkComponentListItemRenderer->ListItemIndex > item2.AtkComponentListItemRenderer->ListItemIndex) ? 1 : -1
                    );

                var nqNeededRem = nqNeeded;
                var hqNeededRem = hqNeeded;
                foreach (var listItem in listings)
                {
                    var itemQualityImageNode = listItem.AtkComponentListItemRenderer->GetImageNodeById(AddonItemQualityImageNodeId);
                    if (itemQualityImageNode == null)
                        continue;

                    var itemIsHq = itemQualityImageNode->IsVisible();

                    var listingNode = (AtkResNode*)listItem.AtkComponentListItemRenderer->OwnerNode;

                    //if (listItem.AtkComponentListItemRenderer->OwnerNode->Y < 0)
                    //{
                    //    setNodeNeededMark(listingNode, false, true, true);
                    //    continue;
                    //}


                    if (nqNeededRem > 0 && !itemIsHq) // needed nq
                    {
                        // mark the node
                        setNodeNeededMark(listingNode, true, true, true);

                        // subtract the node's listing quantity from the remaining needed quantity
                        var listingQuantityNode = (AtkTextNode*)listItem.AtkComponentListItemRenderer->GetTextNodeById(AddonItemQuantityNodeId);
                        var listingQuantity = int.TryParse(listingQuantityNode->GetText().AsDalamudSeString().TextValue, out var parseResult)
                            ? parseResult
                            : 0;

                        nqNeededRem -= listingQuantity;
                    }
                    else if (hqNeededRem > 0 && itemIsHq) // needed hq
                    {
                        // mark the node
                        setNodeNeededMark(listingNode, true, true, true);

                        // subtract the node's listing quantity from the remaining needed quantity
                        var listingQuantityNode = (AtkTextNode*)listingNode->GetComponent()->GetTextNodeById(AddonItemQuantityNodeId);
                        var listingQuantity = int.TryParse(listingQuantityNode->GetText().AsDalamudSeString().TextValue, out var parseResult)
                            ? parseResult
                            : 0;
                        hqNeededRem -= listingQuantity;
                    }
                    else // unneeded
                        setNodeNeededMark(listingNode, false, true, true);
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, $"{GetType().Name}: Failed to update PostDraw");
            }
        }

        private unsafe void updateNeedQualities()
        {
            try
            {
                nqNeeded = 0;
                hqNeeded = 0;
                var infoProxy = InfoProxyItemSearch.Instance();

                if (infoProxy == null) return;

                var nqItemId = infoProxy->SearchItemId;
                var hqItemId = Plugin.ItemData.ConvertItemIdToHq(nqItemId);

                nqNeeded = Gearset.GetItemRequirements(nqItemId, Plugin.ItemRequirements).Count;
                hqNeeded = hqItemId != nqItemId
                    ? Gearset.GetItemRequirements(hqItemId, Plugin.ItemRequirements).Count
                    : nqNeeded;
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Failed to update needed items");
            }
        }

        protected override unsafe NodeBase? initializeCustomNode(AtkResNode* parentNodePtr, AtkUnitBase* addon)
        {
            NineGridNode? customNode = null;
            try
            {
                var parentNode = (AtkComponentNode*)parentNodePtr;

                var hoverNode = parentNode
                    ->GetComponent()
                    ->UldManager
                    .SearchNodeById(AddonHoverHighlightNodeId)
                    ->GetAsAtkNineGridNode();

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
                Services.NativeController.AttachToAddon(customNode, addon, addon->RootNode, NodePosition.AsLastChild);

                return customNode;
            }
            catch (Exception ex)
            {
                customNode?.Dispose();
                Services.Log.Error(ex, "Failed to initialize custom node");
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

            Services.NativeController.DetachFromAddon(node, (AtkUnitBase*)addon);
        }
    }
}

