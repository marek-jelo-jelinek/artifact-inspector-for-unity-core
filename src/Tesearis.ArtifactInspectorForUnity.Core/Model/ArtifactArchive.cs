using System;
using System.Collections.Generic;
using Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Handles;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Interop;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.Model
{
    /// <summary>
    /// A mounted archive.
    /// </summary>
    public sealed class ArtifactArchive : IDisposable
    {
        private readonly IUnityFileSystemApi _api;
        private readonly ArchiveHandle _archive;
        private readonly string _mountPoint;
        private readonly List<string> _entryNames;
        private readonly List<ArchiveEntryInfo> _entries;
        private readonly Dictionary<string, FileHandle> _openFilesByEntryName = new();
        private readonly HashSet<SerializedFile> _openSerializedFiles = new();

        private readonly object _lock = new();
        private bool _disposed;

        internal ArtifactArchive(IUnityFileSystemApi api, ArchiveHandle archive, string mountPoint)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _archive = archive ?? throw new ArgumentNullException(nameof(archive));
            _mountPoint = mountPoint ?? throw new ArgumentNullException(nameof(mountPoint));

            try
            {
                _entryNames = new List<string>();
                _entries = new List<ArchiveEntryInfo>();
                _archive.UseHandle((api, handle) =>
                {
                    var count = api.GetArchiveNodeCount(handle);
                    for (var i = 0; i < count; i++)
                    {
                        var node = api.GetArchiveNode(handle, i);
                        if (node.IsSerializedFile)
                        {
                            _entryNames.Add(node.Path);
                        }

                        if ((node.Flags & (ArchiveNodeFlags.Directory | ArchiveNodeFlags.Deleted)) == ArchiveNodeFlags.None)
                        {
                            _entries.Add(new ArchiveEntryInfo(node.Path, node.Size, node.IsSerializedFile));
                        }
                    }
                });
            }
            catch
            {
                // Construction failed, so dispose here before rethrowing.
                _archive.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Names of the SerializedFile entries in this archive.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This archive has been disposed.</exception>
        public IReadOnlyList<string> EntryNames
        {
            get
            {
                lock (_lock)
                {
                    ThrowIfDisposed();
                    return _entryNames;
                }
            }
        }

        /// <summary>
        /// Every readable entry in this archive (directories and deleted entries excluded).
        /// Unlike <see cref="EntryNames"/>, this includes non-SerializedFile entries too, e.g. a
        /// texture's out-of-line ".resS" payload.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This archive has been disposed.</exception>
        public IReadOnlyList<ArchiveEntryInfo> Entries
        {
            get
            {
                lock (_lock)
                {
                    ThrowIfDisposed();
                    return _entries;
                }
            }
        }

        /// <summary>Opens one SerializedFile entry from this archive by name.</summary>
        /// <exception cref="ObjectDisposedException">This archive has been disposed.</exception>
        /// <exception cref="SerializedFileOpenException">
        /// The native open failed and the entry's bytes positively confirm it has no TypeTrees (e.g. a stripped Player build).
        /// </exception>
        /// <exception cref="NativeCallException">The native open failed for any other reason.</exception>
        public SerializedFile OpenSerializedFile(string entryName)
        {
            if (entryName == null) throw new ArgumentNullException(nameof(entryName));

            lock (_lock)
            {
                ThrowIfDisposed();

                var virtualPath = _mountPoint + entryName;
                SerializedFileHandle serializedFileHandle;
                try
                {
                    var rawSerializedHandle = _api.OpenSerializedFile(virtualPath);
                    serializedFileHandle = new SerializedFileHandle(_api, rawSerializedHandle);
                }
                catch (NativeCallException) when (IsPositivelyMissingTypeTrees(entryName))
                {
                    // UFS_OpenSerializedFile refuses to open files with no TypeTrees at all, and
                    // doesn't reliably report one consistent native error for that case.
                    throw new SerializedFileOpenException(entryName, missingTypeTrees: true);
                }

                FileHandle fileHandle = null;
                try
                {
                    var rawFileHandle = _api.OpenFile(virtualPath);
                    fileHandle = new FileHandle(_api, rawFileHandle);

                    var serializedFile = new SerializedFile(serializedFileHandle, fileHandle, new TypeTreeCache());
                    serializedFile.SetOwner(RemoveSerializedFile);
                    _openSerializedFiles.Add(serializedFile);
                    return serializedFile;
                }
                catch
                {
                    // Construction failed, so dispose here before rethrowing.
                    fileHandle?.Dispose();
                    serializedFileHandle.Dispose();
                    throw;
                }
            }
        }

        /// <summary>Removes serializedFile from tracking once it disposes itself normally.</summary>
        private void RemoveSerializedFile(SerializedFile serializedFile)
        {
            lock (_lock)
            {
                _openSerializedFiles.Remove(serializedFile);
            }
        }

        /// <summary>
        /// Reads a raw byte range from an arbitrary archive entry.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This archive has been disposed.</exception>
        /// <exception cref="ArtifactInspectorException">The entry has fewer than <paramref name="size"/> bytes remaining at <paramref name="offset"/>.</exception>
        public byte[] ReadRawEntry(string entryName, long offset, int size)
        {
            if (entryName == null) throw new ArgumentNullException(nameof(entryName));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must not be negative.");
            if (size < 0) throw new ArgumentOutOfRangeException(nameof(size), size, "Size must not be negative.");

            lock (_lock)
            {
                ThrowIfDisposed();

                var byteSource = OpenRawByteSource(entryName);
                var buffer = new byte[size];
                var read = byteSource.Read(offset, buffer, 0, size);
                if (read != size)
                {
                    throw new ArtifactInspectorException(
                        "Unexpected end of data while reading " + size + " bytes from '" + entryName +
                        "' at offset " + offset + ".");
                }

                return buffer;
            }
        }

        /// <summary>
        /// Reads an archive entry's entire contents.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This archive has been disposed.</exception>
        /// <exception cref="ArtifactInspectorException">The entry is larger than <see cref="int.MaxValue"/> bytes.</exception>
        public byte[] ReadRawEntry(string entryName)
        {
            if (entryName == null) throw new ArgumentNullException(nameof(entryName));

            lock (_lock)
            {
                ThrowIfDisposed();

                var length = OpenRawByteSource(entryName).Length;
                if (length > int.MaxValue)
                {
                    throw new ArtifactInspectorException(
                        "Entry '" + entryName + "' is " + length + " bytes, too large to read into a single byte[] (max " + int.MaxValue + ").");
                }

                return ReadRawEntry(entryName, 0, (int)length);
            }
        }

        /// <summary>
        /// Opens a raw, schema-agnostic byte source for an archive entry.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This archive has been disposed.</exception>
        public IRandomAccessByteSource OpenRawByteSource(string entryName)
        {
            if (entryName == null) throw new ArgumentNullException(nameof(entryName));

            lock (_lock)
            {
                ThrowIfDisposed();

                var fileHandle = GetOrOpenFileHandle(entryName);
                return new NativeFileByteSource(fileHandle);
            }
        }

        /// <summary>
        /// Best-effort check for <see cref="OpenSerializedFile"/>'s failure triage.
        /// </summary>
        private bool IsPositivelyMissingTypeTrees(string entryName)
        {
            try
            {
                var byteSource = OpenRawByteSource(entryName);
                return SerializedFileDetector.IsMissingTypeTrees(byteSource);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Returns the cached FileHandle for entryName, opening and caching one if there isn't one.</summary>
        private FileHandle GetOrOpenFileHandle(string entryName)
        {
            if (_openFilesByEntryName.TryGetValue(entryName, out var existing)) return existing;

            var rawFileHandle = _api.OpenFile(_mountPoint + entryName);
            var fileHandle = new FileHandle(_api, rawFileHandle);
            _openFilesByEntryName[entryName] = fileHandle;
            return fileHandle;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;

                _disposed = true;
                foreach (var fileHandle in _openFilesByEntryName.Values)
                {
                    fileHandle.Dispose();
                }

                _openFilesByEntryName.Clear();
                
                foreach (var serializedFile in _openSerializedFiles)
                {
                    serializedFile.Invalidate();
                }

                _openSerializedFiles.Clear();

                _archive.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ArtifactArchive));
        }
    }
}