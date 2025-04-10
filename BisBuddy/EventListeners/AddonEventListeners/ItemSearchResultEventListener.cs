using BisBuddy.Gear;
using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using System;

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
        // id of the list of items being displayed (and the scrollbar)
        public static readonly uint AddonItemListNodeId = 26;
        // id of the scrollbar in the list of items
        public static readonly uint AddonItemScrollbarNodeId = 5;
        // id of the scroll button on the bar
        public static readonly uint AddonItemScrollButtonNodeId = 2;


        // if nq or hq is needed for the selected item
        private bool nqNeeded = false;
        private bool hqNeeded = false;
        // the previous Y value of the scrollbar
        private float previousScrollbarY = -1.0f;

        protected override void registerAddonListeners()
        {
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, AddonName, handleRequestedUpdate);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, AddonName, handlePostRefresh);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, AddonName, handlePreDraw);
        }
        protected override void unregisterAddonListeners()
        {
            Services.AddonLifecycle.UnregisterListener(handleRequestedUpdate);
            Services.AddonLifecycle.UnregisterListener(handlePreDraw);
        }

        public override void handleManualUpdate()
        {
            updateNeedQualities();
        }

        private unsafe void handleRequestedUpdate(AddonEvent type, AddonArgs args)
        {
            updateNeedQualities();
        }

        private unsafe void handlePostRefresh(AddonEvent type, AddonArgs args)
        {
            updateNeedQualities();
        }

        private void handlePreDraw(AddonEvent type, AddonArgs args)
        {
            handleUpdate();
        }

        private unsafe void handleUpdate()
        {
            try
            {
                if (!hqNeeded && !nqNeeded)
                { // no variant needed for this selection, unmark and quit
                    unmarkAllNodes();
                    return;
                };
                var addon = (AddonItemSearchResult*)Services.GameGui.GetAddonByName(AddonName);
                if (addon == null || !addon->IsVisible) return; // addon not visible/rendered

                if (addon->Results == null || addon->Results->ListLength == 0) return; // no items for sale

                var listingList = addon->Results;

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

                //var nqMarked = false;
                //var hqMarked = false;
                var firstListItemIndex = 0;

                AtkComponentListItemRenderer* nqItemToMark = null;
                AtkComponentListItemRenderer* hqItemToMark = null;

                for (var i = 0; i < listingList->ListLength; i++)
                {
                    var listItem = listingList->ItemRendererList[i];
                    var listItemIndex = listItem.AtkComponentListItemRenderer->ListItemIndex;

                    if (i == 0) firstListItemIndex = listItemIndex;
                    else if (listItemIndex == firstListItemIndex)
                    {
                        // display list has "looped back" to the beginning (too many items to display), break out
                        break;
                    }

                    var itemQualityImageNode = (AtkTextNode*)listItem.AtkComponentListItemRenderer->GetImageNodeById(AddonItemQualityImageNodeId);
                    if (itemQualityImageNode == null) continue;

                    var itemIsHq = itemQualityImageNode->IsVisible();


                    if (nqNeeded && !itemIsHq && (nqItemToMark == null || listItemIndex < nqItemToMark->ListItemIndex)) // needed hq
                    {
                        // unmark old candidate
                        if (nqItemToMark != null)
                        {
                            setNodeNeededMark((AtkResNode*)nqItemToMark->OwnerNode, false, true, true);
                        }
                        // set as new candidate
                        nqItemToMark = listItem.AtkComponentListItemRenderer;

                    }
                    else if (hqNeeded && itemIsHq && (hqItemToMark == null || listItemIndex < hqItemToMark->ListItemIndex)) // needed nq
                    {
                        // unmark old candidate
                        if (hqItemToMark != null)
                        {
                            setNodeNeededMark((AtkResNode*)hqItemToMark->OwnerNode, false, true, true);
                        }
                        // set as new candidate
                        hqItemToMark = listItem.AtkComponentListItemRenderer;

                    }
                    else // unneeded
                    {
                        setNodeNeededMark((AtkResNode*)listItem.AtkComponentListItemRenderer->OwnerNode, false, true, true);
                    }
                }

                // mark final candidate items
                if (nqItemToMark != null)
                {
                    setNodeNeededMark((AtkResNode*)nqItemToMark->OwnerNode, true, true, true);
                }
                if (hqItemToMark != null)
                {
                    setNodeNeededMark((AtkResNode*)hqItemToMark->OwnerNode, true, true, true);
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
                nqNeeded = false;
                hqNeeded = false;
                var infoProxy = InfoProxyItemSearch.Instance();

                if (infoProxy == null) return;

                var nqItemId = infoProxy->SearchItemId;
                var hqItemId = Plugin.ItemData.ConvertItemIdToHq(nqItemId);

                nqNeeded = Gearset.GetGearsetsNeedingItemById(nqItemId, Plugin.Gearsets).Count > 0;
                hqNeeded = hqItemId != nqItemId && Gearset.GetGearsetsNeedingItemById(hqItemId, Plugin.Gearsets).Count > 0;

                // ensure this update refreshes the draws by resetting caches
                previousScrollbarY = -1.0f;
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
                Services.NativeController.AttachToComponent(customNode, addon, parentNode->Component, (AtkResNode*)hoverNode, NodePosition.BeforeTarget);

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

            Services.NativeController.DetachFromComponent(node, (AtkUnitBase*)addon, ((AtkComponentNode*)parentNodePtr)->Component);
        }
    }
}

