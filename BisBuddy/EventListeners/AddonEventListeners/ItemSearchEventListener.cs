using BisBuddy.Gear;
using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using System;
using System.Collections.Generic;

namespace BisBuddy.EventListeners.AddonEventListeners
{
    // marketboard
    public class ItemSearchEventListener(Plugin plugin)
        : AddonEventListener(plugin, plugin.Configuration.HighlightMarketboard)
    {
        public override string AddonName => "ItemSearch";

        // ADDON NODE IDS
        // id of the hover highlight node
        public static readonly uint AddonHoverHighlightNodeId = 15;

        // what items are needed from the marketboard listings
        private readonly HashSet<int> neededItemIndexes = [];

        protected override float CustomNodeMaxY => 500f;

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
                updateNeededItems();
                if (neededItemIndexes.Count == 0)
                { // no items needed from list, return
                    unmarkNodes();
                    return;
                };
                var addon = (AddonItemSearch*)Services.GameGui.GetAddonByName(AddonName);
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

                    var itemNeeded = neededItemIndexes.Contains(listItemIndex);
                    setNodeNeededMark((AtkResNode*)listItem.AtkComponentListItemRenderer->OwnerNode, itemNeeded, true, true);
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, $"{GetType().Name}: Failed to update PreDraw");
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
                        Gearset.GearsetsNeedItemId(nqItemId, Plugin.Gearsets) // either the NQ is needed
                        || Gearset.GearsetsNeedItemId(hqItemId, Plugin.Gearsets) // or HQ is needed
                        );
                    if (nqOrHqNeeded) // needed at some level
                    {
                        var itemName = Plugin.ItemData.GetItemNameById(nqItemId);
                        neededItemIndexes.Add(i);
                    }
                }
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
