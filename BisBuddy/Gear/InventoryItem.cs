using System.Collections.Generic;

namespace BisBuddy.Gear
{
    public record InventoryItem(uint itemId, List<uint> materiaIds)
    {
        public uint ItemId = itemId;
        public IReadOnlyList<uint> MateriaIds = materiaIds;
    }
}
