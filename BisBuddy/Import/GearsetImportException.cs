using System;

namespace BisBuddy.Import
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
