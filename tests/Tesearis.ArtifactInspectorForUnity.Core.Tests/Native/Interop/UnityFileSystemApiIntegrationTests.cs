using System;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Handles;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Interop;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Native.Interop
{
    /// <summary>
    /// Exercises the real UnityFileSystemApi native library to prove the interop layer actually
    /// loads and resolves against Unity's current ABI, not just the shape this project assumes
    /// it has. Skipped (not failed) when no local binary is available for this OS, see
    /// UnityFileSystemApiLibraries/.gitkeep for how to provide one.
    /// </summary>
    [TestFixture]
    public class UnityFileSystemApiIntegrationTests
    {
        [OneTimeSetUp]
        public void SetupLibraryPath()
        {
            if (!NativeLibraryFixture.TryResolvePath(out var path))
            {
                Assert.Ignore(
                    "No local UnityFileSystemApi binary for this OS under UnityFileSystemApiLibraries/. " +
                    "Copy one from a local Unity Editor install to run this fixture, see UnityFileSystemApiLibraries/.gitkeep.");
            }

            UnityFileSystemApi.SetupLibraryPath(path);
        }

        [Test]
        public void LoadAndInit_RealNativeLibrary_LoadsAndResolvesAllExports()
        {
            // The UnityFileSystemLibraryHandle constructor already resolves every UFS_* export
            // and calls the real UFS_Init; not throwing here is the proof this loads against the
            // actual native ABI. Disposing calls UFS_Cleanup and dlclose/FreeLibrary.
            using var library = UnityFileSystemLibraryHandle.LoadAndInit();

            Assert.That(library.IsInvalid, Is.False);
        }

        [Test]
        public void MountArchive_NonexistentPath_ThrowsNativeCallException()
        {
            using var library = UnityFileSystemLibraryHandle.LoadAndInit();

            Assert.Throws<NativeCallException>(() =>
                library.Api.MountArchive("/nonexistent/path/to/artifact-inspector-for-unity-test-bundle", "archive:/does-not-exist"));
        }

        [Test]
        public void AddTypeTreeSourceFromFile_NonexistentPath_ThrowsNativeCallExceptionOrNotSupported()
        {
            // UFS_AddTypeTreeSourceFromFile only exists in native libraries new enough to support it
            // (Unity 6.5+); whichever local binary is available for this OS may or may not be one of
            // those. Either way, a nonexistent source path must fail cleanly as one of these two
            // exception types -- never an unhandled/raw native error.
            using var library = UnityFileSystemLibraryHandle.LoadAndInit();

            Assert.That(
                () => library.Api.AddTypeTreeSourceFromFile("/nonexistent/path/to/typetree-source"),
                Throws.TypeOf<NativeCallException>().Or.TypeOf<NativeFeatureNotSupportedException>());
        }

        [Test]
        public void RemoveTypeTreeSource_NonexistentPath_ThrowsNativeCallExceptionOrNotSupported()
        {
            using var library = UnityFileSystemLibraryHandle.LoadAndInit();

            Assert.That(
                () => library.Api.RemoveTypeTreeSource("/nonexistent/path/to/typetree-source"),
                Throws.TypeOf<NativeCallException>().Or.TypeOf<NativeFeatureNotSupportedException>());
        }

        [Test]
        public void GetTypeTreeCount_InvalidHandle_ThrowsNativeCallExceptionOrNotSupported()
        {
            // UFS_GetTypeTreeCount isn't present in every native library observed in the wild
            // (confirmed absent from at least one that otherwise opens files fine), so whichever
            // local binary is available for this OS may or may not export it.
            using var library = UnityFileSystemLibraryHandle.LoadAndInit();

            Assert.That(
                () => library.Api.GetTypeTreeCount(IntPtr.Zero),
                Throws.TypeOf<NativeCallException>().Or.TypeOf<NativeFeatureNotSupportedException>());
        }

        [Test]
        public void GetTypeTreeInfo_InvalidHandle_ThrowsNativeCallExceptionOrNotSupported()
        {
            using var library = UnityFileSystemLibraryHandle.LoadAndInit();

            Assert.That(
                () => library.Api.GetTypeTreeInfo(IntPtr.Zero, 0),
                Throws.TypeOf<NativeCallException>().Or.TypeOf<NativeFeatureNotSupportedException>());
        }

        [Test]
        public void GetTypeTreeByIndex_InvalidHandle_ThrowsNativeCallExceptionOrNotSupported()
        {
            using var library = UnityFileSystemLibraryHandle.LoadAndInit();

            Assert.That(
                () => library.Api.GetTypeTreeByIndex(IntPtr.Zero, 0),
                Throws.TypeOf<NativeCallException>().Or.TypeOf<NativeFeatureNotSupportedException>());
        }

        [Test]
        public void GetDllVersion_RealNativeLibrary_SucceedsOrThrowsNotSupported()
        {
            // UFS_GetDllVersion isn't present in every native library observed in the wild
            // (confirmed absent from at least one that otherwise opens files fine), so whichever
            // local binary is available for this OS may or may not export it.
            using var library = UnityFileSystemLibraryHandle.LoadAndInit();

            try
            {
                library.Api.GetDllVersion();
            }
            catch (NativeFeatureNotSupportedException)
            {
                // Acceptable: this native library doesn't export UFS_GetDllVersion.
            }
        }

        [Test]
        public void GetUnityVersion_RealNativeLibrary_SucceedsOrThrowsNotSupported()
        {
            using var library = UnityFileSystemLibraryHandle.LoadAndInit();

            try
            {
                library.Api.GetUnityVersion();
            }
            catch (NativeFeatureNotSupportedException)
            {
                // Acceptable: this native library doesn't export UFS_GetUnityVersion.
            }
        }

        [Test]
        public void GetSerializedFileVersion_InvalidHandle_ThrowsNativeCallExceptionOrNotSupported()
        {
            using var library = UnityFileSystemLibraryHandle.LoadAndInit();

            Assert.That(
                () => library.Api.GetSerializedFileVersion(IntPtr.Zero),
                Throws.TypeOf<NativeCallException>().Or.TypeOf<NativeFeatureNotSupportedException>());
        }
    }
}
