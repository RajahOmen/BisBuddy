using BisBuddy.Gear;
using BisBuddy.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;

namespace BisBuddy.EventListeners.AddonEventListeners
{
    internal class NeedGreedEventListener(Plugin plugin)
        : AddonEventListenerBase(plugin, plugin.Configuration.HighlightNeedGreed)
    {
        // ADDON NODE IDS
        // the node id of the list of items in the addon
        internal static readonly uint AddonItemListNodeId = 6;
        // the offset of the item list node in the addon
        internal static readonly int AddonItemListNodeOffset = 1;
        // the int node type of a ListItemRenderer Component Node
        internal static readonly int AddonListItemRendererType = 1008;

        public override uint AddonCustomNodeId => throw new NotImplementedException();

        public override string AddonName => "NeedGreed";

        protected override void registerAddonListeners()
        {
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, AddonName, handleEvents);
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, AddonName, handleEvents);
        }

        protected override void unregisterAddonListeners()
        {
            Services.AddonLifecycle.UnregisterListener(handleEvents);
        }

        public override void handleManualUpdate()
        {
            handleUpdate();
        }

        protected override unsafe void unlinkCustomNode(nint nodePtr)
        {
            // no nodes to unlink
            return;
        }

        private void handleEvents(AddonEvent type, AddonArgs args)
        {
            handleUpdate();
        }

        private unsafe void handleUpdate()
        {
            var needGreed = (AddonNeedGreed*)Services.GameGui.GetAddonByName(AddonName);
            if (needGreed == null || !needGreed->IsVisible) return;

            var itemIndexesToHighlight = new List<int>();
            try
            {
                for (var itemIdx = 0; itemIdx < needGreed->NumItems; itemIdx++)
                {
                    var lootItem = needGreed->Items[itemIdx];
                    var itemName = SeString.Parse(lootItem.ItemName).TextValue;
                    var gearsetsNeedingItem = Gearset.GetGearsetsNeedingItemById(lootItem.ItemId, Plugin.Gearsets);
                    if (gearsetsNeedingItem.Count > 0)
                    {
                        itemIndexesToHighlight.Add(itemIdx);
                    }
                }

                highlightItems(itemIndexesToHighlight, needGreed);
            }
            catch (Exception ex)
            {
                Services.Log.Warning(ex, "Error in handleNeedGreedAddonEvent");
            }
        }

        private unsafe void highlightItems(List<int> itemIndexes, AddonNeedGreed* needGreed)
        {
            // highlight the items in the addon

            var itemCount = needGreed->NumItems;
            var baseNode = new BaseNode((AtkUnitBase*)needGreed);
            var itemList = baseNode.GetComponentNode(AddonItemListNodeId).GetComponentNodes();


            for (var i = 0; i < itemCount; i++)
            {
                var itemNode = itemList[i];
                var itemPtr = itemNode.GetPointer();

                // not a ListItemRenderer Component Node, continue
                if ((int)itemPtr->Type != AddonListItemRendererType) continue;

                var itemNeeded = itemIndexes.Contains(i - AddonItemListNodeOffset);

                setNodeNeededMark((AtkResNode*)itemPtr, itemNeeded, true, false);
            }
        }

        protected override nint initializeCustomNode(nint parentNodePtr)
        {
            throw new NotImplementedException();
        }
    }
}
