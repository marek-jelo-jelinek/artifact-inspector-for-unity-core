namespace Tesearis.ArtifactInspectorForUnity.Core.Adapters
{
    /// <summary>Where a texture's pixel data lives when it is not stored inline in the SerializedFile.</summary>
    public readonly struct StreamingInfo
    {
        public ulong Offset { get; }
        public uint Size { get; }
        public string Path { get; }

        public StreamingInfo(ulong offset, uint size, string path)
        {
            Offset = offset;
            Size = size;
            Path = path;
        }
    }
}