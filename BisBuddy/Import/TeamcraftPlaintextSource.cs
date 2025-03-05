using BisBuddy.Gear;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Import
{
    public class TeamcraftPlaintextSource : ImportSource
    {
        public ImportSourceType SourceType => throw new NotImplementedException();

        public Task<List<Gearset>> ImportGearsets(string importString)
        {
            throw new NotImplementedException();
        }
    }
}
