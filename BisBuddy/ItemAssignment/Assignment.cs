using BisBuddy.Gear;
using BisBuddy.Gear.Melds;
using System.Collections.Generic;

namespace BisBuddy.ItemAssignment
{
    public class Assignment
    {
        // null if gearpieces unassigned
        public uint? ItemId { get; set; }
        public List<Materia> ItemMateria { get; set; } = [];
        public List<Gearpiece> Gearpieces { get; set; } = [];
    }
}
