using BisBuddy.Gear;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.System;
using System;
using System.Collections.Generic;

namespace BisBuddy.Services.Addon
{
    public class NeedGreedService(AddonServiceDependencies<NeedGreedService> deps)
        : AddonService<NeedGreedService>(deps)
    {
        // ADDON NODE IDS
        // the node id of the list of items in the addon
        public static readonly uint AddonItemListNodeId = 6;

        public override string AddonName => "NeedGreed";

        protected override void registerAddonListeners()
        {
            addonLifecycle.RegisterListener(AddonEvent.PreDraw, AddonName, handlePreDraw);
        }

        protected override void unregisterAddonListeners()
        {
            addonLifecycle.UnregisterListener(handlePreDraw);
        }

        protected override bool isEnabledFromConfig
            => configurationService.HighlightNeedGreed;

        private unsafe void handlePreDraw(AddonEvent type, AddonArgs args)
        {
            debugService.AssertMainThreadDebug();

            var addon = (AddonNeedGreed*)args.Addon.Address;
            try
            {
                if (addon == null || !addon->IsVisible) return;

                var itemIndexesToHighlight = new Dictionary<int, HighlightColor>();
                for (var itemIdx = 0; itemIdx < addon->NumItems; itemIdx++)
                {
                    var lootItem = addon->Items[itemIdx];
                    var itemColor = gearsetsService.GetRequirementColor(lootItem.ItemId);

                    if (itemColor is not null)
                        itemIndexesToHighlight.Add(itemIdx, itemColor);
                }

                highlightItems(itemIndexesToHighlight, addon);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Error in handleNeedGreedAddonEvent");
            }
        }

        private unsafe void highlightItems(Dictionary<int, HighlightColor> indexColors, AddonNeedGreed* needGreed)
        {
            var itemListComponent = (AtkComponentList*)needGreed
                ->GetComponentByNodeId(AddonItemListNodeId);

            for (var i = 0; i < itemListComponent->ListLength; i++)
            {
                var itemComponent = itemListComponent->ItemRendererList[i].AtkComponentListItemRenderer;
                var itemColor = indexColors.GetValueOrDefault(itemComponent->ListItemIndex);
                setNodeNeededMark((AtkResNode*)itemComponent->OwnerNode, itemColor, true, false);
            }
        }

        protected override unsafe NodeBase initializeCustomNode(AtkResNode* parentNodePtr, AtkUnitBase* addon, HighlightColor color)
        {
            // doesn't use custom nodes
            throw new NotSupportedException($"{GetType().Name} does not use custom nodes");
        }
    }
}
