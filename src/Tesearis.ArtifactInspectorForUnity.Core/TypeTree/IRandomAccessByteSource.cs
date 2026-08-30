namespace Tesearis.ArtifactInspectorForUnity.Core.TypeTree
{
    public interface IRandomAccessByteSource
    {
        long Length { get; }

        int Read(long offset, byte[] buffer, int bufferOffset, int count);
    }
}