using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Services.Addon.Containers
{
    public class InventoryLargeService(AddonServiceDependencies<InventoryLargeService> deps)
        : ContainerService<InventoryLargeService>(deps)
    {
        public override string AddonName => "InventoryLarge";
        protected override int pagesPerView => 2;
        protected override int maxTabIndex => 1;
        protected override string[] dragDropGridAddonNames => [
            "InventoryGrid0",
            "InventoryGrid1",
            "InventoryGrid2",
            ];
        protected override unsafe ItemOrderModuleSorter* sorter
            => ItemOrderModule.Instance()->InventorySorter;

        protected override unsafe int getTabIndex()
        {
            var addon = (AddonInventoryLarge*)gameGui.GetAddonByName(AddonName);
            if (addon == null || !addon->IsVisible) return -1;
            return addon->TabIndex;
        }

        protected override unsafe List<nint> getAddons()
        {
            var addon = (AddonInventoryLarge*)gameGui.GetAddonByName(AddonName);
            if (addon == null)
                return [];

            var addons = new List<nint>();

            foreach (var childAddon in addon->AddonControl.ChildAddons)
            {
                addons.Add((nint)childAddon.Value->AtkUnitBase);
            }

            return addons;
        }

        protected override unsafe List<nint> getDragDropComponents(nint gridAddon)
        {
            var slots = ((AddonInventoryGrid*)gridAddon)->Slots.ToArray();

            return slots.Select(s => (nint)s.Value).Where(n => n != nint.Zero).ToList();
        }
    }
}
