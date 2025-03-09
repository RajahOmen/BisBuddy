using BisBuddy.Gear;
using System.Collections.Generic;

namespace BisBuddy.ItemAssignment
{
    public class Assignment
    {
        // null if gearpieces unassigned
        public uint? ItemId { get; set; }
        public List<Materia> MateriaList { get; set; } = [];
        public List<Gearpiece> Gearpieces { get; set; } = [];
    }
}
