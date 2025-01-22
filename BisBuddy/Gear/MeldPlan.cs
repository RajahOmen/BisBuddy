using System.Collections.Generic;

namespace BisBuddy.Gear
{
    public readonly struct MeldPlan
    {
        public Gearset Gearset { get; init; }
        public Gearpiece Gearpiece { get; init; }
        public List<Materia> Materia { get; init; }
    }
}
