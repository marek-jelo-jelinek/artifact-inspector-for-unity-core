namespace Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat
{
    /// <summary>
    /// The raw fields of a SerializedFile header plus the computed metadata-section start offset.
    /// An implementation-detail carrier between <see cref="SerializedFileHeaderParser"/> and the
    /// metadata parse in <see cref="SerializedFileDetector"/>.
    /// </summary>
    internal readonly struct SerializedFileHeader
    {
        internal uint Version { get; }
        internal ulong FileSize { get; }
        internal ulong MetadataSize { get; }
        internal ulong DataOffset { get; }
        internal bool IsBigEndian { get; }
        internal int MetadataStartOffset { get; }

        internal SerializedFileHeader(uint version, ulong fileSize, ulong metadataSize,
            ulong dataOffset, bool isBigEndian, int metadataStartOffset)
        {
            Version = version;
            FileSize = fileSize;
            MetadataSize = metadataSize;
            DataOffset = dataOffset;
            IsBigEndian = isBigEndian;
            MetadataStartOffset = metadataStartOffset;
        }
    }
}
