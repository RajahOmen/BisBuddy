using System;

namespace BisBuddy.Gear
{
    public class GearsetImportException : Exception
    {
        public GearsetImportStatusType FailStatusType { get; init; }
        public GearsetImportException(GearsetImportStatusType failStatusType)
        {
            FailStatusType = failStatusType;
        }
    }
}
