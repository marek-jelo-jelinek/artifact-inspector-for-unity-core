using System;
using System.IO;
using System.Runtime.InteropServices;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Interop;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport
{
    /// <summary>
    /// Locates the real UnityFileSystemApi binary for the current OS, copied next to the test
    /// assembly (see the Content item in Tesearis.ArtifactInspectorForUnity.Core.Tests.csproj) from
    /// UnityFileSystemApiLibraries/ in the source tree. That folder's binaries are gitignored
    /// (see its .gitkeep) so this resolves to nothing on a fresh clone until a contributor drops
    /// their own local copy in.
    /// </summary>
    internal static class NativeLibraryFixture
    {
        private const string FolderName = "UnityFileSystemApiLibraries";

        internal static bool TryResolvePath(out string path)
        {
            string fileName;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) fileName = UnityFileSystemApi.LibraryFileNameMac;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) fileName = UnityFileSystemApi.LibraryFileNameWindows;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) fileName = UnityFileSystemApi.LibraryFileNameLinux;
            else
            {
                path = null;
                return false;
            }

            path = Path.Combine(AppContext.BaseDirectory, FolderName, fileName);
            return File.Exists(path);
        }
    }
}
