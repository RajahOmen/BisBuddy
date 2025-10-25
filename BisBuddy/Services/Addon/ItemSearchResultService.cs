using BisBuddy.Gear;
using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using KamiToolKit.System;
using System;
using System.Collections.Generic;
using System.Linq;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkComponentList;

namespace BisBuddy.Services.Addon
{
    // marketboard
    public class ItemSearchResultService(AddonServiceDependencies<ItemSearchResultService> deps)
        : AddonService<ItemSearchResultService>(deps)
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
        private HighlightColor? nqColor = null;
        private HighlightColor? hqColor = null;

        protected override float CustomNodeMaxY => 240f;

        protected override void registerAddonListeners()
        {
            addonLifecycle.RegisterListener(AddonEvent.PreDraw, AddonName, handlePreDraw);
        }
        protected override void unregisterAddonListeners()
        {
            addonLifecycle.UnregisterListener(handlePreDraw);
        }
        protected override void updateListeningStatus(bool effectsAssignments)
            => setListeningStatus(configurationService.HighlightMarketboard);

        private unsafe void handlePreDraw(AddonEvent type, AddonArgs args)
        {
            debugService.AssertMainThreadDebug();

            try
            {
                updateNeedQualities();
                if (hqNeeded == 0 && nqNeeded == 0)
                { // no variant needed for this selection, unmark and quit
                    unmarkNodes();
                    return;
                };
                var addon = (AddonItemSearchResult*)gameGui.GetAddonByName(AddonName).Address;

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
                    item1.AtkComponentListItemRenderer->ListItemIndex > item2.AtkComponentListItemRenderer->ListItemIndex ? 1 : -1
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

                    if (nqNeededRem > 0 && !itemIsHq) // needed nq
                    {
                        // mark the node
                        setNodeNeededMark(listingNode, nqColor, true, true);

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
                        setNodeNeededMark(listingNode, hqColor, true, true);

                        // subtract the node's listing quantity from the remaining needed quantity
                        var listingQuantityNode = (AtkTextNode*)listingNode->GetComponent()->GetTextNodeById(AddonItemQuantityNodeId);
                        var listingQuantity = int.TryParse(listingQuantityNode->GetText().AsDalamudSeString().TextValue, out var parseResult)
                            ? parseResult
                            : 0;
                        hqNeededRem -= listingQuantity;
                    }
                    else // unneeded
                        setNodeNeededMark(listingNode, null, true, true);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to update PostDraw");
            }
        }

        private unsafe void updateNeedQualities()
        {
            debugService.AssertMainThreadDebug();

            try
            {
                nqNeeded = 0;
                hqNeeded = 0;
                var infoProxy = InfoProxyItemSearch.Instance();

                if (infoProxy == null) return;

                var nqItemId = infoProxy->SearchItemId;
                var hqItemId = itemDataService.ConvertItemIdToHq(nqItemId);

                var nqItemRequirements = gearsetsService.GetItemRequirements(nqItemId).ToList();
                var hqItemRequirements = hqItemId != nqItemId
                    ? gearsetsService.GetItemRequirements(hqItemId).ToList()
                    : nqItemRequirements;

                nqNeeded = nqItemRequirements.Count;
                hqNeeded = nqItemRequirements.Count;

                nqColor = gearsetsService.GetRequirementColor(nqItemRequirements);
                hqColor = hqItemId != nqItemId
                    ? gearsetsService.GetRequirementColor(hqItemRequirements)
                    : nqColor;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to update needed items");
            }
        }

        protected override unsafe NodeBase initializeCustomNode(AtkResNode* parentNodePtr, AtkUnitBase* addon, HighlightColor color)
        {
            debugService.AssertMainThreadDebug();

            NineGridNode? customNode = null;
            try
            {
                var parentNode = (AtkComponentNode*)parentNodePtr;

                var hoverNode = parentNode
                    ->GetComponent()
                    ->UldManager
                    .SearchNodeById(AddonHoverHighlightNodeId)
                    ->GetAsAtkNineGridNode();

                customNode = UiHelper.CloneHighlightNineGridNode(
                    hoverNode,
                    color.CustomNodeColor,
                    color.CustomNodeAlpha(configurationService.BrightListItemHighlighting)
                    ) ?? throw new InvalidOperationException($"Could not clone node \"{hoverNode->NodeId}\"");

                return customNode;
            }
            catch
            {
                customNode?.Dispose();
                throw;
            }
        }
    }
}

