using System;
using System.IO;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat
{
    /// <summary>
    /// A plain <see cref="IRandomAccessByteSource"/> over a local file, backing
    /// <see cref="SerializedFileDetector"/>'s and <see cref="YamlSerializedFileDetector"/>'s
    /// <c>string filePath</c> convenience overloads for a bare on-disk file with no archive or
    /// native library involved.
    /// </summary>
    internal sealed class FileStreamByteSource : IRandomAccessByteSource, IDisposable
    {
        private readonly FileStream _stream;
        private readonly object _lock = new();
        private bool _disposed;

        internal FileStreamByteSource(string filePath)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            _stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public long Length
        {
            get
            {
                lock (_lock)
                {
                    ThrowIfDisposed();
                    return _stream.Length;
                }
            }
        }

        public int Read(long offset, byte[] buffer, int bufferOffset, int count)
        {
            lock (_lock)
            {
                ThrowIfDisposed();

                _stream.Seek(offset, SeekOrigin.Begin);

                var totalRead = 0;
                while (totalRead < count)
                {
                    var read = _stream.Read(buffer, bufferOffset + totalRead, count - totalRead);
                    if (read == 0) break;
                    totalRead += read;
                }

                return totalRead;
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;

                _disposed = true;
                _stream.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FileStreamByteSource));
        }
    }
}
