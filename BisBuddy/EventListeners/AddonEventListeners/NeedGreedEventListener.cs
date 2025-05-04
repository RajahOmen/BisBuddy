using BisBuddy.Gear;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
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

        protected override float CustomNodeMaxY => float.MaxValue;

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
            var addon = (AddonNeedGreed*)args.Addon;
            try
            {
                if (addon == null || !addon->IsVisible) return;

                var itemIndexesToHighlight = new Dictionary<int, HighlightColor>();
                for (var itemIdx = 0; itemIdx < addon->NumItems; itemIdx++)
                {
                    var lootItem = addon->Items[itemIdx];
                    var itemColor = Gearset.GetRequirementColor(
                        lootItem.ItemId,
                        Plugin.Configuration.DefaultHighlightColor,
                        Plugin.ItemRequirements
                        );

                    if (itemColor is not null)
                        itemIndexesToHighlight.Add(itemIdx, itemColor);
                }

                highlightItems(itemIndexesToHighlight, addon);
            }
            catch (Exception ex)
            {
                Services.Log.Warning(ex, "Error in handleNeedGreedAddonEvent");
            }
        }

        private unsafe void highlightItems(Dictionary<int, HighlightColor> indexColors, AddonNeedGreed* needGreed)
        {
            var itemListComponent = (AtkComponentList*)needGreed
                ->GetComponentByNodeId(AddonItemListNodeId);

            for (var i = 0; i < itemListComponent->ListLength; i++)
            {
                var itemComponent = itemListComponent->ItemRendererList[i].AtkComponentListItemRenderer;
                var itemColor = indexColors.GetValueOrDefault(i);
                setNodeNeededMark((AtkResNode*)itemComponent->OwnerNode, itemColor, true, false);
            }
        }

        protected override unsafe NodeBase? initializeCustomNode(AtkResNode* parentNodePtr, AtkUnitBase* addon, HighlightColor color)
        {
            // doesn't use custom nodes
            return null;
        }

        protected override void unlinkCustomNode(nint parentNodePtr, NodeBase node)
        {
            // doesn't use custom nodes
            return;
        }
    }
}
