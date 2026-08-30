using System;
using System.Buffers;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Tesearis.ArtifactInspectorForUnity.Core.Native.Interop
{
    /// <summary>The P/Invoke boundary that resolves exported functions from the native UnityFileSystemApi library.</summary>
    internal sealed class UnityFileSystemApi : IUnityFileSystemApi
    {
        private const int PathBufferCapacity = 1024;
        private const int NameBufferCapacity = 512;
        private const int GuidBufferCapacity = 64;

        internal const string LibraryFileNameMac = "UnityFileSystemApi.dylib";
        internal const string LibraryFileNameWindows = "UnityFileSystemApi.dll";
        internal const string LibraryFileNameLinux = "UnityFileSystemApi.so";

        private static string _unityFileSystemApiLibraryPath;

        internal static void SetupLibraryPath(string unityFileSystemApiLibraryPath)
        {
            _unityFileSystemApiLibraryPath = unityFileSystemApiLibraryPath;
        }

        private static string Resolve()
        {
            if (!string.IsNullOrWhiteSpace(_unityFileSystemApiLibraryPath))
            {
                if (File.Exists(_unityFileSystemApiLibraryPath)) return _unityFileSystemApiLibraryPath;
                throw new FileNotFoundException("The configured Unity file-system API library does not exist.", _unityFileSystemApiLibraryPath);
            }

            var applicationPath = GetUnityEditorApplicationPath();
            string unityFileSystemApiLibraryPath;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                unityFileSystemApiLibraryPath = Path.Combine(applicationPath, "Contents", "Frameworks", LibraryFileNameMac);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var editorDirectory = Path.GetDirectoryName(applicationPath) ?? string.Empty;
                unityFileSystemApiLibraryPath = Path.Combine(editorDirectory, "Data", "Framework", LibraryFileNameWindows);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var editorDirectory = Path.GetDirectoryName(applicationPath) ?? string.Empty;
                unityFileSystemApiLibraryPath = Path.Combine(editorDirectory, "Data", "Framework", LibraryFileNameLinux);
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported operating system for Tesearis.ArtifactInspectorForUnity.Core.");
            }

            if (!File.Exists(unityFileSystemApiLibraryPath))
            {
                throw new FileNotFoundException(
                    "Could not locate the Unity file-system API library in the running " +
                    "Unity Editor installation. The Unity version may use a different " +
                    "library location.",
                    unityFileSystemApiLibraryPath);
            }


            return unityFileSystemApiLibraryPath;

            string GetUnityEditorApplicationPath()
            {
                var editorApplicationType = Type.GetType("UnityEditor.EditorApplication, UnityEditor");
                var property = editorApplicationType?.GetProperty("applicationPath", BindingFlags.Public | BindingFlags.Static);
                var path = property?.GetValue(null) as string;

                if (string.IsNullOrWhiteSpace(path))
                {
                    throw new InvalidOperationException(
                        "Setup UnityFileSystemApi.SetupLibraryPath or ensure the ArtifactInspector is running inside the Unity Editor.");
                }

                return path;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode GetDllVersionDelegate(out int version);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode GetUnityVersionDelegate(byte[] version, int versionCapacity);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode GetSerializedFileVersionDelegate(IntPtr serializedFileHandle, out int version);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode InitDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode CleanupDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode MountArchiveDelegate(byte[] archivePath, byte[] mountPoint, out IntPtr archiveHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode UnmountArchiveDelegate(IntPtr archiveHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode GetArchiveNodeCountDelegate(IntPtr archiveHandle, out int count);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode GetArchiveNodeDelegate(
            IntPtr archiveHandle,
            int index,
            byte[] path,
            int pathCapacity,
            out long size,
            out int flags);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode OpenFileDelegate(byte[] virtualPath, out IntPtr fileHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode ReadFileDelegate(IntPtr fileHandle, byte[] buffer, long size, out long readSize);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode SeekFileDelegate(IntPtr fileHandle, long offset, int whence, out long newPosition);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode GetFileSizeDelegate(IntPtr fileHandle, out long size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode CloseFileDelegate(IntPtr fileHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode OpenSerializedFileDelegate(byte[] virtualPath, out IntPtr serializedFileHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode CloseSerializedFileDelegate(IntPtr serializedFileHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode GetExternalReferenceCountDelegate(IntPtr serializedFileHandle, out int count);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode GetExternalReferenceDelegate(
            IntPtr serializedFileHandle,
            int index,
            byte[] path,
            int pathCapacity,
            byte[] guid,
            out ExternalReferenceType type);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode GetObjectCountDelegate(IntPtr serializedFileHandle, out int count);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode GetObjectInfoDelegate(IntPtr serializedFileHandle, [In, Out] ObjectInfo[] infos, int count);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode GetTypeTreeDelegate(IntPtr serializedFileHandle, long objectId, out IntPtr typeTreeHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode GetTypeTreeNodeInfoDelegate(
            IntPtr typeTreeHandle,
            int nodeIndex,
            byte[] type,
            int typeCapacity,
            byte[] name,
            int nameCapacity,
            out int offset,
            out int size,
            out TypeTreeFlags flags,
            out TypeTreeMetaFlags metaFlags,
            out int firstChildNode,
            out int nextNode);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode GetTypeTreeCountDelegate(IntPtr serializedFileHandle, out int count);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode GetTypeTreeInfoDelegate(IntPtr serializedFileHandle, int index, out TypeTreeInfo info);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode GetTypeTreeByIndexDelegate(IntPtr serializedFileHandle, int index, out IntPtr typeTreeHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode AddTypeTreeSourceFromFileDelegate(byte[] path);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ReturnCode RemoveTypeTreeSourceDelegate(byte[] path);

        private readonly GetDllVersionDelegate _getDllVersion;
        private readonly GetUnityVersionDelegate _getUnityVersion;
        private readonly GetSerializedFileVersionDelegate _getSerializedFileVersion;
        private readonly InitDelegate _init;
        private readonly CleanupDelegate _cleanup;
        private readonly MountArchiveDelegate _mountArchive;
        private readonly UnmountArchiveDelegate _unmountArchive;
        private readonly GetArchiveNodeCountDelegate _getArchiveNodeCount;
        private readonly GetArchiveNodeDelegate _getArchiveNode;
        private readonly OpenFileDelegate _openFile;
        private readonly ReadFileDelegate _readFile;
        private readonly SeekFileDelegate _seekFile;
        private readonly GetFileSizeDelegate _getFileSize;
        private readonly CloseFileDelegate _closeFile;
        private readonly OpenSerializedFileDelegate _openSerializedFile;
        private readonly CloseSerializedFileDelegate _closeSerializedFile;
        private readonly GetExternalReferenceCountDelegate _getExternalReferenceCount;
        private readonly GetExternalReferenceDelegate _getExternalReference;
        private readonly GetObjectCountDelegate _getObjectCount;
        private readonly GetObjectInfoDelegate _getObjectInfo;
        private readonly GetTypeTreeDelegate _getTypeTree;
        private readonly GetTypeTreeNodeInfoDelegate _getTypeTreeNodeInfo;

        // Present only in native libraries new enough to support external TypeTree source
        // registration (Unity 6.5+); resolved defensively via TryResolve so an older library
        // missing just these two exports doesn't fail to construct this whole class.
        private readonly AddTypeTreeSourceFromFileDelegate _addTypeTreeSourceFromFile;
        private readonly RemoveTypeTreeSourceDelegate _removeTypeTreeSource;

        // Defensive TryResolve, same rationale as above.
        private readonly GetTypeTreeCountDelegate _getTypeTreeCount;
        private readonly GetTypeTreeInfoDelegate _getTypeTreeInfo;
        private readonly GetTypeTreeByIndexDelegate _getTypeTreeByIndex;

        /// <summary>The raw OS module handle returned by the platform loader.</summary>
        internal IntPtr LibraryHandle { get; }

        internal UnityFileSystemApi()
        {
            var nativeLibraryPath = Resolve();
            var libraryHandle = NativeLibraryLoader.Load(nativeLibraryPath);

            try
            {
                _init = Resolve<InitDelegate>(libraryHandle, "UFS_Init");
                _cleanup = Resolve<CleanupDelegate>(libraryHandle, "UFS_Cleanup");
                _mountArchive = Resolve<MountArchiveDelegate>(libraryHandle, "UFS_MountArchive");
                _unmountArchive = Resolve<UnmountArchiveDelegate>(libraryHandle, "UFS_UnmountArchive");
                _getArchiveNodeCount = Resolve<GetArchiveNodeCountDelegate>(libraryHandle, "UFS_GetArchiveNodeCount");
                _getArchiveNode = Resolve<GetArchiveNodeDelegate>(libraryHandle, "UFS_GetArchiveNode");
                _openFile = Resolve<OpenFileDelegate>(libraryHandle, "UFS_OpenFile");
                _readFile = Resolve<ReadFileDelegate>(libraryHandle, "UFS_ReadFile");
                _seekFile = Resolve<SeekFileDelegate>(libraryHandle, "UFS_SeekFile");
                _getFileSize = Resolve<GetFileSizeDelegate>(libraryHandle, "UFS_GetFileSize");
                _closeFile = Resolve<CloseFileDelegate>(libraryHandle, "UFS_CloseFile");
                _openSerializedFile = Resolve<OpenSerializedFileDelegate>(libraryHandle, "UFS_OpenSerializedFile");
                _closeSerializedFile = Resolve<CloseSerializedFileDelegate>(libraryHandle, "UFS_CloseSerializedFile");
                _getExternalReferenceCount = Resolve<GetExternalReferenceCountDelegate>(libraryHandle, "UFS_GetExternalReferenceCount");
                _getExternalReference = Resolve<GetExternalReferenceDelegate>(libraryHandle, "UFS_GetExternalReference");
                _getObjectCount = Resolve<GetObjectCountDelegate>(libraryHandle, "UFS_GetObjectCount");
                _getObjectInfo = Resolve<GetObjectInfoDelegate>(libraryHandle, "UFS_GetObjectInfo");
                _getTypeTree = Resolve<GetTypeTreeDelegate>(libraryHandle, "UFS_GetTypeTree");
                _getTypeTreeNodeInfo = Resolve<GetTypeTreeNodeInfoDelegate>(libraryHandle, "UFS_GetTypeTreeNodeInfo");
                _addTypeTreeSourceFromFile = TryResolve<AddTypeTreeSourceFromFileDelegate>(libraryHandle, "UFS_AddTypeTreeSourceFromFile");
                _removeTypeTreeSource = TryResolve<RemoveTypeTreeSourceDelegate>(libraryHandle, "UFS_RemoveTypeTreeSource");
                _getTypeTreeCount = TryResolve<GetTypeTreeCountDelegate>(libraryHandle, "UFS_GetTypeTreeCount");
                _getTypeTreeInfo = TryResolve<GetTypeTreeInfoDelegate>(libraryHandle, "UFS_GetTypeTreeInfo");
                _getTypeTreeByIndex = TryResolve<GetTypeTreeByIndexDelegate>(libraryHandle, "UFS_GetTypeTreeByIndex");

                // Defensive TryResolve, same rationale as above.
                _getDllVersion = TryResolve<GetDllVersionDelegate>(libraryHandle, "UFS_GetDllVersion");
                _getUnityVersion = TryResolve<GetUnityVersionDelegate>(libraryHandle, "UFS_GetUnityVersion");
                _getSerializedFileVersion = TryResolve<GetSerializedFileVersionDelegate>(libraryHandle, "UFS_GetSerializedFileVersion");
            }
            catch
            {
                NativeLibraryLoader.Free(libraryHandle);
                throw;
            }

            // Only commit the handle once every required symbol resolved successfully, so a
            // partially-constructed instance never reports a library handle nobody owns.
            LibraryHandle = libraryHandle;
        }

        private static TDelegate Resolve<TDelegate>(IntPtr libraryHandle, string symbolName) where TDelegate : Delegate
        {
            var address = NativeLibraryLoader.GetExport(libraryHandle, symbolName);
            return (TDelegate)Marshal.GetDelegateForFunctionPointer(address, typeof(TDelegate));
        }

        /// <summary>
        /// Like <see cref="Resolve{TDelegate}"/>, but returns null instead of throwing when the symbol
        /// isn't present in the loaded library, for entry points that only exist in newer native
        /// libraries.
        /// </summary>
        private static TDelegate TryResolve<TDelegate>(IntPtr libraryHandle, string symbolName) where TDelegate : Delegate
        {
            try
            {
                return Resolve<TDelegate>(libraryHandle, symbolName);
            }
            catch (ArtifactInspectorException)
            {
                return null;
            }
        }

        private static byte[] ToNativeUtf8(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var byteCount = Encoding.UTF8.GetByteCount(value);
            var buffer = new byte[byteCount + 1];
            Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);
            return buffer;
        }

        /// <summary>Decodes a NUL-terminated UTF-8 buffer into a string.</summary>
        /// <param name="buffer">The native-filled buffer.</param>
        /// <param name="length">The number of bytes to decode.</param>
        private static string FromNativeUtf8(byte[] buffer, int length)
        {
            var terminator = Array.IndexOf(buffer, (byte)0, 0, length);
            if (terminator < 0) terminator = length;
            return Encoding.UTF8.GetString(buffer, 0, terminator);
        }

        internal int GetDllVersion()
        {
            if (_getDllVersion == null) throw NativeFeatureNotSupportedException.ForSymbol("UFS_GetDllVersion");
            _getDllVersion(out var version).ThrowIfNotSuccess("UFS_GetDllVersion");
            return version;
        }

        internal string GetUnityVersion()
        {
            if (_getUnityVersion == null) throw NativeFeatureNotSupportedException.ForSymbol("UFS_GetUnityVersion");
            var version = ArrayPool<byte>.Shared.Rent(NameBufferCapacity);
            try
            {
                _getUnityVersion(version, NameBufferCapacity).ThrowIfNotSuccess("UFS_GetUnityVersion");
                return FromNativeUtf8(version, NameBufferCapacity);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(version);
            }
        }

        internal int GetSerializedFileVersion(IntPtr serializedFileHandle)
        {
            if (_getSerializedFileVersion == null) throw NativeFeatureNotSupportedException.ForSymbol("UFS_GetSerializedFileVersion");
            _getSerializedFileVersion(serializedFileHandle, out var version).ThrowIfNotSuccess("UFS_GetSerializedFileVersion");
            return version;
        }

        internal void Init()
        {
            _init().ThrowIfNotSuccess("UFS_Init");
        }

        internal void Cleanup()
        {
            _cleanup().ThrowIfNotSuccess("UFS_Cleanup");
        }

        internal IntPtr MountArchive(string archivePath, string mountPoint)
        {
            _mountArchive(ToNativeUtf8(archivePath), ToNativeUtf8(mountPoint), out var handle)
                .ThrowIfNotSuccess("UFS_MountArchive");
            return handle;
        }

        internal void UnmountArchive(IntPtr archiveHandle)
        {
            _unmountArchive(archiveHandle).ThrowIfNotSuccess("UFS_UnmountArchive");
        }

        internal int GetArchiveNodeCount(IntPtr archiveHandle)
        {
            _getArchiveNodeCount(archiveHandle, out var count).ThrowIfNotSuccess("UFS_GetArchiveNodeCount");
            return count;
        }

        internal ArchiveNode GetArchiveNode(IntPtr archiveHandle, int index)
        {
            var path = ArrayPool<byte>.Shared.Rent(PathBufferCapacity);
            try
            {
                _getArchiveNode(archiveHandle, index, path, PathBufferCapacity, out var size, out var flags).ThrowIfNotSuccess("UFS_GetArchiveNode");
                return new ArchiveNode(FromNativeUtf8(path, PathBufferCapacity), size, (ArchiveNodeFlags)flags);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(path);
            }
        }

        internal IntPtr OpenFile(string virtualPath)
        {
            _openFile(ToNativeUtf8(virtualPath), out IntPtr handle).ThrowIfNotSuccess("UFS_OpenFile");
            return handle;
        }

        internal long ReadFile(IntPtr fileHandle, byte[] buffer, long size)
        {
            _readFile(fileHandle, buffer, size, out var readSize).ThrowIfNotSuccess("UFS_ReadFile");
            return readSize;
        }

        internal long SeekFile(IntPtr fileHandle, long offset, SeekOrigin origin)
        {
            _seekFile(fileHandle, offset, (int)origin, out var newPosition).ThrowIfNotSuccess("UFS_SeekFile");
            return newPosition;
        }

        internal long GetFileSize(IntPtr fileHandle)
        {
            _getFileSize(fileHandle, out var size).ThrowIfNotSuccess("UFS_GetFileSize");
            return size;
        }

        internal void CloseFile(IntPtr fileHandle)
        {
            _closeFile(fileHandle).ThrowIfNotSuccess("UFS_CloseFile");
        }

        internal IntPtr OpenSerializedFile(string virtualPath)
        {
            _openSerializedFile(ToNativeUtf8(virtualPath), out IntPtr handle).ThrowIfNotSuccess("UFS_OpenSerializedFile");
            return handle;
        }

        internal void CloseSerializedFile(IntPtr serializedFileHandle)
        {
            _closeSerializedFile(serializedFileHandle).ThrowIfNotSuccess("UFS_CloseSerializedFile");
        }

        internal void AddTypeTreeSourceFromFile(string path)
        {
            if (_addTypeTreeSourceFromFile == null) throw NativeFeatureNotSupportedException.ForSymbol("UFS_AddTypeTreeSourceFromFile");
            _addTypeTreeSourceFromFile(ToNativeUtf8(path)).ThrowIfNotSuccess("UFS_AddTypeTreeSourceFromFile");
        }

        internal void RemoveTypeTreeSource(string path)
        {
            if (_removeTypeTreeSource == null) throw NativeFeatureNotSupportedException.ForSymbol("UFS_RemoveTypeTreeSource");
            _removeTypeTreeSource(ToNativeUtf8(path)).ThrowIfNotSuccess("UFS_RemoveTypeTreeSource");
        }

        internal int GetExternalReferenceCount(IntPtr serializedFileHandle)
        {
            _getExternalReferenceCount(serializedFileHandle, out var count).ThrowIfNotSuccess("UFS_GetExternalReferenceCount");
            return count;
        }

        internal ExternalReferenceInfo GetExternalReference(IntPtr serializedFileHandle, int index)
        {
            var path = ArrayPool<byte>.Shared.Rent(PathBufferCapacity);
            var guid = ArrayPool<byte>.Shared.Rent(GuidBufferCapacity);
            try
            {
                _getExternalReference(serializedFileHandle, index, path, PathBufferCapacity, guid, out var type)
                    .ThrowIfNotSuccess("UFS_GetExternalReference");

                return new ExternalReferenceInfo(
                    FromNativeUtf8(path, PathBufferCapacity), FromNativeUtf8(guid, GuidBufferCapacity), type);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(path);
                ArrayPool<byte>.Shared.Return(guid);
            }
        }

        internal int GetObjectCount(IntPtr serializedFileHandle)
        {
            _getObjectCount(serializedFileHandle, out var count).ThrowIfNotSuccess("UFS_GetObjectCount");
            return count;
        }

        internal ObjectInfo[] GetObjectInfos(IntPtr serializedFileHandle)
        {
            var count = GetObjectCount(serializedFileHandle);
            var infos = new ObjectInfo[count];
            if (count > 0)
            {
                _getObjectInfo(serializedFileHandle, infos, count).ThrowIfNotSuccess("UFS_GetObjectInfo");
            }

            return infos;
        }

        internal IntPtr GetTypeTree(IntPtr serializedFileHandle, long objectId)
        {
            _getTypeTree(serializedFileHandle, objectId, out IntPtr handle).ThrowIfNotSuccess("UFS_GetTypeTree");
            return handle;
        }

        internal TypeTreeNodeInfo GetTypeTreeNodeInfo(IntPtr typeTreeHandle, int nodeIndex)
        {
            var type = ArrayPool<byte>.Shared.Rent(NameBufferCapacity);
            var name = ArrayPool<byte>.Shared.Rent(NameBufferCapacity);
            try
            {
                _getTypeTreeNodeInfo(
                        typeTreeHandle, nodeIndex, type, NameBufferCapacity, name, NameBufferCapacity,
                        out var offset, out var size, out var flags, out var metaFlags,
                        out var firstChildNode, out var nextNode)
                    .ThrowIfNotSuccess("UFS_GetTypeTreeNodeInfo");

                return new TypeTreeNodeInfo(
                    FromNativeUtf8(type, NameBufferCapacity), FromNativeUtf8(name, NameBufferCapacity),
                    offset, size, flags, metaFlags, firstChildNode, nextNode);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(type);
                ArrayPool<byte>.Shared.Return(name);
            }
        }

        internal int GetTypeTreeCount(IntPtr serializedFileHandle)
        {
            if (_getTypeTreeCount == null) throw NativeFeatureNotSupportedException.ForSymbol("UFS_GetTypeTreeCount");
            _getTypeTreeCount(serializedFileHandle, out var count).ThrowIfNotSuccess("UFS_GetTypeTreeCount");
            return count;
        }

        internal TypeTreeInfo GetTypeTreeInfo(IntPtr serializedFileHandle, int index)
        {
            if (_getTypeTreeInfo == null) throw NativeFeatureNotSupportedException.ForSymbol("UFS_GetTypeTreeInfo");
            _getTypeTreeInfo(serializedFileHandle, index, out var info).ThrowIfNotSuccess("UFS_GetTypeTreeInfo");
            return info;
        }

        internal IntPtr GetTypeTreeByIndex(IntPtr serializedFileHandle, int index)
        {
            if (_getTypeTreeByIndex == null) throw NativeFeatureNotSupportedException.ForSymbol("UFS_GetTypeTreeByIndex");
            _getTypeTreeByIndex(serializedFileHandle, index, out IntPtr handle).ThrowIfNotSuccess("UFS_GetTypeTreeByIndex");
            return handle;
        }
        
        int IUnityFileSystemApi.GetDllVersion() => GetDllVersion();
        string IUnityFileSystemApi.GetUnityVersion() => GetUnityVersion();
        int IUnityFileSystemApi.GetSerializedFileVersion(IntPtr serializedFileHandle) => GetSerializedFileVersion(serializedFileHandle);
        void IUnityFileSystemApi.Init() => Init();
        void IUnityFileSystemApi.Cleanup() => Cleanup();
        IntPtr IUnityFileSystemApi.MountArchive(string archivePath, string mountPoint) => MountArchive(archivePath, mountPoint);
        void IUnityFileSystemApi.UnmountArchive(IntPtr archiveHandle) => UnmountArchive(archiveHandle);
        int IUnityFileSystemApi.GetArchiveNodeCount(IntPtr archiveHandle) => GetArchiveNodeCount(archiveHandle);
        ArchiveNode IUnityFileSystemApi.GetArchiveNode(IntPtr archiveHandle, int index) => GetArchiveNode(archiveHandle, index);
        IntPtr IUnityFileSystemApi.OpenFile(string virtualPath) => OpenFile(virtualPath);
        long IUnityFileSystemApi.ReadFile(IntPtr fileHandle, byte[] buffer, long size) => ReadFile(fileHandle, buffer, size);
        long IUnityFileSystemApi.SeekFile(IntPtr fileHandle, long offset, SeekOrigin origin) => SeekFile(fileHandle, offset, origin);
        long IUnityFileSystemApi.GetFileSize(IntPtr fileHandle) => GetFileSize(fileHandle);
        void IUnityFileSystemApi.CloseFile(IntPtr fileHandle) => CloseFile(fileHandle);
        IntPtr IUnityFileSystemApi.OpenSerializedFile(string virtualPath) => OpenSerializedFile(virtualPath);
        void IUnityFileSystemApi.CloseSerializedFile(IntPtr serializedFileHandle) => CloseSerializedFile(serializedFileHandle);
        void IUnityFileSystemApi.AddTypeTreeSourceFromFile(string path) => AddTypeTreeSourceFromFile(path);
        void IUnityFileSystemApi.RemoveTypeTreeSource(string path) => RemoveTypeTreeSource(path);
        int IUnityFileSystemApi.GetExternalReferenceCount(IntPtr serializedFileHandle) => GetExternalReferenceCount(serializedFileHandle);

        ExternalReferenceInfo IUnityFileSystemApi.GetExternalReference(IntPtr serializedFileHandle, int index) =>
            GetExternalReference(serializedFileHandle, index);

        int IUnityFileSystemApi.GetObjectCount(IntPtr serializedFileHandle) => GetObjectCount(serializedFileHandle);
        ObjectInfo[] IUnityFileSystemApi.GetObjectInfos(IntPtr serializedFileHandle) => GetObjectInfos(serializedFileHandle);
        IntPtr IUnityFileSystemApi.GetTypeTree(IntPtr serializedFileHandle, long objectId) => GetTypeTree(serializedFileHandle, objectId);

        TypeTreeNodeInfo IUnityFileSystemApi.GetTypeTreeNodeInfo(IntPtr typeTreeHandle, int nodeIndex) =>
            GetTypeTreeNodeInfo(typeTreeHandle, nodeIndex);

        int IUnityFileSystemApi.GetTypeTreeCount(IntPtr serializedFileHandle) => GetTypeTreeCount(serializedFileHandle);
        TypeTreeInfo IUnityFileSystemApi.GetTypeTreeInfo(IntPtr serializedFileHandle, int index) => GetTypeTreeInfo(serializedFileHandle, index);
        IntPtr IUnityFileSystemApi.GetTypeTreeByIndex(IntPtr serializedFileHandle, int index) => GetTypeTreeByIndex(serializedFileHandle, index);
    }
}