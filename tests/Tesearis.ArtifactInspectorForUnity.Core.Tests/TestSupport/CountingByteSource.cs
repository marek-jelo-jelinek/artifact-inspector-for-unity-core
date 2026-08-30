using System;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport
{
    /// <summary>
    /// Wraps another <see cref="IRandomAccessByteSource"/> and counts
    /// <see cref="Read"/> calls, to assert that <c>TypeTreeReader</c>'s per-instance
    /// memoization actually avoids redundant reads on repeated field access.
    /// </summary>
    internal sealed class CountingByteSource : IRandomAccessByteSource
    {
        private readonly IRandomAccessByteSource _inner;

        internal CountingByteSource(IRandomAccessByteSource inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        internal int ReadCallCount { get; private set; }

        public long Length => _inner.Length;

        public int Read(long offset, byte[] buffer, int bufferOffset, int count)
        {
            ReadCallCount++;
            return _inner.Read(offset, buffer, bufferOffset, count);
        }
    }
}
