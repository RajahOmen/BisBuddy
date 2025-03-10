using BisBuddy.Gear;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BisBuddy.Import
{
    public interface ImportSource
    {
        public ImportSourceType SourceType { get; }
        public Task<List<Gearset>> ImportGearsets(string importString);
    }
}
