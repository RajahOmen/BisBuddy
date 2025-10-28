using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Services.Addon.Containers
{
    public class InventoryBuddyService(AddonServiceDependencies<InventoryBuddyService> deps)
        : ContainerService<InventoryBuddyService>(deps)
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
                if (!AddonPtr.IsReady)
                    return null;
                if (((AddonInventoryBuddy*)AddonPtr.Address)->TabIndex == 0)
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
            if (AddonPtr.IsReady && AddonPtr.IsVisible)
                return 0;

            return -1;
        }

        protected override unsafe List<nint> getAddons()
        {
            // inventory buddy has no child addons, stores everything in InventoryBuddy
            if (AddonPtr.IsReady && AddonPtr.IsVisible)
                return [AddonPtr.Address];

            return [];
        }

        protected override unsafe List<nint> getDragDropComponents(nint gridAddon)
        {
            var addon = (AddonInventoryBuddy*)gridAddon;
            var slots = addon->Slots.ToArray();

            return slots
                .Select(s => (nint)s.Value)
                .Where(s => s != nint.Zero)
                .ToList();
        }
    }
}
