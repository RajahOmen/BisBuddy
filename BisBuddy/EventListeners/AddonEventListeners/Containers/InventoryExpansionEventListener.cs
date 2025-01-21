using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.EventListeners.AddonEventListeners.Containers
{
    internal class InventoryExpansionEventListener(Plugin plugin)
        : ContainerEventListenerBase(plugin)
    {
        public override string AddonName => "InventoryExpansion";
        protected override int pagesPerView => 4;
        protected override int maxTabIndex => 0;
        protected override string[] dragDropGridAddonNames => [
            "InventoryGrid0E",
            "InventoryGrid1E",
            "InventoryGrid2E",
            "InventoryGrid3E",
            ];
        protected override unsafe ItemOrderModuleSorter* sorter
            => ItemOrderModule.Instance()->InventorySorter;

        protected override unsafe int getTabIndex()
        {
            var addon = (AddonInventoryExpansion*)Services.GameGui.GetAddonByName(AddonName);
            if (addon == null || !addon->IsVisible) return -1;
            return addon->TabIndex;
        }

        protected override unsafe List<nint> getAddons()
        {
            var addon = (AddonInventoryExpansion*)Services.GameGui.GetAddonByName(AddonName);
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

            return slots.Select(s => (nint)s.Value).Where(s => s != nint.Zero).ToList();
        }
    }
}
