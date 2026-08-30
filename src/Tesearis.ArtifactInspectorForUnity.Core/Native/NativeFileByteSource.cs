using System;
using System.Buffers;
using System.IO;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Handles;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.Native
{
    internal sealed class NativeFileByteSource : IRandomAccessByteSource
    {
        private readonly FileHandle _fileHandle;
        private readonly object _lock = new();

        internal NativeFileByteSource(FileHandle fileHandle)
        {
            _fileHandle = fileHandle ?? throw new ArgumentNullException(nameof(fileHandle));
        }

        public long Length
        {
            get
            {
                lock (_lock)
                {
                    return _fileHandle.UseHandle((api, h) => api.GetFileSize(h));
                }
            }
        }

        public int Read(long offset, byte[] buffer, int bufferOffset, int count)
        {
            lock (_lock)
            {
                // Seek + read happen inside one UseHandle scope so a concurrent Dispose() can't
                // free the underlying file handle between the two native calls.
                return _fileHandle.UseHandle((api, h) =>
                {
                    api.SeekFile(h, offset, SeekOrigin.Begin);

                    if (bufferOffset == 0) return (int)api.ReadFile(h, buffer, count);

                    var scratch = ArrayPool<byte>.Shared.Rent(count);
                    try
                    {
                        var readCount = api.ReadFile(h, scratch, count);
                        Buffer.BlockCopy(scratch, 0, buffer, bufferOffset, (int)readCount);
                        return (int)readCount;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(scratch);
                    }
                });
            }
        }
    }
}