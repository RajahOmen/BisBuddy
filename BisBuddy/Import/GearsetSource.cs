using BisBuddy.Gear;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Import
{
    public interface GearsetSource
    {
        public GearsetSourceType SourceType { get; }
        public bool IsSource(string candidateImportString);
        public List<Gearset> Import(string importString);
    }
}
