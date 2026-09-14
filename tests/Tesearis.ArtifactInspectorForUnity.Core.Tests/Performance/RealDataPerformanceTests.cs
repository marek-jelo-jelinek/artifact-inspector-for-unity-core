using System;
using System.Diagnostics;
using Tesearis.ArtifactInspectorForUnity.Core.Adapters;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Performance
{
    /// <summary>
    /// Opt-in, real-bundle-backed performance tests: mirrors RealTestDataIntegrationTests'
    /// [OneTimeSetUp]/Assert.Ignore pattern (via the shared RealTestDataDiscovery helper), so this
    /// is skipped in CI (no native library or TestData/ content is checked in) and only exercises
    /// anything on a contributor's machine with a local Unity Editor install and real bundle(s)
    /// dropped into TestData/. Every test here logs elapsed time and a derived throughput number
    /// via TestContext.Progress.WriteLine for a human to read -- no wall-clock assertions, matching
    /// the house style established in RealTestDataIntegrationTests and PERFORMANCE_IMPROVEMENTS.md.
    /// See Performance/SyntheticPerformanceTests.cs for the CI-safe, synthetic-data counterpart.
    /// </summary>
    [TestFixture]
    public class RealDataPerformanceTests
    {
        private string[] _archiveFilePaths;

        [OneTimeSetUp]
        public void Setup()
        {
            var discovered = RealTestDataDiscovery.DiscoverAndProbe();
            _archiveFilePaths = discovered.ArchiveFilePaths;
        }

        /// <summary>Same reasoning as RealTestDataIntegrationTests.TearDown -- see there.</summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            ArtifactInspector.ResetForTests();
        }

        [Test]
        public void Inspect_EveryArchive_ReportsObjectsPerSecond()
        {
            var registry = new ArtifactAdapterRegistry();

            foreach (var filePath in _archiveFilePaths)
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                var stopwatch = Stopwatch.StartNew();
                var count = 0;
                foreach (var obj in registry.Inspect(archive))
                {
                    count++;
                }
                stopwatch.Stop();

                var objectsPerSecond = count / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                TestContext.Progress.WriteLine(
                    $"[Inspect] {filePath}: {count} objects in {stopwatch.ElapsedMilliseconds} ms " +
                    $"({objectsPerSecond:N0} objects/sec)");
            }
        }

        /// <summary>
        /// Isolates raw TypeTree-decode cost (ObjectRef.GetReader() touching just the reader root)
        /// from full adapter dispatch/allocation, so the two numbers logged here and by
        /// Inspect_EveryArchive_ReportsObjectsPerSecond can be compared side by side to see how much
        /// of total walk time is spent in each.
        /// </summary>
        [Test]
        public void SerializedFileObjects_EveryEntry_ReportsRawTypeTreeReadThroughputVsFullInspect()
        {
            foreach (var filePath in _archiveFilePaths)
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                foreach (var entryName in archive.EntryNames)
                {
                    using var serializedFile = archive.OpenSerializedFile(entryName);

                    var stopwatch = Stopwatch.StartNew();
                    var count = 0;
                    foreach (var objectRef in serializedFile.Objects)
                    {
                        _ = objectRef.GetReader().TypeName;
                        count++;
                    }
                    stopwatch.Stop();

                    var objectsPerSecond = count / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                    TestContext.Progress.WriteLine(
                        $"[SerializedFileObjects] {filePath} :: {entryName}: {count} objects in " +
                        $"{stopwatch.ElapsedMilliseconds} ms ({objectsPerSecond:N0} objects/sec)");
                }
            }
        }

        [Test]
        public void ReadRawEntry_WholeEntryVsSmallBufferedReads_ReportsMegabytesPerSecond()
        {
            const int chunkSize = 4096;

            foreach (var filePath in _archiveFilePaths)
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                foreach (var entry in archive.Entries)
                {
                    if (entry.Size == 0) continue;

                    // Whole-entry path -- ArtifactArchive.ReadRawEntry bypasses BufferedByteSource's
                    // 64 KiB window entirely for a read this size.
                    var wholeStopwatch = Stopwatch.StartNew();
                    var whole = archive.ReadRawEntry(entry.Path);
                    wholeStopwatch.Stop();
                    var wholeMBps = entry.Size / (1024.0 * 1024.0) / Math.Max(0.001, wholeStopwatch.Elapsed.TotalSeconds);

                    // Small-buffered path -- samples the buffered path's throughput on the same
                    // (possibly compressed) real entry data.
                    var source = archive.OpenRawByteSource(entry.Path);
                    var chunk = new byte[chunkSize];
                    var bufferedStopwatch = Stopwatch.StartNew();
                    long readTotal = 0;
                    for (var offset = 0L; offset < entry.Size; offset += chunkSize)
                    {
                        var toRead = (int)Math.Min(chunkSize, entry.Size - offset);
                        readTotal += source.Read(offset, chunk, 0, toRead);
                    }
                    bufferedStopwatch.Stop();
                    var bufferedMBps = readTotal / (1024.0 * 1024.0) / Math.Max(0.001, bufferedStopwatch.Elapsed.TotalSeconds);

                    Assert.That(whole.Length, Is.EqualTo(entry.Size), entry.Path);
                    Assert.That(readTotal, Is.EqualTo(entry.Size), entry.Path);

                    TestContext.Progress.WriteLine(
                        $"[ReadRawEntry] {filePath} :: {entry.Path} ({entry.Size} bytes): " +
                        $"whole={wholeMBps:N1} MB/s, buffered-{chunkSize / 1024}KiB-chunks={bufferedMBps:N1} MB/s");
                }
            }
        }

        /// <summary>
        /// The "huge scene" scenario the snapshot-caching feature targets: eagerly materializing every
        /// GameObject/Transform/MonoBehaviour up front (one sequential, offset-sorted pass), instead of
        /// each one being re-walked from scratch on every later lookup. Filtered to those three ClassIds
        /// (see TypeIdRegistry) so Mesh/Texture2D/AudioClip-style objects aren't eagerly decoded here.
        /// Logs elapsed time, throughput, and approximate managed-memory growth (GC.GetTotalMemory) --
        /// not asserted, per this file's house style, but the number this feature's memory-footprint
        /// tradeoff (see PERFORMANCE_IMPROVEMENTS.md-style reasoning in MaterializeOptions' doc comment)
        /// should be checked against on a real "huge scene" bundle.
        /// </summary>
        [Test]
        public void MaterializeAll_GameObjectTransformMonoBehaviour_ReportsThroughputAndMemoryGrowth()
        {
            bool IsSceneHierarchyType(int typeId) => typeId == 1 || typeId == 4 || typeId == 114; // GameObject, Transform, MonoBehaviour
            var options = new MaterializeOptions { TypeIdFilter = IsSceneHierarchyType };

            foreach (var filePath in _archiveFilePaths)
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                foreach (var entryName in archive.EntryNames)
                {
                    using var serializedFile = archive.OpenSerializedFile(entryName);

                    GC.Collect();
                    var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

                    var stopwatch = Stopwatch.StartNew();
                    var result = serializedFile.MaterializeAll(options);
                    stopwatch.Stop();

                    var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);

                    var objectsPerSecond = result.SucceededCount / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                    TestContext.Progress.WriteLine(
                        $"[MaterializeAll] {filePath} :: {entryName}: {result.SucceededCount} objects " +
                        $"({result.Failures.Count} failures) in {stopwatch.ElapsedMilliseconds} ms " +
                        $"({objectsPerSecond:N0} objects/sec), ~{(memoryAfter - memoryBefore) / (1024.0 * 1024.0):N1} MB managed growth");
                }
            }
        }
    }
}
