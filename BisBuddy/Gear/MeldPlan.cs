using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Gear
{
    public readonly struct MeldPlan(Gearset gearset, Gearpiece gearpiece, List<Materia> materia)
    {
        // maximum length of gearset name for a plan
        public static readonly int MaxMeldPlanNameLength = 30;
        // string appended to unmelded materia in the meld plan
        public static readonly string UnmeldedColorblindIndicator = "*";

        public Gearset Gearset { get; init; } = gearset;
        public Gearpiece Gearpiece { get; init; } = gearpiece;
        public List<Materia> Materia { get; init; } = materia;
        public string PlanText { get; init; } = BuildPlanText(gearset, gearpiece, materia);
        public List<(string MateriaText, bool IsMelded)> MateriaInfo { get; init; } = BuildMateriaInfo(materia);

        private static string BuildPlanText(Gearset gearset, Gearpiece gearpiece, List<Materia> materia)
        {
            var jobAbbrev = gearset.JobAbbrv;
            var gearsetName = gearset.Name.Length > MaxMeldPlanNameLength
                ? gearset.Name[..(MaxMeldPlanNameLength - 2)] + ".."
                : gearset.Name;

            return $"[{jobAbbrev}] {gearsetName}";
        }

        private static List<(string MateriaText, bool IsMelded)> BuildMateriaInfo(List<Materia> materia)
        {
            return materia
                .Select(m => (
                    // MateriaText
                    $"+{m.StatQuantity} {m.StatShortName}{(m.IsMelded ? "" : UnmeldedColorblindIndicator)}",
                    // IsMelded
                    m.IsMelded
                    ))
                .ToList();
        }
    }
}
