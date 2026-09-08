using System;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.Native
{
    /// <summary>
    /// Decorates an <see cref="IRandomAccessByteSource"/> with a fixed-size read-ahead buffer.
    /// <see cref="TypeTree.TypeTreeReader"/>/<see cref="TypeTree.TypeTreeOffsetWalker"/> issue many
    /// small, mostly forward reads within one object's byte range (one field/array-length/string-length
    /// prefix at a time); against a native, compression-backed source (<see cref="NativeFileByteSource"/>)
    /// each of those otherwise becomes its own out-of-order native seek, which can force the
    /// native decoder to redo work from the start of the compressed block. Coalescing them into
    /// fewer, larger reads avoids that.
    /// </summary>
    internal sealed class BufferedByteSource : IRandomAccessByteSource
    {
        private const int BufferSize = 64 * 1024;

        private readonly IRandomAccessByteSource _inner;
        private readonly byte[] _buffer = new byte[BufferSize];
        private long _bufferStart;
        private int _bufferLength;

        internal BufferedByteSource(IRandomAccessByteSource inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public long Length => _inner.Length;

        public int Read(long offset, byte[] buffer, int bufferOffset, int count)
        {
            // Larger than our own buffer: not worth staging through it, and it would need to
            // clobber the current window anyway. Read straight from the inner source.
            if (count > BufferSize) return _inner.Read(offset, buffer, bufferOffset, count);

            if (!IsBuffered(offset, count))
            {
                RefillBuffer(offset);
            }

            var available = (int)Math.Min(count, _bufferLength - (offset - _bufferStart));
            if (available <= 0) return 0;

            Buffer.BlockCopy(_buffer, (int)(offset - _bufferStart), buffer, bufferOffset, available);
            return available;
        }

        private bool IsBuffered(long offset, int count) =>
            _bufferLength > 0 && offset >= _bufferStart && offset + count <= _bufferStart + _bufferLength;

        private void RefillBuffer(long offset)
        {
            _bufferStart = offset;
            _bufferLength = _inner.Read(offset, _buffer, 0, BufferSize);
        }
    }
}
