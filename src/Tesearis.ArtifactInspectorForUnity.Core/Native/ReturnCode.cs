namespace Tesearis.ArtifactInspectorForUnity.Core.Native
{
    /// <summary>Result codes returned by the native UnityFileSystemApi library.</summary>
    public enum ReturnCode
    {
        Success = 0,
        AlreadyInitialized = 1,
        NotInitialized = 2,
        FileNotFound = 3,
        FileFormatError = 4,
        InvalidArgument = 5,
        HigherSerializedFileVersion = 6,
        DestinationBufferTooSmall = 7,
        InvalidObjectId = 8,
        UnknownError = 9,
        FileError = 10,
        ErrorCreatingArchiveFile = 11,
        ErrorAddingFileToArchive = 12,
        TypeNotFound = 13,
    }

    internal static class ReturnCodeExtensions
    {
        internal static void ThrowIfNotSuccess(this ReturnCode code, string operationDescription)
        {
            if (code != ReturnCode.Success) throw NativeCallException.FromReturnCode(code, operationDescription);
        }
    }
}