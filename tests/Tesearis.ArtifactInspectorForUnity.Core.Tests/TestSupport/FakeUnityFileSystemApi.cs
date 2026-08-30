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
        public IntPtr OpenFile(string virtualPath) => OpenFileOverride != null ? OpenFileOverride(virtualPath) : NextHandle();
        public long ReadFile(IntPtr fileHandle, byte[] buffer, long size) => 0;
        public long SeekFile(IntPtr fileHandle, long offset, SeekOrigin origin) => offset;
        public long GetFileSize(IntPtr fileHandle) => 0;
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
        public IntPtr GetTypeTree(IntPtr serializedFileHandle, long objectId) => NextHandle();

        public TypeTreeNodeInfo GetTypeTreeNodeInfo(IntPtr typeTreeHandle, int nodeIndex) =>
            new TypeTreeNodeInfo("int", "value", 0, 4, TypeTreeFlags.None, TypeTreeMetaFlags.None, firstChildNode: 0, nextNode: 0);

        public int GetTypeTreeCount(IntPtr serializedFileHandle)
        {
            GetTypeTreeCountCallCount++;
            return TypeTreeInfos.Count;
        }
        public TypeTreeInfo GetTypeTreeInfo(IntPtr serializedFileHandle, int index) => TypeTreeInfos[index];
        public IntPtr GetTypeTreeByIndex(IntPtr serializedFileHandle, int index) => NextHandle();
    }
}
