using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Gear
{
    public record InventoryItem(uint itemId, List<uint> materiaIds)
    {
        public uint ItemId = itemId;
        public IReadOnlyList<uint> MateriaIds = materiaIds;
    }
}
