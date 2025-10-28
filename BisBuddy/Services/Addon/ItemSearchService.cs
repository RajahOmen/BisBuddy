using BisBuddy.Gear;
using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using KamiToolKit.System;
using System;
using System.Collections.Generic;

namespace BisBuddy.Services.Addon
{
    // marketboard
    public class ItemSearchService(AddonServiceDependencies<ItemSearchResultService> deps)
        : AddonService<ItemSearchResultService>(deps)
    {
        public override string AddonName => "ItemSearch";

        // ADDON NODE IDS
        // id of the hover highlight node
        public static readonly uint AddonHoverHighlightNodeId = 15;

        // what items are needed from the marketboard listings
        private readonly Dictionary<int, HighlightColor> neededItemColors = [];

        protected override float CustomNodeMaxY => 500f;

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
                updateNeededItems();
                if (neededItemColors.Count == 0)
                { // no items needed from list, return
                    unmarkNodes();
                    return;
                };
                var addon = (AddonItemSearch*)AddonPtr.Address;
                if (addon == null || !addon->IsVisible) return; // addon not visible/rendered
                if (addon->ResultsList == null || addon->ResultsList->ListLength == 0) return; // no items in search

                var itemList = addon->ResultsList;

                var firstListItemIndex = -1;
                for (var i = 0; i < itemList->ListLength; i++)
                {
                    var listItem = itemList->ItemRendererList[i];
                    var listItemIndex = listItem.AtkComponentListItemRenderer->ListItemIndex;
                    if (i == 0) firstListItemIndex = listItemIndex;
                    // display list has "looped back" to the beginning (too many items to display), break out
                    else if (listItemIndex == firstListItemIndex) break;

                    var itemColor = neededItemColors.GetValueOrDefault(listItemIndex);
                    setNodeNeededMark((AtkResNode*)listItem.AtkComponentListItemRenderer->OwnerNode, itemColor, true, true);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to update PreDraw");
            }
        }

        private unsafe void updateNeededItems()
        {
            try
            {
                neededItemColors.Clear();
                var agent = AgentItemSearch.Instance();

                if (agent == null) return;

                for (var i = 0; i < agent->ListingPageItemCount; i++)
                {
                    var nqItemId = agent->ListingPageItems[i].ItemId;
                    var hqItemId = itemDataService.ConvertItemIdToHq(nqItemId);

                    var nqItemColor = gearsetsService.GetRequirementColor(nqItemId);
                    var hqItemColor = gearsetsService.GetRequirementColor(nqItemId);
                    HighlightColor? itemColor = null;

                    // set color to set this item as based on the requirements of the nq and hq versions of the item
                    if (nqItemColor is null && hqItemColor is null) // not needed
                        itemColor = null;
                    if (nqItemColor is not null && hqItemColor is not null) // both needed
                        // set to nq item color if nq and hq is the same, else use tiebreak color
                        itemColor = nqItemColor.Equals(hqItemColor) ? nqItemColor : configurationService.DefaultHighlightColor;
                    else if (nqItemColor is not null) // nq only needed
                        itemColor = nqItemColor;
                    else // hq only needed
                        itemColor = hqItemColor;

                    if (itemColor is not null)
                        neededItemColors.Add(i, itemColor);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to update needed items");
            }
        }

        protected override unsafe NodeBase initializeCustomNode(AtkResNode* parentNodePtr, AtkUnitBase* addon, HighlightColor color)
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
