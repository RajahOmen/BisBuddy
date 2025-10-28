using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Services.Addon.Containers
{
    public class InventoryRetainerService(AddonServiceDependencies<InventoryRetainerService> deps)
        : ContainerService<InventoryRetainerService>(deps)
    {
        public override string AddonName => "InventoryRetainer";
        protected override int pagesPerView => 1;
        protected override int maxTabIndex => 4;
        protected override string[] dragDropGridAddonNames => [
            "RetainerGrid",
            ];
        protected override unsafe ItemOrderModuleSorter* sorter =>
            ItemOrderModule.Instance()->RetainerSorter[ItemOrderModule.Instance()->ActiveRetainerId];

        protected override unsafe int getTabIndex()
        {
            var addon = (AddonInventoryRetainer*)AddonPtr.Address;
            if (addon == null || !addon->IsVisible) return -1;
            return addon->TabIndex;
        }

        protected override unsafe List<nint> getAddons()
        {
            var addon = (AddonInventoryRetainer*)AddonPtr.Address;
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
            // this also works for retainer grids
            var slots = ((AddonInventoryGrid*)gridAddon)->Slots.ToArray();

            return slots.Select(s => (nint)s.Value).Where(s => s != nint.Zero).ToList();
        }
    }
}
