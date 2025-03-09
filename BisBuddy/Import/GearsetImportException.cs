using System;

namespace BisBuddy.Import
{
    public class GearsetImportException : Exception
    {
        public GearsetImportStatusType FailStatusType { get; init; }
        public GearsetImportException(GearsetImportStatusType failStatusType, string? message = null) : base(message)
        {
            FailStatusType = failStatusType;
        }
    }
}
