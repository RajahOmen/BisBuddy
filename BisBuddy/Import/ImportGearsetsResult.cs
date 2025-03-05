using BisBuddy.Gear;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Import
{
    public struct ImportGearsetsResult
    {
        public GearsetImportStatusType StatusType;
        public List<Gearset>? Gearsets;
    }
}
