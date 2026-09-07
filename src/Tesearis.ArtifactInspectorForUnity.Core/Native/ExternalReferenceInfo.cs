namespace Tesearis.ArtifactInspectorForUnity.Core.Native
{
    /// <summary>
    /// One external reference entry, as returned by UFS_GetExternalReference.
    /// </summary>
    internal readonly struct ExternalReferenceInfo
    {
        public string Path { get; }
        public string Guid { get; }
        public int Type { get; }

        public ExternalReferenceInfo(string path, string guid, int type)
        {
            Path = path;
            Guid = guid;
            Type = type;
        }
    }
}
