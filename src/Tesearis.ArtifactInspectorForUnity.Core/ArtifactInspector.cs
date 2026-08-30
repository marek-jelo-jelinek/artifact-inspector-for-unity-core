using System;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Handles;

namespace Tesearis.ArtifactInspectorForUnity.Core
{
    public static class ArtifactInspector
    {
        private static readonly Lazy<UnityFileSystemLibraryHandle> Library =
            new(UnityFileSystemLibraryHandle.LoadAndInit, System.Threading.LazyThreadSafetyMode.PublicationOnly);

        /// <summary>Mounts a built archive (an asset bundle or player-build data file) and returns a handle to it.</summary>
        public static ArtifactArchive OpenAssetBundle(string filePath)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));

            var api = GetLibrary().Api;
            var mountPoint = ArchiveMountPoint.NewMountPoint();
            var archiveRawHandle = api.MountArchive(filePath, mountPoint);
            var archiveHandle = new ArchiveHandle(api, archiveRawHandle);
            return new ArtifactArchive(api, archiveHandle, mountPoint);
        }

        /// <summary>
        /// Points the library at an explicit <c>UnityFileSystemApi</c> native library path, for use when
        /// there is no running Unity Editor process to auto-detect one from. Must be called once, before
        /// any other call into this library, when running outside the Editor.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The native library has already been loaded (by an earlier call into this library, or a
        /// previous call to this method) and can no longer be reconfigured.
        /// </exception>
        public static void SetupLibraryPath(string unityFileSystemApiLibraryPath)
        {
            if (unityFileSystemApiLibraryPath == null) throw new ArgumentNullException(nameof(unityFileSystemApiLibraryPath));
            if (Library.IsValueCreated)
            {
                throw new InvalidOperationException(
                    "SetupLibraryPath must be called before any other Tesearis.ArtifactInspectorForUnity.Core call. " +
                    "The native library is already loaded and cannot be reconfigured.");
            }

            Native.Interop.UnityFileSystemApi.SetupLibraryPath(unityFileSystemApiLibraryPath);
        }

        /// <summary>
        /// Registers an out-of-band TypeTree source for SerializedFile format versions ≥ 23, whose
        /// TypeTree blobs can be extracted at build time instead of stored inline. Requires a native
        /// library new enough to support this (Unity 6.5+).
        /// </summary>
        /// <remarks>
        /// This is process-wide, global state, not scoped to archives opened afterward: it affects
        /// every <see cref="ArtifactArchive"/> mounted from this process from then on, including ones
        /// already open at the time of the call.
        /// </remarks>
        /// <exception cref="NativeFeatureNotSupportedException">The loaded native library doesn't export this.</exception>
        public static void AddTypeTreeSource(string filePath)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            GetLibrary().Api.AddTypeTreeSourceFromFile(filePath);
        }

        /// <summary>Undoes a prior <see cref="AddTypeTreeSource"/> registration.</summary>
        /// <remarks>Process-wide, global state.</remarks>
        public static void RemoveTypeTreeSource(string filePath)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            GetLibrary().Api.RemoveTypeTreeSource(filePath);
        }

        /// <summary>
        /// The loaded native library's own version, as reported by <c>UFS_GetDllVersion</c>.
        /// </summary>
        /// <exception cref="NativeFeatureNotSupportedException">The loaded native library doesn't export UFS_GetDllVersion.</exception>
        public static string GetNativeLibraryVersion() => GetLibrary().Api.GetDllVersion().ToString();

        /// <summary>The Unity Editor version the loaded native library was built from.</summary>
        /// <exception cref="NativeFeatureNotSupportedException">The loaded native library doesn't export UFS_GetUnityVersion.</exception>
        public static string GetUnityEditorVersion() => GetLibrary().Api.GetUnityVersion();

        private static UnityFileSystemLibraryHandle GetLibrary() => Library.Value;
    }
}