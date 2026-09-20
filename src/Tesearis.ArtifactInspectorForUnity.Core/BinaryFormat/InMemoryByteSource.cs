using System;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat
{
    /// <summary>
    /// An in-memory <see cref="IRandomAccessByteSource"/> backed by a single contiguous byte array.
    /// Used for whole-entry in-memory buffering to eliminate native seek and P/Invoke overhead.
    /// </summary>
    internal sealed class InMemoryByteSource : IRandomAccessByteSource
    {
        private readonly byte[] _data;

        internal InMemoryByteSource(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public long Length => _data.Length;

        public int Read(long offset, byte[] buffer, int bufferOffset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (bufferOffset < 0) throw new ArgumentOutOfRangeException(nameof(bufferOffset), bufferOffset, "Buffer offset must not be negative.");
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "Count must not be negative.");
            if (buffer.Length - bufferOffset < count) throw new ArgumentException("Buffer is too small for the requested count.", nameof(buffer));
            if (offset < 0 || offset >= _data.Length || count == 0) return 0;

            var available = (int)Math.Min(count, _data.Length - offset);
            Buffer.BlockCopy(_data, (int)offset, buffer, bufferOffset, available);
            return available;
        }
    }
}
