using BisBuddy.Gear;
using BisBuddy.Import;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BisBuddy.Services.ImportGearset
{
    public interface IImportGearsetSource
    {
        public ImportGearsetSourceType SourceType { get; }
        public Task<List<Gearset>> ImportGearsets(string importString);
    }
}
