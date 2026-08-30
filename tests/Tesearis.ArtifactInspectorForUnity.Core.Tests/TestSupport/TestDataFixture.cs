using System;
using System.IO;
using System.Linq;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport
{
    /// <summary>
    /// Lists real Unity-built files (asset bundles, player build output, or anything else a
    /// contributor wants to exercise the pipeline against) copied next to the test assembly
    /// (see the Content item in Tesearis.ArtifactInspectorForUnity.Core.Tests.csproj) from TestData/ in the
    /// source tree. That folder's contents are gitignored (see its .gitkeep) so this resolves
    /// to an empty list on a fresh clone until a contributor drops their own local files in --
    /// no naming or extension convention is assumed, since any real Unity-built file works.
    /// </summary>
    internal static class TestDataFixture
    {
        private const string FolderName = "TestData";
        private const string PlaceholderFileName = ".gitkeep";

        internal static string[] DiscoverFiles()
        {
            var folder = Path.Combine(AppContext.BaseDirectory, FolderName);
            if (!Directory.Exists(folder)) return Array.Empty<string>();

            return Directory.GetFiles(folder)
                .Where(path => !string.Equals(Path.GetFileName(path), PlaceholderFileName, StringComparison.Ordinal))
                .ToArray();
        }
    }
}
