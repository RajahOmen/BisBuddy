using BisBuddy.Gear;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Import
{
    public interface ImportSource
    {
        public ImportSourceType SourceType { get; }
        public Task<List<Gearset>> ImportGearsets(string importString);
    }
}
