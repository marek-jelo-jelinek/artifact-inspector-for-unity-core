using System.Collections.Generic;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport
{
    /// <summary>
    /// Shared [OneTimeSetUp] discovery logic for any fixture that runs against a contributor's
    /// local TestData/ files -- native library resolution, TestData/ discovery, and splitting
    /// files into the archive lane (anything OpenAssetBundle can mount) vs. the loose-SerializedFile
    /// lane (a bare SerializedFile sitting directly on disk). Extracted from
    /// RealTestDataIntegrationTests so RealDataPerformanceTests can reuse the exact same
    /// discovery/skip behavior without duplicating it. Calls Assert.Ignore (same messages as
    /// before this extraction) when no native library or no usable test data is available, so
    /// callers get identical skip behavior just by invoking this from their own [OneTimeSetUp].
    /// </summary>
    internal static class RealTestDataDiscovery
    {
        internal readonly struct Result
        {
            internal string[] ArchiveFilePaths { get; }
            internal string[] LooseSerializedFilePaths { get; }

            internal Result(string[] archiveFilePaths, string[] looseSerializedFilePaths)
            {
                ArchiveFilePaths = archiveFilePaths;
                LooseSerializedFilePaths = looseSerializedFilePaths;
            }
        }

        internal static Result DiscoverAndProbe()
        {
            if (!NativeLibraryFixture.TryResolvePath(out var libraryPath))
            {
                Assert.Ignore(
                    "No local UnityFileSystemApi binary for this OS under UnityFileSystemApiLibraries/. " +
                    "Copy one from a local Unity Editor install to run this fixture, see UnityFileSystemApiLibraries/.gitkeep.");
            }

            var filePaths = TestDataFixture.DiscoverFiles();
            if (filePaths.Length == 0)
            {
                Assert.Ignore(
                    "No files under TestData/. Drop any real Unity-built file (asset bundle, player build output, ...) " +
                    "in there to run this fixture, see TestData/.gitkeep.");
            }

            ArtifactInspector.SetupLibraryPath(libraryPath);

            // A real Player Build output directory is not just SerializedFiles/archives -- it also
            // has ancillary files (Managed/*.dll, boot.config, *.json manifests, unity_app_guid,
            // and out-of-line resource fragments like globalgamemanagers.assets.split0/1/2, which
            // aren't independently self-describing -- Unity's virtual filesystem stitches them
            // back onto their base file). Those aren't a SerializedFile-or-archive shape at all, so
            // they're neither a bug nor exercised by any fixture using this helper -- just excluded
            // from both lanes.
            var archiveFilePaths = new List<string>();
            var looseSerializedFilePaths = new List<string>();
            foreach (var filePath in filePaths)
            {
                if (TryProbeAsArchive(filePath))
                {
                    archiveFilePaths.Add(filePath);
                }
                else if (TryProbeAsLooseSerializedFile(filePath))
                {
                    looseSerializedFilePaths.Add(filePath);
                }
            }

            if (archiveFilePaths.Count == 0 && looseSerializedFilePaths.Count == 0)
            {
                Assert.Ignore(
                    "No file under TestData/ opens as either an archive or a loose SerializedFile " +
                    "-- only ancillary files (DLLs, manifests, resource fragments, ...) were found.");
            }

            return new Result(archiveFilePaths.ToArray(), looseSerializedFilePaths.ToArray());
        }

        private static bool TryProbeAsArchive(string filePath)
        {
            try
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);
                return true;
            }
            catch (NativeCallException)
            {
                return false;
            }
        }

        private static bool TryProbeAsLooseSerializedFile(string filePath)
        {
            try
            {
                using var serializedFile = ArtifactInspector.OpenSerializedFile(filePath);
                return true;
            }
            catch (SerializedFileOpenException ex) when (ex.MissingTypeTrees)
            {
                // Positively confirmed to be a stripped SerializedFile -- still counts as "is one".
                return true;
            }
            catch (NativeCallException)
            {
                return false;
            }
        }
    }
}
