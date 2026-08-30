namespace Tesearis.ArtifactInspectorForUnity.Core.Model
{
    /// <summary>
    /// One readable entry in a mounted archive.
    /// </summary>
    public readonly struct ArchiveEntryInfo
    {
        public string Path { get; }
        public long Size { get; }
        public bool IsSerializedFile { get; }

        internal ArchiveEntryInfo(string path, long size, bool isSerializedFile)
        {
            Path = path;
            Size = size;
            IsSerializedFile = isSerializedFile;
        }
    }
}
