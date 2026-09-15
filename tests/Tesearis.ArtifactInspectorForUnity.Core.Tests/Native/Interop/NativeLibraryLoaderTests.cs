using System;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Interop;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Native.Interop
{
    [TestFixture]
    public class NativeLibraryLoaderTests
    {
        [TestCase(null)]
        [TestCase("")]
        public void Load_NullOrEmptyPath_ThrowsArgumentException(string path)
        {
            Assert.Throws<ArgumentException>(() => NativeLibraryLoader.Load(path));
        }

        [Test]
        public void Load_NonExistentLibrary_ThrowsArtifactInspectorException()
        {
            var ex = Assert.Throws<ArtifactInspectorException>(() =>
                NativeLibraryLoader.Load("non_existent_library_which_does_not_exist_xyz123.dylib"));

            Assert.That(ex.Message, Does.Contain("Failed to load native library"));
        }

        [Test]
        public void Free_ZeroHandle_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => NativeLibraryLoader.Free(IntPtr.Zero));
        }

        [Test]
        public void GetExport_InvalidHandle_ThrowsArtifactInspectorException()
        {
            var ex = Assert.Throws<ArtifactInspectorException>(() =>
                NativeLibraryLoader.GetExport((IntPtr)0x1234, "non_existent_symbol"));

            Assert.That(ex.Message, Does.Contain("Native symbol 'non_existent_symbol' was not found"));
        }
    }
}
