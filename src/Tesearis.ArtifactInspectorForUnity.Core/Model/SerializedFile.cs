using System;
using System.Collections.Generic;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Handles;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.Model
{
    /// <summary>
    /// A parsed Unity SerializedFile.
    /// </summary>
    public sealed class SerializedFile : IDisposable
    {
        private readonly SerializedFileHandle _handle;
        private readonly FileHandle _fileHandle;
        private readonly TypeTreeCache _typeTreeCache;
        private readonly IRandomAccessByteSource _byteSource;
        private readonly List<ObjectRef> _objects;
        private readonly List<ExternalReference> _externalReferences;
        private Dictionary<long, int> _objectsByPathId;
        private Dictionary<long, ObjectSnapshot> _snapshotsByPathId;
        private List<TypeTreeSummary> _typeTrees;
        private int? _version;

        private readonly object _lock = new();
        private bool _disposed;

        // Set by ArtifactArchive.OpenSerializedFile right after construction, so this instance can
        // remove itself from the archive's tracking set on normal disposal. Null for a SerializedFile
        // not obtained that way (e.g. constructed directly in tests).
        private Action<SerializedFile> _onDisposed;

        internal SerializedFile(SerializedFileHandle handle, FileHandle fileHandle, TypeTreeCache typeTreeCache, IRandomAccessByteSource byteSource = null)
        {
            _handle = handle ?? throw new ArgumentNullException(nameof(handle));
            _fileHandle = fileHandle ?? throw new ArgumentNullException(nameof(fileHandle));
            _typeTreeCache = typeTreeCache ?? throw new ArgumentNullException(nameof(typeTreeCache));
            // Buffered: TypeTreeReader/TypeTreeOffsetWalker read one field/array-length/string-length
            // prefix at a time, and each unbuffered native read against a compressed archive entry
            // can force the native decoder to redo work from the start of the block.
            _byteSource = byteSource ?? new BufferedByteSource(new NativeFileByteSource(fileHandle));

            var infos = _handle.UseHandle((api, h) => api.GetObjectInfos(h));
            _objects = new List<ObjectRef>(infos.Length);
            foreach (var info in infos)
            {
                _objects.Add(new ObjectRef(this, info.Id, info.TypeId, info.Offset, info.Size));
            }

            var externalReferenceCount = _handle.UseHandle((api, h) => api.GetExternalReferenceCount(h));
            _externalReferences = new List<ExternalReference>(externalReferenceCount);
            for (var i = 0; i < externalReferenceCount; i++)
            {
                var refIndex = i;
                var info = _handle.UseHandle((api, h) => api.GetExternalReference(h, refIndex));
                _externalReferences.Add(new ExternalReference(info.Path, info.Guid, (ExternalReferenceType)info.Type));
            }
        }

        /// <summary>Registers a callback invoked once, right after this instance disposes itself normally.</summary>
        internal void SetOwner(Action<SerializedFile> onDisposed)
        {
            _onDisposed = onDisposed;
        }

        /// <summary>The SerializedFile format version, as reported by <c>UFS_GetSerializedFileVersion</c>.</summary>
        /// <exception cref="NativeFeatureNotSupportedException">The loaded native library doesn't export UFS_GetSerializedFileVersion.</exception>
        public int Version => Guarded(() =>
        {
            _version ??= _handle.UseHandle((api, h) => api.GetSerializedFileVersion(h));
            return _version.Value;
        });

        public IReadOnlyList<ObjectRef> Objects => Guarded<IReadOnlyList<ObjectRef>>(() => _objects);

        public IReadOnlyList<ExternalReference> ExternalReferences => Guarded<IReadOnlyList<ExternalReference>>(() => _externalReferences);

        /// <summary>
        /// Every distinct type-tree entry this file declares without needing to already hold a live
        /// object of that type. Pass an entry's <see cref="TypeTreeSummary.Index"/> to
        /// <see cref="GetTypeTreeByIndex"/> to walk its full tree.
        /// </summary>
        /// <exception cref="NativeFeatureNotSupportedException">
        /// The loaded native library doesn't export UFS_GetTypeTreeCount/UFS_GetTypeTreeInfo.
        /// </exception>
        public IReadOnlyList<TypeTreeSummary> TypeTrees => Guarded<IReadOnlyList<TypeTreeSummary>>(() =>
        {
            _typeTrees ??= _handle.UseHandle((api, h) =>
            {
                var count = api.GetTypeTreeCount(h);
                var typeTrees = new List<TypeTreeSummary>(count);
                for (var i = 0; i < count; i++)
                {
                    var info = api.GetTypeTreeInfo(h, i);
                    typeTrees.Add(new TypeTreeSummary(i, info));
                }

                return typeTrees;
            });

            return _typeTrees;
        });

        public bool TryGetObject(long pathId, out ObjectRef objectRef)
        {
            lock (_lock)
            {
                ThrowIfDisposed();

                if (_objectsByPathId == null)
                {
                    _objectsByPathId = new Dictionary<long, int>(_objects.Count);
                    for (var i = 0; i < _objects.Count; i++)
                    {
                        _objectsByPathId[_objects[i].PathId] = i;
                    }
                }

                if (_objectsByPathId.TryGetValue(pathId, out var index))
                {
                    objectRef = _objects[index];
                    return true;
                }
            }

            objectRef = default;
            return false;
        }

        internal string GetTypeName(long pathId, int typeId)
        {
            return Guarded(() => _typeTreeCache.GetOrBuild(_handle, pathId, typeId).TypeName);
        }

        internal TypeTreeReader CreateReader(long pathId, int typeId, long byteOffset)
        {
            return Guarded(() =>
            {
                var root = _typeTreeCache.GetOrBuild(_handle, pathId, typeId);
                return new TypeTreeReader(root, _byteSource, byteOffset);
            });
        }

        /// <summary>
        /// Looks up (or builds and caches) a fully-decoded snapshot of one object's fields, keyed by PathId.
        /// A cache hit costs zero further byte-source reads -- this is what makes repeated lookups of the
        /// same object (e.g. several sibling components resolving their owning GameObject's name) and
        /// PPtr-chasing (see <see cref="PPtr.TryResolveSnapshot"/>) cheap, instead of re-walking the type
        /// tree from scratch every time (see <see cref="ObjectRef.GetReader"/>'s doc comment for why that
        /// used to happen).
        ///
        /// The first call for a given PathId decides eager-vs-deferred per field using whichever
        /// <paramref name="options"/> were passed then; a later call for an already-cached PathId returns
        /// the existing snapshot and ignores any different options passed here.
        /// </summary>
        public bool TryGetSnapshot(long pathId, out ObjectSnapshot snapshot, MaterializeOptions options = null)
        {
            lock (_lock)
            {
                ThrowIfDisposed();

                if (_snapshotsByPathId != null && _snapshotsByPathId.TryGetValue(pathId, out var cached))
                {
                    snapshot = cached;
                    return true;
                }

                if (!TryGetObject(pathId, out var objectRef))
                {
                    snapshot = default;
                    return false;
                }

                snapshot = BuildAndCacheSnapshot(objectRef, options ?? MaterializeOptions.Default);
                return true;
            }
        }

        /// <summary>Same as <see cref="TryGetSnapshot"/>, for a caller that already holds the <see cref="ObjectRef"/> and can skip the redundant PathId lookup.</summary>
        internal ObjectSnapshot GetOrBuildSnapshotForRef(ObjectRef objectRef, MaterializeOptions options)
        {
            lock (_lock)
            {
                ThrowIfDisposed();

                if (_snapshotsByPathId != null && _snapshotsByPathId.TryGetValue(objectRef.PathId, out var cached))
                {
                    return cached;
                }

                return BuildAndCacheSnapshot(objectRef, options ?? MaterializeOptions.Default);
            }
        }

        /// <summary>
        /// Eagerly snapshots every object (optionally filtered by <see cref="MaterializeOptions.TypeIdFilter"/>),
        /// in ascending <see cref="ObjectRef.ByteOffset"/> order for forward/local reads. Already-cached
        /// objects are skipped, not rebuilt; a single object's failure is recorded in the result rather than
        /// aborting the pass. Holds this file's internal lock for the whole pass, blocking other calls on
        /// this instance until it finishes.
        /// </summary>
        public MaterializeResult MaterializeAll(MaterializeOptions options = null)
        {
            options ??= MaterializeOptions.Default;

            lock (_lock)
            {
                ThrowIfDisposed();

                var ordered = new List<ObjectRef>(_objects.Count);
                for (var i = 0; i < _objects.Count; i++)
                {
                    var obj = _objects[i];
                    if (options.TypeIdFilter == null || options.TypeIdFilter(obj.TypeId))
                    {
                        ordered.Add(obj);
                    }
                }

                ordered.Sort((a, b) => a.ByteOffset.CompareTo(b.ByteOffset));

                var succeeded = 0;
                var failures = new List<(long PathId, Exception Error)>();

                foreach (var objectRef in ordered)
                {
                    if (_snapshotsByPathId != null && _snapshotsByPathId.ContainsKey(objectRef.PathId))
                    {
                        succeeded++;
                        continue;
                    }

                    try
                    {
                        BuildAndCacheSnapshot(objectRef, options);
                        succeeded++;
                    }
                    catch (ArtifactInspectorException ex)
                    {
                        failures.Add((objectRef.PathId, ex));
                    }
                }

                return new MaterializeResult(succeeded, failures);
            }
        }

        /// <summary>Builds one object's snapshot and caches it. Caller must hold <see cref="_lock"/>.</summary>
        private ObjectSnapshot BuildAndCacheSnapshot(ObjectRef objectRef, MaterializeOptions options)
        {
            var root = _typeTreeCache.GetOrBuild(_handle, objectRef.PathId, objectRef.TypeId);
            var rootField = SnapshotBuilder.Build(root, objectRef.ByteOffset, _byteSource, options);
            var snapshot = new ObjectSnapshot(objectRef.PathId, objectRef.TypeId, objectRef.ByteOffset, objectRef.ByteSize, rootField);

            _snapshotsByPathId ??= new Dictionary<long, ObjectSnapshot>();
            _snapshotsByPathId[objectRef.PathId] = snapshot;
            return snapshot;
        }

        /// <summary>The walked type tree for one <see cref="TypeTrees"/> entry, by its <see cref="TypeTreeSummary.Index"/>.</summary>
        /// <exception cref="NativeFeatureNotSupportedException">The loaded native library doesn't export UFS_GetTypeTreeByIndex.</exception>
        public TypeTreeNode GetTypeTreeByIndex(int index)
        {
            return Guarded(() => _typeTreeCache.GetOrBuildByIndex(_handle, index));
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

        public void Dispose()
        {
            if (!DisposeHandles()) return;

            // Invoked outside the lock: this notifies the owning ArtifactArchive (if any) to
            // remove this instance from its tracking set, which takes the archive's own lock.
            _onDisposed?.Invoke(this);
        }

        internal void Invalidate()
        {
            DisposeHandles();
        }

        /// <summary>Marks this instance disposed and releases its native handles, once. Returns whether this call was the one that did so.</summary>
        private bool DisposeHandles()
        {
            lock (_lock)
            {
                if (_disposed) return false;

                _disposed = true;
                _fileHandle.Dispose();
                _handle.Dispose();
                return true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SerializedFile));
        }
    }
}