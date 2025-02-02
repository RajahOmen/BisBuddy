using BisBuddy.Gear;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;

namespace BisBuddy.EventListeners.AddonEventListeners
{
    public class NeedGreedEventListener(Plugin plugin)
        : AddonEventListener(plugin, plugin.Configuration.HighlightNeedGreed)
    {
        // ADDON NODE IDS
        // the node id of the list of items in the addon
        public static readonly uint AddonItemListNodeId = 6;

        public override uint AddonCustomNodeId => throw new NotImplementedException();

        public override string AddonName => "NeedGreed";

        protected override void registerAddonListeners()
        {
            Services.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, AddonName, handlePostRefresh);
        }

        protected override void unregisterAddonListeners()
        {
            Services.AddonLifecycle.UnregisterListener(handlePostRefresh);
        }

        public override unsafe void handleManualUpdate()
        {
            handleUpdate((AddonNeedGreed*)Services.GameGui.GetAddonByName(AddonName));
        }

        private unsafe void handlePostRefresh(AddonEvent type, AddonArgs args)
        {
            handleUpdate((AddonNeedGreed*)args.Addon);
        }

        private unsafe void handleUpdate(AddonNeedGreed* addon)
        {
            try
            {
                if (addon == null || !addon->IsVisible) return;

                var itemIndexesToHighlight = new List<int>();
                for (var itemIdx = 0; itemIdx < addon->NumItems; itemIdx++)
                {
                    var lootItem = addon->Items[itemIdx];
                    if (Gearset.GetGearsetsNeedingItemById(lootItem.ItemId, Plugin.Gearsets).Count > 0)
                    {
                        itemIndexesToHighlight.Add(itemIdx);
                    }
                }

                highlightItems(itemIndexesToHighlight, addon);
            }
            catch (Exception ex)
            {
                Services.Log.Warning(ex, "Error in handleNeedGreedAddonEvent");
            }
        }

        private unsafe void highlightItems(List<int> itemIndexes, AddonNeedGreed* needGreed)
        {
            var itemListComponent = (AtkComponentList*)needGreed
                ->GetComponentByNodeId(AddonItemListNodeId);

            for (var i = 0; i < itemListComponent->ListLength; i++)
            {
                var itemComponent = itemListComponent->ItemRendererList[i].AtkComponentListItemRenderer;
                var itemNeeded = itemIndexes.Contains(itemComponent->ListItemIndex);
                setNodeNeededMark((AtkResNode*)itemComponent->OwnerNode, itemNeeded, true, false);
            }
        }

        protected override nint initializeCustomNode(nint parentNodePtr)
        {
            throw new NotImplementedException();
        }

        protected override unsafe void unlinkCustomNode(nint nodePtr)
        {
            throw new NotImplementedException();
        }
    }
}
