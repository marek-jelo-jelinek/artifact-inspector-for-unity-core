using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport
{
    /// <summary>
    /// Wraps another <see cref="IRandomAccessByteSource"/> but reports a caller-supplied <see cref="Length"/>
    /// instead of the inner source's real one. Lets a test simulate a declared/nominal length that's larger than
    /// the data actually backing it, isolating the "fewer bytes were actually read than requested" failure path
    /// from a declared-length bounds check that would otherwise short-circuit first.
    /// </summary>
    internal sealed class LengthOverrideByteSource : IRandomAccessByteSource
    {
        private readonly IRandomAccessByteSource _inner;

        internal LengthOverrideByteSource(IRandomAccessByteSource inner, long length)
        {
            _inner = inner;
            Length = length;
        }

        public long Length { get; }

        public int Read(long offset, byte[] buffer, int bufferOffset, int count) => _inner.Read(offset, buffer, bufferOffset, count);
    }
}
