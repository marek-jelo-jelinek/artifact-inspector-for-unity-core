using System;
using System.Runtime.InteropServices;
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

        // Passing a handle GetExport's underlying OS API (dlsym/GetProcAddress) never handed out
        // is undefined behavior there, not a condition this wrapper can catch -- it can (and did,
        // in CI) segfault the process instead of throwing. So this test loads a real, always-present
        // library to get a genuine handle, and only fabricates the unresolvable part (the symbol name).
        private static string GetKnownGoodLibraryPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "kernel32.dll";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "/usr/lib/libSystem.B.dylib";
            return "libc.so.6";
        }

        [Test]
        public void GetExport_UnknownSymbol_ThrowsArtifactInspectorException()
        {
            var handle = NativeLibraryLoader.Load(GetKnownGoodLibraryPath());
            try
            {
                var ex = Assert.Throws<ArtifactInspectorException>(() =>
                    NativeLibraryLoader.GetExport(handle, "non_existent_symbol_xyz123"));

                Assert.That(ex.Message, Does.Contain("Native symbol 'non_existent_symbol_xyz123' was not found"));
            }
            finally
            {
                NativeLibraryLoader.Free(handle);
            }
        }
    }
}
