using System;

namespace BisBuddy.Gear
{
    public class GearsetImportException : Exception
    {
        public GearsetImportStatusType FailStatusType { get; init; }
        internal GearsetImportException(GearsetImportStatusType failStatusType)
        {
            FailStatusType = failStatusType;
        }
    }
}
