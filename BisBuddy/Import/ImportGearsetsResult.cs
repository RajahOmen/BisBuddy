using BisBuddy.Gear;
using System.Collections.Generic;

namespace BisBuddy.Import
{
    public struct ImportGearsetsResult
    {
        public GearsetImportStatusType StatusType;
        public List<Gearset>? Gearsets;
    }
}
