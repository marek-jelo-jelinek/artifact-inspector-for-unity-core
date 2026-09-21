using Tesearis.ArtifactInspectorForUnity.Core.Model;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.Adapters
{
    /// <summary>Where a texture's pixel data lives when it is not stored inline in the SerializedFile.</summary>
    public readonly struct StreamingInfo
    {
        public long Offset { get; }
        public long Size { get; }
        public string Path { get; }

        public StreamingInfo(long offset, long size, string path)
        {
            Offset = offset;
            Size = size;
            Path = path;
        }

        /// <summary>Reads exactly <see cref="Size"/> bytes at <see cref="Offset"/> from <paramref name="archive"/>.</summary>
        public byte[] ReadBytes(ArtifactArchive archive)
        {
            return archive.ReadRawEntry(Path, Offset, checked((int)Size));
        }

        /// <summary>
        /// Opens the shared byte source for the whole entry named by <see cref="Path"/> -- not scoped to
        /// <see cref="Offset"/>/<see cref="Size"/>; since <see cref="IRandomAccessByteSource"/> has no Seek,
        /// read <see cref="Size"/> bytes starting at <see cref="Offset"/> via the returned source's Read.
        /// </summary>
        public IRandomAccessByteSource OpenByteSource(ArtifactArchive archive)
        {
            return archive.OpenRawByteSource(Path);
        }
    }
}
