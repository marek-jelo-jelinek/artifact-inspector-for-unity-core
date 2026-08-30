using System;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport
{
    /// <summary>A plain in-memory <see cref="IRandomAccessByteSource"/> for tests.</summary>
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
            var available = Math.Max(0, Math.Min(count, _data.Length - offset));
            if (available > 0)
            {
                Buffer.BlockCopy(_data, (int)offset, buffer, bufferOffset, (int)available);
            }

            return (int)available;
        }
    }
}