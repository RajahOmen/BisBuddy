using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Services.Addon.Containers
{
    public class InventoryRetainerLargeService(AddonServiceDependencies<InventoryRetainerLargeService> deps)
        : ContainerService<InventoryRetainerLargeService>(deps)
    {
        public override string AddonName => "InventoryRetainerLarge";
        protected override int pagesPerView => 2;
        protected override int maxTabIndex => 3;
        protected override string[] dragDropGridAddonNames => [
            "RetainerGrid0", // 0
            "RetainerGrid1", // 0
            "RetainerGrid2", // 1
            "RetainerGrid3", // 1
            "RetainerGrid4", // 2
            ];
        protected override unsafe ItemOrderModuleSorter* sorter
            => ItemOrderModule.Instance()->RetainerSorter[ItemOrderModule.Instance()->ActiveRetainerId];

        protected override unsafe int getTabIndex()
        {
            var addon = (AddonInventoryRetainerLarge*)gameGui.GetAddonByName(AddonName);
            if (addon == null || !addon->IsVisible) return -1;
            return addon->TabIndex;
        }

        protected override unsafe List<nint> getAddons()
        {
            var addon = (AddonInventoryRetainerLarge*)gameGui.GetAddonByName(AddonName);
            if (addon == null || !addon->IsVisible)
                return [];

            var visibleAddonNames = dragDropGridAddonNames[
                (getTabIndex() * pagesPerView)..Math.Min(dragDropGridAddonNames.Length, getTabIndex() * pagesPerView + pagesPerView)
                ];

            var visibleAddons = new List<nint>();
            foreach (var childAddon in addon->AddonControl.ChildAddons)
            {
                if (visibleAddonNames.Contains(childAddon.Value->AtkUnitBase->NameString))
                    visibleAddons.Add((nint)childAddon.Value->AtkUnitBase);
            }

            return visibleAddons;
        }

        protected override unsafe List<nint> getDragDropComponents(nint gridAddon)
        {
            // this also works for retainer grids
            var slots = ((AddonInventoryGrid*)gridAddon)->Slots.ToArray();

            return slots.Select(s => (nint)s.Value).Where(s => s != nint.Zero).ToList();
        }
    }
}
