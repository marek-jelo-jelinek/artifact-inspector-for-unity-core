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

        /// <summary>
        /// The bare "archive:/" root, with no per-archive mount segment. At least one real
        /// UnityFileSystemApi build (confirmed on Unity 6000.3.13f1's macOS Editor) mounts an archive
        /// and lists its nodes fine via the handle-based calls, but silently ignores the caller-supplied
        /// mount-point identifier passed to <c>UFS_MountArchive</c> for path *resolution*: every
        /// "archive:" virtual path is resolved through one flat, un-namespaced root instead, so a
        /// <see cref="_mountPoint"/>-qualified path (e.g. "archive:/&lt;guid&gt;/CAB-xxx") 404s there,
        /// while the bare form ("archive:/CAB-xxx") opens fine.
        /// </summary>
        private const string BareArchiveRoot = "archive:/";

        /// <summary>
        /// Which virtual-path form this archive's native library actually honors for
        /// <c>UFS_OpenFile</c>/<c>UFS_OpenSerializedFile</c> -- see <see cref="BareArchiveRoot"/>.
        /// Determined once, in the constructor, via <see cref="ProbeVirtualPathScheme"/>: <c>false</c>
        /// means the normal <see cref="_mountPoint"/>-qualified form works (and is used, so a native
        /// library that *does* respect per-archive mount points -- letting multiple concurrently-mounted
        /// archives disambiguate same-named entries -- keeps using that); <c>true</c> means only the bare
        /// form resolved during the probe. Left <c>null</c> (defaulting to the normal form) when the probe
        /// itself couldn't run (an archive with no entries at all) or was inconclusive -- deliberately not
        /// re-probed per call, since repeatedly issuing a native open we already know will fail is exactly
        /// the pattern observed to eventually crash the native library's test build.
        /// </summary>
        private bool? _usesBareArchiveRoot;

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

                ProbeVirtualPathScheme();
            }
            catch
            {
                // Construction failed, so dispose here before rethrowing.
                _archive.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Determines and caches <see cref="_usesBareArchiveRoot"/> with a single, disposable
        /// <c>UFS_OpenFile</c> call against this archive's first entry -- deliberately <c>OpenFile</c>
        /// rather than <c>OpenSerializedFile</c>, since the latter can genuinely fail for a stripped
        /// (no-TypeTree) entry regardless of which virtual-path form is correct, which would make that a
        /// useless, misleading signal here. Left <c>null</c> if this archive has no entries to probe with,
        /// or if neither form opens (a genuinely unreadable archive) -- either way, callers fall back to
        /// the normal mount-scoped form and any real error surfaces from the real call instead.
        /// </summary>
        private void ProbeVirtualPathScheme()
        {
            if (_entries.Count == 0) return;

            var probeEntry = _entries[0].Path;
            try
            {
                _api.CloseFile(_api.OpenFile(_mountPoint + probeEntry));
                _usesBareArchiveRoot = false;
            }
            catch (NativeCallException)
            {
                try
                {
                    _api.CloseFile(_api.OpenFile(BareArchiveRoot + probeEntry));
                    _usesBareArchiveRoot = true;
                }
                catch (NativeCallException)
                {
                    // Neither form opens this entry -- leave undetermined; real calls below will
                    // surface whatever the underlying issue actually is.
                }
            }
        }

        /// <summary>The virtual path to open <paramref name="entryName"/> at, per <see cref="_usesBareArchiveRoot"/>.</summary>
        private string ResolveVirtualPath(string entryName)
        {
            return (_usesBareArchiveRoot == true ? BareArchiveRoot : _mountPoint) + entryName;
        }

        /// <summary>
        /// Names of the SerializedFile entries in this archive.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This archive has been disposed.</exception>
        public IReadOnlyList<string> EntryNames => Guarded<IReadOnlyList<string>>(() => _entryNames);

        /// <summary>
        /// Every readable entry in this archive (directories and deleted entries excluded).
        /// Unlike <see cref="EntryNames"/>, this includes non-SerializedFile entries too, e.g. a
        /// texture's out-of-line ".resS" payload.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This archive has been disposed.</exception>
        public IReadOnlyList<ArchiveEntryInfo> Entries => Guarded<IReadOnlyList<ArchiveEntryInfo>>(() => _entries);

        /// <summary>Opens one SerializedFile entry from this archive by name.</summary>
        /// <exception cref="ObjectDisposedException">This archive has been disposed.</exception>
        /// <exception cref="SerializedFileOpenException">
        /// The native open failed and the entry's bytes positively confirm it has no TypeTrees (e.g. a stripped Player build).
        /// </exception>
        /// <exception cref="NativeCallException">The native open failed for any other reason.</exception>
        public SerializedFile OpenSerializedFile(string entryName)
        {
            if (entryName == null) throw new ArgumentNullException(nameof(entryName));

            return Guarded(() =>
            {
                var virtualPath = ResolveVirtualPath(entryName);
                var serializedFile = SerializedFileOpener.Open(
                    _api, virtualPath, entryName, () => IsPositivelyMissingTypeTrees(entryName));
                serializedFile.SetOwner(RemoveSerializedFile);
                _openSerializedFiles.Add(serializedFile);
                return serializedFile;
            });
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

            return Guarded(() =>
            {
                var byteSource = OpenRawByteSource(entryName);
                var buffer = new byte[size];
                var read = byteSource.Read(offset, buffer, 0, size);
                return read != size ? throw new ArtifactInspectorException($"Unexpected end of data while from '{entryName}'.") : buffer;
            });
        }

        /// <summary>
        /// Reads an archive entry's entire contents.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This archive has been disposed.</exception>
        /// <exception cref="ArtifactInspectorException">The entry is larger than <see cref="int.MaxValue"/> bytes.</exception>
        public byte[] ReadRawEntry(string entryName)
        {
            if (entryName == null) throw new ArgumentNullException(nameof(entryName));

            return Guarded(() =>
            {
                var length = OpenRawByteSource(entryName).Length;
                return length > int.MaxValue
                    ? throw new ArtifactInspectorException("Entry '" + entryName + "' is too large to read.")
                    : ReadRawEntry(entryName, 0, (int)length);
            });
        }

        /// <summary>
        /// Opens a raw, schema-agnostic byte source for an archive entry.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This archive has been disposed.</exception>
        public IRandomAccessByteSource OpenRawByteSource(string entryName)
        {
            if (entryName == null) throw new ArgumentNullException(nameof(entryName));

            return Guarded<IRandomAccessByteSource>(() =>
            {
                var fileHandle = GetOrOpenFileHandle(entryName);
                return new NativeFileByteSource(fileHandle);
            });
        }

        /// <summary>
        /// Best-effort check for <see cref="OpenSerializedFile"/>'s failure triage.
        /// </summary>
        private bool IsPositivelyMissingTypeTrees(string entryName)
        {
            return SerializedFileOpener.SafeInvoke(() => SerializedFileDetector.IsMissingTypeTrees(OpenRawByteSource(entryName)));
        }

        /// <summary>Returns the cached FileHandle for entryName, opening and caching one if there isn't one.</summary>
        private FileHandle GetOrOpenFileHandle(string entryName)
        {
            if (_openFilesByEntryName.TryGetValue(entryName, out var existing)) return existing;

            var rawFileHandle = _api.OpenFile(ResolveVirtualPath(entryName));
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

        /// <summary>Runs body under this instance's lock, after checking it hasn't been disposed.</summary>
        private T Guarded<T>(Func<T> body)
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                return body();
            }
        }
    }
}