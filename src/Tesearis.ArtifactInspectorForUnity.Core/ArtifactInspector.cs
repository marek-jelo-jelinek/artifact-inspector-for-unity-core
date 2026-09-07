using System;
using Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Handles;

namespace Tesearis.ArtifactInspectorForUnity.Core
{
    public static class ArtifactInspector
    {
        private static Lazy<UnityFileSystemLibraryHandle> Library =
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
        /// Opens a loose SerializedFile sitting directly on disk -- a Player Build's output files
        /// (<c>globalgamemanagers</c>, <c>sharedassets0.assets</c>, <c>level0</c>, ...), which are
        /// already bare SerializedFiles rather than archive containers. Unlike <see cref="OpenAssetBundle"/>,
        /// this never calls <c>UFS_MountArchive</c> -- it opens filePath directly, so it fails with
        /// <see cref="Native.NativeCallException"/> for an actual archive/asset bundle; use
        /// <see cref="OpenAssetBundle"/> for those instead.
        /// </summary>
        /// <exception cref="Model.SerializedFileOpenException">
        /// The native open failed and the file's bytes positively confirm it has no TypeTrees (e.g. a stripped Player build).
        /// </exception>
        /// <exception cref="Native.NativeCallException">The native open failed for any other reason.</exception>
        public static SerializedFile OpenSerializedFile(string filePath)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));

            var api = GetLibrary().Api;
            return SerializedFileOpener.Open(api, filePath, filePath, () => SerializedFileDetector.IsMissingTypeTrees(filePath));
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

        /// <summary>
        /// Test-only seam: disposes the currently loaded native library (if any -- a no-op otherwise)
        /// and resets this class back to its just-loaded-the-assembly state, so a subsequent call
        /// behaves like a fresh process. Exists because this class is designed to load its native
        /// library once per process and never unload it (matching a real Unity Editor host), which
        /// conflicts with any other test fixture that independently loads/initializes/disposes the
        /// same native library within the same test process (see UnityFileSystemApiIntegrationTests).
        /// Internal -- not part of the public API contract. Any fixture that touches this class's
        /// shared native library should call this from a [OneTimeTearDown] so it doesn't leak
        /// process-wide UFS_Init state into whichever fixture runs next.
        /// </summary>
        internal static void ResetForTests()
        {
            if (Library.IsValueCreated)
            {
                Library.Value.Dispose();
            }

            Library = new Lazy<UnityFileSystemLibraryHandle>(
                UnityFileSystemLibraryHandle.LoadAndInit, System.Threading.LazyThreadSafetyMode.PublicationOnly);
        }
    }
}