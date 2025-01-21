using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.EventListeners.AddonEventListeners.Containers
{
    internal class InventoryBuddyEventListener(Plugin plugin)
        : ContainerEventListenerBase(plugin)
    {
        public override string AddonName => "InventoryBuddy";
        protected override int pagesPerView => 2;
        protected override int maxTabIndex => 1;
        protected override unsafe ItemOrderModuleSorter* sorter
        {
            get
            {
                // since buddy broken up by normal/premium, select sorter based on
                // true tab index
                var addon = (AddonInventoryBuddy*)Services.GameGui.GetAddonByName(AddonName);
                if (addon == null) return null;
                if (addon->TabIndex == 0)
                    return ItemOrderModule.Instance()->SaddleBagSorter;
                else
                    return ItemOrderModule.Instance()->PremiumSaddleBagSorter;
            }

        }

        protected override string[] dragDropGridAddonNames => [
            AddonName
            ];

        protected override unsafe int getTabIndex()
        {
            var addon = (AddonInventoryBuddy*)Services.GameGui.GetAddonByName(AddonName);
            if (addon == null || !addon->IsVisible) return -1;

            // since tabs are split into two separate sorters of 1 page each, always on tab "0"
            return 0;
        }

        protected override unsafe List<nint> getAddons()
        {
            // inventory buddy has no child addons, stores everything in InventoryBuddy
            var addon = (AddonInventoryBuddy*)Services.GameGui.GetAddonByName(AddonName);
            if (addon == null) return [];


            return [(nint)addon];
        }

        protected override unsafe List<nint> getDragDropComponents(nint gridAddon)
        {
            var addon = (AddonInventoryBuddy*)gridAddon;
            var slots = addon->Slots.ToArray();

            return slots.Select(s => (nint)s.Value).Where(s => s != nint.Zero).ToList();
        }
    }
}
