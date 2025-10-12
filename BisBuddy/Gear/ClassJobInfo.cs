using BisBuddy.Util;

namespace BisBuddy.Gear
{
    public class ClassJobInfo(
        uint classJobId,
        string name,
        string abbreviation,
        int? iconIdIndex = null
        )
    {
        public readonly uint ClassJobId = classJobId;
        public readonly string Name = name;
        public readonly string Abbreviation = abbreviation;
        public readonly int IconIdFramed = Constants.ClassJobIconIdOffsetFramed
            + (iconIdIndex ?? (int)classJobId);
        public readonly int IconId = Constants.ClassJobIconIdOffset
            + (iconIdIndex ?? (int)classJobId);
    }
}
