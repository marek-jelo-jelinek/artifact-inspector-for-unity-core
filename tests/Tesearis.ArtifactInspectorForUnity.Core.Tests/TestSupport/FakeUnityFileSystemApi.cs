using System;
using System.Collections.Generic;
using System.IO;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Interop;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport
{
    /// <summary>
    /// In-memory <see cref="IUnityFileSystemApi"/> double -- lets Model/ tests exercise
    /// ArtifactArchive/SerializedFile's own logic (disposal, tracking, error paths) without a real
    /// native UnityFileSystemApi library present. Every returned "handle" is just a distinct
    /// incrementing IntPtr with no real native resource behind it; Close*/Unmount* calls are
    /// recorded rather than acted on, so tests can assert exactly what was (or wasn't) released.
    /// </summary>
    internal sealed class FakeUnityFileSystemApi : IUnityFileSystemApi
    {
        private int _nextHandle = 1;

        public List<ArchiveNode> ArchiveNodes { get; } = new();
        public List<ObjectInfo> Objects { get; } = new();
        public List<ExternalReferenceInfo> ExternalReferences { get; } = new();
        public List<TypeTreeInfo> TypeTreeInfos { get; } = new();
        public int SerializedFileVersion { get; set; } = 1;

        /// <summary>When set, called instead of returning a fresh handle -- e.g. to simulate a failure.</summary>
        public Func<string, IntPtr> OpenFileOverride { get; set; }

        /// <summary>Like <see cref="OpenFileOverride"/>, for <see cref="OpenSerializedFile"/>.</summary>
        public Func<string, IntPtr> OpenSerializedFileOverride { get; set; }

        public List<IntPtr> ClosedFileHandles { get; } = new();
        public List<IntPtr> ClosedSerializedFileHandles { get; } = new();
        public List<IntPtr> UnmountedArchiveHandles { get; } = new();

        internal IntPtr NextHandle() => new IntPtr(_nextHandle++);

        public int GetSerializedFileVersionCallCount { get; private set; }
        public int GetTypeTreeCountCallCount { get; private set; }

        public int GetDllVersion() => 0;
        public string GetUnityVersion() => string.Empty;

        public int GetSerializedFileVersion(IntPtr serializedFileHandle)
        {
            GetSerializedFileVersionCallCount++;
            return SerializedFileVersion;
        }
        public void Init() { }
        public void Cleanup() { }
        public IntPtr MountArchive(string archivePath, string mountPoint) => NextHandle();
        public void UnmountArchive(IntPtr archiveHandle) => UnmountedArchiveHandles.Add(archiveHandle);
        public int GetArchiveNodeCount(IntPtr archiveHandle) => ArchiveNodes.Count;
        public ArchiveNode GetArchiveNode(IntPtr archiveHandle, int index) => ArchiveNodes[index];
        public int OpenFileCallCount { get; private set; }
        public IntPtr OpenFile(string virtualPath)
        {
            OpenFileCallCount++;
            return OpenFileOverride != null ? OpenFileOverride(virtualPath) : NextHandle();
        }

        /// <summary>Backing bytes for <see cref="ReadFile"/>/<see cref="GetFileSize"/>. Empty by default,
        /// matching every other test's assumption of a contentless fake file; set it when a test needs
        /// <c>SerializedFile</c>/<c>TypeTreeReader</c> to read real field values through this fake.</summary>
        public byte[] Content { get; set; } = Array.Empty<byte>();

        public int ReadFileCallCount { get; private set; }

        /// <summary>Every offset passed to <see cref="SeekFile"/>, in call order -- lets a test observe the
        /// actual read order (e.g. to confirm a bulk pass visits objects sorted by ByteOffset rather than in
        /// native/insertion order) without needing to intercept the real <c>IRandomAccessByteSource</c> chain,
        /// which <c>SerializedFile</c> builds internally.</summary>
        public List<long> SeekOffsets { get; } = new();

        private long _filePosition;

        public long ReadFile(IntPtr fileHandle, byte[] buffer, long size)
        {
            ReadFileCallCount++;
            var available = Math.Max(0, Content.Length - _filePosition);
            var toCopy = (int)Math.Min(size, available);
            if (toCopy > 0) Buffer.BlockCopy(Content, (int)_filePosition, buffer, 0, toCopy);
            _filePosition += toCopy;
            return toCopy;
        }

        public long SeekFile(IntPtr fileHandle, long offset, SeekOrigin origin)
        {
            SeekOffsets.Add(offset);
            _filePosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _filePosition + offset,
                SeekOrigin.End => Content.Length + offset,
                _ => offset
            };
            return _filePosition;
        }

        public long GetFileSize(IntPtr fileHandle) => Content.Length;
        public void CloseFile(IntPtr fileHandle) => ClosedFileHandles.Add(fileHandle);

        public IntPtr OpenSerializedFile(string virtualPath) =>
            OpenSerializedFileOverride != null ? OpenSerializedFileOverride(virtualPath) : NextHandle();

        public void CloseSerializedFile(IntPtr serializedFileHandle) => ClosedSerializedFileHandles.Add(serializedFileHandle);
        public void AddTypeTreeSourceFromFile(string path) { }
        public void RemoveTypeTreeSource(string path) { }
        public int GetExternalReferenceCount(IntPtr serializedFileHandle) => ExternalReferences.Count;
        public ExternalReferenceInfo GetExternalReference(IntPtr serializedFileHandle, int index) => ExternalReferences[index];
        public int GetObjectCount(IntPtr serializedFileHandle) => Objects.Count;
        public ObjectInfo[] GetObjectInfos(IntPtr serializedFileHandle) => Objects.ToArray();
        public int GetTypeTreeCallCount { get; private set; }
        public int GetTypeTreeNodeInfoCallCount { get; private set; }

        /// <summary>Correlates a fake typeTreeHandle (as returned by GetTypeTree) back to which objectId
        /// requested it, so a test can make GetTypeTreeNodeInfo answer differently per object -- e.g. to
        /// prove distinct-schema MonoBehaviour instances aren't collided by a TypeId-keyed cache.</summary>
        public Dictionary<IntPtr, long> TypeTreeHandleToObjectId { get; } = new();

        public IntPtr GetTypeTree(IntPtr serializedFileHandle, long objectId)
        {
            GetTypeTreeCallCount++;
            var handle = NextHandle();
            TypeTreeHandleToObjectId[handle] = objectId;
            return handle;
        }

        /// <summary>When set, called instead of the fixed single-leaf-node default -- e.g. to simulate a deeply nested type tree.</summary>
        public Func<int, TypeTreeNodeInfo> GetTypeTreeNodeInfoOverride { get; set; }

        /// <summary>Like <see cref="GetTypeTreeNodeInfoOverride"/>, but also given the typeTreeHandle so a
        /// test can look up (via <see cref="TypeTreeHandleToObjectId"/>) which object a walk belongs to and
        /// answer per-object. Checked first; falls back to <see cref="GetTypeTreeNodeInfoOverride"/> when null.</summary>
        public Func<IntPtr, int, TypeTreeNodeInfo> GetTypeTreeNodeInfoByHandleOverride { get; set; }

        public TypeTreeNodeInfo GetTypeTreeNodeInfo(IntPtr typeTreeHandle, int nodeIndex)
        {
            GetTypeTreeNodeInfoCallCount++;
            if (GetTypeTreeNodeInfoByHandleOverride != null) return GetTypeTreeNodeInfoByHandleOverride(typeTreeHandle, nodeIndex);
            return GetTypeTreeNodeInfoOverride != null
                ? GetTypeTreeNodeInfoOverride(nodeIndex)
                : new TypeTreeNodeInfo("int", "value", 0, 4, TypeTreeFlags.None, TypeTreeMetaFlags.None, firstChildNode: 0, nextNode: 0);
        }

        public int GetTypeTreeCount(IntPtr serializedFileHandle)
        {
            GetTypeTreeCountCallCount++;
            return TypeTreeInfos.Count;
        }
        public TypeTreeInfo GetTypeTreeInfo(IntPtr serializedFileHandle, int index) => TypeTreeInfos[index];
        public IntPtr GetTypeTreeByIndex(IntPtr serializedFileHandle, int index) => NextHandle();
    }
}
