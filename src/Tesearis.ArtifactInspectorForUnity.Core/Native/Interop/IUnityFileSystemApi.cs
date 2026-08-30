using System;
using System.IO;

namespace Tesearis.ArtifactInspectorForUnity.Core.Native.Interop
{
    /// <summary>
    /// The native call surface <see cref="UnityFileSystemApi"/> exposes to the rest of this library,
    /// factored out as an interface so handle types and the Model/TypeTree layers don't depend on the
    /// concrete P/Invoke boundary directly. This is what makes it possible to unit-test
    /// <c>ArtifactArchive</c>/<c>SerializedFile</c> against a fake implementation, without a real
    /// native <c>UnityFileSystemApi</c> library present.
    /// </summary>
    internal interface IUnityFileSystemApi
    {
        int GetDllVersion();
        string GetUnityVersion();
        int GetSerializedFileVersion(IntPtr serializedFileHandle);
        void Init();
        void Cleanup();
        IntPtr MountArchive(string archivePath, string mountPoint);
        void UnmountArchive(IntPtr archiveHandle);
        int GetArchiveNodeCount(IntPtr archiveHandle);
        ArchiveNode GetArchiveNode(IntPtr archiveHandle, int index);
        IntPtr OpenFile(string virtualPath);
        long ReadFile(IntPtr fileHandle, byte[] buffer, long size);
        long SeekFile(IntPtr fileHandle, long offset, SeekOrigin origin);
        long GetFileSize(IntPtr fileHandle);
        void CloseFile(IntPtr fileHandle);
        IntPtr OpenSerializedFile(string virtualPath);
        void CloseSerializedFile(IntPtr serializedFileHandle);
        void AddTypeTreeSourceFromFile(string path);
        void RemoveTypeTreeSource(string path);
        int GetExternalReferenceCount(IntPtr serializedFileHandle);
        ExternalReferenceInfo GetExternalReference(IntPtr serializedFileHandle, int index);
        int GetObjectCount(IntPtr serializedFileHandle);
        ObjectInfo[] GetObjectInfos(IntPtr serializedFileHandle);
        IntPtr GetTypeTree(IntPtr serializedFileHandle, long objectId);
        TypeTreeNodeInfo GetTypeTreeNodeInfo(IntPtr typeTreeHandle, int nodeIndex);
        int GetTypeTreeCount(IntPtr serializedFileHandle);
        TypeTreeInfo GetTypeTreeInfo(IntPtr serializedFileHandle, int index);
        IntPtr GetTypeTreeByIndex(IntPtr serializedFileHandle, int index);
    }
}
