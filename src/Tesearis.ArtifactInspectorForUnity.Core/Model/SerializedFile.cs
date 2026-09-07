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
        private readonly NativeFileByteSource _byteSource;
        private readonly List<ObjectRef> _objects;
        private readonly List<ExternalReference> _externalReferences;
        private Dictionary<long, int> _objectsByPathId;
        private List<TypeTreeSummary> _typeTrees;
        private int? _version;

        private readonly object _lock = new();
        private bool _disposed;

        // Set by ArtifactArchive.OpenSerializedFile right after construction, so this instance can
        // remove itself from the archive's tracking set on normal disposal. Null for a SerializedFile
        // not obtained that way (e.g. constructed directly in tests).
        private Action<SerializedFile> _onDisposed;

        internal SerializedFile(SerializedFileHandle handle, FileHandle fileHandle, TypeTreeCache typeTreeCache)
        {
            _handle = handle ?? throw new ArgumentNullException(nameof(handle));
            _fileHandle = fileHandle ?? throw new ArgumentNullException(nameof(fileHandle));
            _typeTreeCache = typeTreeCache ?? throw new ArgumentNullException(nameof(typeTreeCache));
            _byteSource = new NativeFileByteSource(fileHandle);

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

        internal TypeTreeReader CreateReader(long pathId, long byteOffset)
        {
            return Guarded(() =>
            {
                var root = _typeTreeCache.GetOrBuild(_handle, pathId);
                return new TypeTreeReader(root, _byteSource, byteOffset);
            });
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