namespace Tesearis.ArtifactInspectorForUnity.Core.Native
{
    /// <summary>
    /// One external reference entry, as returned by UFS_GetExternalReference.
    /// </summary>
    internal readonly struct ExternalReferenceInfo
    {
        public string Path { get; }
        public string Guid { get; }
        public ExternalReferenceType Type { get; }

        public ExternalReferenceInfo(string path, string guid, ExternalReferenceType type)
        {
            Path = path;
            Guid = guid;
            Type = type;
        }
    }
}
