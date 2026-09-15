using System;
using System.Collections.Generic;
using System.Diagnostics;
using Tesearis.ArtifactInspectorForUnity.Core.Adapters;
using Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Model
{
    /// <summary>
    /// Exercises the full public pipeline -- ArtifactInspector.OpenAssetBundle/OpenSerializedFile
    /// through ArtifactArchive/SerializedFile/ObjectRef and the adapter dispatch mechanism --
    /// against whatever real Unity-built file(s) a contributor has dropped into TestData/. Content
    /// is never assumed (any real file works, no naming/extension convention), so every assertion
    /// here is a structural invariant rather than a hardcoded expected value. Discovered files are
    /// split into two lanes in Setup: <see cref="_archiveFilePaths"/> (anything OpenAssetBundle can
    /// mount -- .bundle-style containers) and <see cref="_looseSerializedFilePaths"/> (bare
    /// SerializedFiles sitting directly on disk, e.g. a Player Build's globalgamemanagers/level0/
    /// sharedassets*.assets, opened via OpenSerializedFile instead). Archive-only APIs (Entries,
    /// OpenRawByteSource, adapter dispatch) only make sense for the archive lane; tests that only
    /// need SerializedFile-level structure exercise both. Files that open as neither (a Player
    /// Build's ancillary DLLs/manifests/out-of-line resource fragments) are silently excluded from
    /// both lanes -- see Setup. Skipped (not failed) when no native library or no test data is
    /// available -- see TestData/.gitkeep and UnityFileSystemApiLibraries/.gitkeep.
    /// </summary>
    [TestFixture]
    public class RealTestDataIntegrationTests
    {
        private string[] _archiveFilePaths;
        private string[] _looseSerializedFilePaths;

        [OneTimeSetUp]
        public void Setup()
        {
            var discovered = RealTestDataDiscovery.DiscoverAndProbe();
            _archiveFilePaths = discovered.ArchiveFilePaths;
            _looseSerializedFilePaths = discovered.LooseSerializedFilePaths;
        }

        /// <summary>
        /// Releases the native library this fixture loaded via ArtifactInspector's shared,
        /// normally-never-unloaded singleton -- ArtifactInspector is designed to load its native
        /// library once per process and keep it forever (matching a real Unity Editor host), which
        /// otherwise leaks process-wide UFS_Init state into other fixtures in the same test process
        /// that independently load/init/dispose the same native library themselves (e.g.
        /// UnityFileSystemApiIntegrationTests), causing UFS_Init to fail with AlreadyInitialized
        /// depending on run order. Runs even when Setup ignored (no native library/test data) --
        /// ResetForTests() no-ops in that case.
        /// </summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            ArtifactInspector.ResetForTests();
        }

        [Test]
        public void OpenAssetBundle_EveryFileInTestData_OpensAndListsEntriesWithoutThrowing()
        {
            foreach (var filePath in _archiveFilePaths)
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                Assert.That(archive.EntryNames, Is.Not.Null, filePath);
            }
        }

        /// <summary>
        /// Opens every SerializedFile reachable from TestData/ -- every entry in every archive
        /// lane file, plus every loose lane file directly -- as (label, serializedFile, isLooseFile)
        /// tuples, for tests that only need SerializedFile-level structure (not archive-only APIs
        /// like Entries or OpenRawByteSource). A loose file positively confirmed to have no
        /// TypeTrees is skipped -- there's nothing for these tests to open.
        /// </summary>
        private IEnumerable<(string Label, SerializedFile SerializedFile, bool IsLooseFile)> OpenAllSerializedFiles()
        {
            foreach (var filePath in _archiveFilePaths)
            {
                TestContext.Progress.WriteLine($"[OpenAllSerializedFiles] opening archive {filePath}");
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                foreach (var entryName in archive.EntryNames)
                {
                    var openStopwatch = Stopwatch.StartNew();
                    using var serializedFile = archive.OpenSerializedFile(entryName);
                    TestContext.Progress.WriteLine(
                        $"[OpenAllSerializedFiles] opened entry {filePath} :: {entryName} " +
                        $"({serializedFile.Objects.Count} objects, took {openStopwatch.ElapsedMilliseconds} ms)");
                    yield return ($"{filePath} :: {entryName}", serializedFile, false);
                }
            }

            foreach (var filePath in _looseSerializedFilePaths)
            {
                TestContext.Progress.WriteLine($"[OpenAllSerializedFiles] opening loose file {filePath}");
                SerializedFile serializedFile;
                var openStopwatch = Stopwatch.StartNew();
                try
                {
                    serializedFile = ArtifactInspector.OpenSerializedFile(filePath);
                }
                catch (SerializedFileOpenException ex) when (ex.MissingTypeTrees)
                {
                    continue;
                }

                using (serializedFile)
                {
                    TestContext.Progress.WriteLine(
                        $"[OpenAllSerializedFiles] opened loose file {filePath} " +
                        $"({serializedFile.Objects.Count} objects, took {openStopwatch.ElapsedMilliseconds} ms)");
                    yield return (filePath, serializedFile, true);
                }
            }
        }

        /// <summary>
        /// True for a <see cref="NativeCallException"/> matching a real, confirmed limitation of
        /// <c>UFS_GetTypeTree</c> found while validating against a real Player Build: it fails with
        /// <c>InvalidObjectId</c> for every object of a SerializedFile opened via
        /// <see cref="ArtifactInspector.OpenSerializedFile"/> (i.e. not part of a mounted archive)
        /// -- confirmed not an object-id bug on this library's side (the exact same ids read back
        /// via <c>UFS_GetObjectInfo</c> are rejected; every id from -1 through the object count was
        /// tried). Nothing to structurally validate for a loose-lane object until this native
        /// behavior is understood well enough to work around.
        /// </summary>
        private static bool IsKnownLooseFileTypeTreeLimitation(bool isLooseFile, NativeCallException ex) =>
            isLooseFile && ex.Message.Contains("UFS_GetTypeTree");

        /// <summary>How often <see cref="Objects_EveryObjectInEveryEntry_HasASelfConsistentClassNameAndByteSize"/> logs progress.</summary>
        private const int ObjectProgressLogInterval = 100;

        [Test]
        public void Objects_EveryObjectInEveryEntry_HasASelfConsistentClassNameAndByteSize()
        {
            foreach (var (label, serializedFile, isLooseFile) in OpenAllSerializedFiles())
            {
                var entryStopwatch = Stopwatch.StartNew();
                var objectCount = serializedFile.Objects.Count;
                var index = 0;

                foreach (var objectRef in serializedFile.Objects)
                {
                    if (index % ObjectProgressLogInterval == 0)
                    {
                        TestContext.Progress.WriteLine(
                            $"[Objects] {label} :: object {index}/{objectCount} " +
                            $"(elapsed {entryStopwatch.ElapsedMilliseconds} ms)");
                    }

                    var objectLabel = $"{label} :: pathId {objectRef.PathId}";
                    Assert.That(objectRef.ByteSize, Is.GreaterThanOrEqualTo(0), objectLabel);

                    try
                    {
                        var reader = objectRef.GetReader();
                        Assert.That(reader.TypeName, Is.Not.Null.And.Not.Empty, objectLabel);

                        if (reader.HasField("m_Name"))
                        {
                            // Just prove it can be read without throwing. The actual name is
                            // arbitrary user data, not something to assert a specific value for.
                            Assert.DoesNotThrow(() => reader.Field("m_Name").AsString(), objectLabel);
                        }
                    }
                    catch (UnsupportedManagedReferenceShapeException)
                    {
                        // Known, documented limitation (see README's "Known limitations"):
                        // [SerializeReference] polymorphic fields aren't decoded, so this
                        // object's offsets can't be computed -- nothing to structurally
                        // validate for it.
                        // ignore
                    }
                    catch (NativeCallException ex) when (IsKnownLooseFileTypeTreeLimitation(isLooseFile, ex))
                    {
                        // ignore
                    }

                    index++;
                }

                TestContext.Progress.WriteLine(
                    $"[Objects] {label} :: done, {objectCount} objects in {entryStopwatch.ElapsedMilliseconds} ms");
            }
        }

        /// <summary>
        /// UFS_GetTypeTreeCount/Info/ByIndex aren't present in every native library observed in
        /// the wild (confirmed absent from at least one that otherwise opens files and reads
        /// per-object type trees fine) -- entries whose file's native library lacks this trio are
        /// skipped rather than failed, since there's nothing to cross-validate against.
        /// </summary>
        private static bool TryGetTypeTrees(SerializedFile serializedFile, out IReadOnlyList<TypeTreeSummary> typeTrees)
        {
            try
            {
                typeTrees = serializedFile.TypeTrees;
                return true;
            }
            catch (NativeFeatureNotSupportedException)
            {
                typeTrees = null;
                return false;
            }
        }

        [Test]
        public void TypeTrees_EveryEntryInEveryFile_HasUniqueInRangeIndexAndWalkableTree()
        {
            foreach (var (label, serializedFile, _) in OpenAllSerializedFiles())
            {
                if (!TryGetTypeTrees(serializedFile, out var typeTrees)) continue;

                var entryStopwatch = Stopwatch.StartNew();
                var seenIndices = new HashSet<int>();

                foreach (var summary in typeTrees)
                {
                    var summaryLabel = $"{label} :: type tree {summary.Index}";
                    Assert.That(summary.Index, Is.InRange(0, typeTrees.Count - 1), summaryLabel);
                    Assert.That(seenIndices.Add(summary.Index), Is.True, summaryLabel);
                    Assert.That(summary.ClassName, Is.Not.Null, summaryLabel);
                    Assert.That(summary.NamespaceName, Is.Not.Null, summaryLabel);
                    Assert.That(summary.AssemblyName, Is.Not.Null, summaryLabel);
                    Assert.That(summary.Hash, Is.Not.Null.And.Count.EqualTo(4), summaryLabel);

                    var root = serializedFile.GetTypeTreeByIndex(summary.Index);
                    Assert.That(root.TypeName, Is.Not.Null.And.Not.Empty, summaryLabel);
                }

                TestContext.Progress.WriteLine(
                    $"[TypeTrees] {label} :: done, {typeTrees.Count} type trees in {entryStopwatch.ElapsedMilliseconds} ms");
            }
        }

        [Test]
        public void GetTypeTreeByIndex_ForAnObjectTypeMatchingALiveObject_MatchesThatObjectsReaderRootShape()
        {
            foreach (var (label, serializedFile, isLooseFile) in OpenAllSerializedFiles())
            {
                if (!TryGetTypeTrees(serializedFile, out var typeTrees)) continue;

                var entryStopwatch = Stopwatch.StartNew();

                foreach (var summary in typeTrees)
                {
                    if (summary.Category != TypeTreeCategory.ObjectType) continue;

                    ObjectRef matchingObject = default;
                    var found = false;
                    foreach (var objectRef in serializedFile.Objects)
                    {
                        if (objectRef.TypeId != summary.TypeId) continue;
                        matchingObject = objectRef;
                        found = true;
                        break;
                    }

                    if (!found) continue;

                    var summaryLabel = $"{label} :: type tree {summary.Index}";
                    var byIndex = serializedFile.GetTypeTreeByIndex(summary.Index);

                    TypeTreeReader byObject;
                    try
                    {
                        byObject = matchingObject.GetReader();
                    }
                    catch (NativeCallException ex) when (IsKnownLooseFileTypeTreeLimitation(isLooseFile, ex))
                    {
                        continue;
                    }

                    Assert.That(byIndex.TypeName, Is.EqualTo(byObject.TypeName), summaryLabel);
                }

                TestContext.Progress.WriteLine(
                    $"[GetTypeTreeByIndex] {label} :: done, {typeTrees.Count} type trees in {entryStopwatch.ElapsedMilliseconds} ms");
            }
        }

        [Test]
        public void ExternalReferences_EveryEntryInEveryFile_HasNonNullPathAndGuid()
        {
            foreach (var (label, serializedFile, _) in OpenAllSerializedFiles())
            {
                foreach (var externalReference in serializedFile.ExternalReferences)
                {
                    Assert.That(externalReference.Path, Is.Not.Null, label);
                    Assert.That(externalReference.Guid, Is.Not.Null, label);
                }
            }
        }

        [Test]
        public void OpenRawByteSource_EveryEntry_ProducesANonNullSourceWithPositiveLength()
        {
            foreach (var filePath in _archiveFilePaths)
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                foreach (var entryName in archive.EntryNames)
                {
                    var label = $"{filePath} :: {entryName}";
                    var byteSource = archive.OpenRawByteSource(entryName);

                    Assert.That(byteSource, Is.Not.Null, label);
                    Assert.That(byteSource.Length, Is.GreaterThan(0), label);
                }
            }
        }

        [Test]
        public void Entries_EveryFileInTestData_ListsAllEntriesWithNonNegativeSizes()
        {
            foreach (var filePath in _archiveFilePaths)
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                Assert.That(archive.Entries, Is.Not.Null, filePath);
                Assert.That(archive.Entries.Count, Is.GreaterThanOrEqualTo(archive.EntryNames.Count), filePath);

                foreach (var entry in archive.Entries)
                {
                    var label = $"{filePath} :: {entry.Path}";
                    Assert.That(entry.Path, Is.Not.Null, label);
                    Assert.That(entry.Size, Is.GreaterThanOrEqualTo(0), label);
                }
            }
        }

        [Test]
        public void Entries_IsSerializedFileFlag_IsConsistentWithEntryNames()
        {
            foreach (var filePath in _archiveFilePaths)
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                var serializedFileEntryPaths = new HashSet<string>();
                foreach (var entry in archive.Entries)
                {
                    if (entry.IsSerializedFile)
                    {
                        serializedFileEntryPaths.Add(entry.Path);
                    }
                }

                Assert.That(serializedFileEntryPaths, Is.EquivalentTo(archive.EntryNames), filePath);
            }
        }

        [Test]
        public void ReadRawEntry_WholeEntry_MatchesLengthAndRangedRead()
        {
            foreach (var filePath in _archiveFilePaths)
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                foreach (var entry in archive.Entries)
                {
                    var label = $"{filePath} :: {entry.Path}";

                    var whole = archive.ReadRawEntry(entry.Path);
                    var ranged = archive.ReadRawEntry(entry.Path, 0, (int)entry.Size);

                    Assert.That(whole.Length, Is.EqualTo(entry.Size), label);

                    // Is.EqualTo on two byte[] falls back to NUnit's boxed, element-by-element
                    // IEnumerable comparison -- fine for small arrays, but on a multi-MB entry the
                    // boxing/GC cost dwarfs the actual reads. SequenceEqual is the allocation-free
                    // equivalent; fall back to Is.EqualTo only on a mismatch, for a diagnosable diff.
                    if (!whole.AsSpan().SequenceEqual(ranged))
                    {
                        Assert.That(whole, Is.EqualTo(ranged), label);
                    }
                }
            }
        }

        /// <summary>
        /// The actual cross-validation body of <see cref="SerializedFileDetector_TryDetect_MatchesNativeObjectsAndExternalReferences_ForEveryEntry"/>,
        /// shared between the archive lane (byteSource comes from the mounted archive) and the
        /// loose lane (byteSource reads the file directly).
        /// </summary>
        private static void AssertDetectorMatchesNative(string label, SerializedFile serializedFile, IRandomAccessByteSource byteSource)
        {
            var detected = SerializedFileDetector.TryDetect(byteSource, out var info);
            Assert.That(detected, Is.True, label);

            if (!info.MetadataParsed)
            {
                // This entry's version is outside the 19-23 range this parser understands, even
                // though the native library opened it fine -- nothing to cross-validate.
                return;
            }

            Assert.That(info.Objects.Count, Is.EqualTo(serializedFile.Objects.Count), label);
            for (var i = 0; i < info.Objects.Count; i++)
            {
                var fromScratch = info.Objects[i];
                var native = serializedFile.Objects[i];
                var objectLabel = $"{label} :: object {i}";
                Assert.That(fromScratch.PathId, Is.EqualTo(native.PathId), objectLabel);
                Assert.That(fromScratch.TypeId, Is.EqualTo(native.TypeId), objectLabel);
                Assert.That(fromScratch.ByteOffset, Is.EqualTo(native.ByteOffset), objectLabel);
                Assert.That(fromScratch.ByteSize, Is.EqualTo(native.ByteSize), objectLabel);
            }

            Assert.That(info.ExternalReferences.Count, Is.EqualTo(serializedFile.ExternalReferences.Count), label);
            for (var i = 0; i < info.ExternalReferences.Count; i++)
            {
                var fromScratch = info.ExternalReferences[i];
                var native = serializedFile.ExternalReferences[i];
                var refLabel = $"{label} :: external reference {i}";
                Assert.That(fromScratch.Path, Is.EqualTo(native.Path), refLabel);
                Assert.That(fromScratch.Guid, Is.EqualTo(native.Guid), refLabel);
                Assert.That(fromScratch.Type, Is.EqualTo(native.Type), refLabel);
            }
        }

        /// <summary>
        /// Cross-validates SerializedFileDetector's from-scratch parse against
        /// UnityFileSystemApi's native results for every entry a contributor's TestData/ files
        /// happen to contain -- no stripped fixture is required for this to be meaningful: any
        /// entry that natively opens fine and whose version is in this library's supported
        /// metadata range (19-23) gets its object list and external references compared field by
        /// field against the native-parsed equivalents, which is a strong correctness check of the
        /// whole from-scratch parser (including the GUID formatter) against Unity's own results. An
        /// entry that natively fails to open specifically because it has no TypeTrees is exercised
        /// for free too, via the new SerializedFileOpenException triage path, if a contributor's
        /// TestData/ happens to contain one.
        /// </summary>
        [Test]
        public void SerializedFileDetector_TryDetect_MatchesNativeObjectsAndExternalReferences_ForEveryEntry()
        {
            foreach (var filePath in _archiveFilePaths)
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                foreach (var entryName in archive.EntryNames)
                {
                    var label = $"{filePath} :: {entryName}";

                    SerializedFile serializedFile;
                    try
                    {
                        serializedFile = archive.OpenSerializedFile(entryName);
                    }
                    catch (SerializedFileOpenException ex) when (ex.MissingTypeTrees)
                    {
                        // No native ground truth to compare against for a stripped entry -- this is
                        // exactly the case this feature exists for, just nothing to cross-validate.
                        continue;
                    }

                    using (serializedFile)
                    {
                        var entryStopwatch = Stopwatch.StartNew();
                        AssertDetectorMatchesNative(label, serializedFile, archive.OpenRawByteSource(entryName));
                        TestContext.Progress.WriteLine(
                            $"[SerializedFileDetector] {label} :: done in {entryStopwatch.ElapsedMilliseconds} ms");
                    }
                }
            }

            foreach (var filePath in _looseSerializedFilePaths)
            {
                SerializedFile serializedFile;
                try
                {
                    serializedFile = ArtifactInspector.OpenSerializedFile(filePath);
                }
                catch (SerializedFileOpenException ex) when (ex.MissingTypeTrees)
                {
                    // No native ground truth to compare against for a stripped file -- this is
                    // exactly the case this feature exists for, just nothing to cross-validate.
                    continue;
                }

                using (serializedFile)
                using (var byteSource = new FileStreamByteSource(filePath))
                {
                    AssertDetectorMatchesNative(filePath, serializedFile, byteSource);
                }
            }
        }

        [Test]
        public void Inspect_EveryObjectInEveryFile_NeverThrows_AndNothingIsSilentlyDropped()
        {
            var registry = new ArtifactAdapterRegistry();

            // Archive lane: registry.Inspect(ArtifactArchive) is an archive-level entry point that
            // iterates all serialized files within the mounted archive container.
            foreach (var filePath in _archiveFilePaths)
            {
                TestContext.Progress.WriteLine($"[Inspect] opening archive {filePath}");
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                var entryStopwatch = Stopwatch.StartNew();
                var count = 0;
                foreach (var obj in registry.Inspect(archive))
                {
                    if (count % ObjectProgressLogInterval == 0)
                    {
                        TestContext.Progress.WriteLine(
                            $"[Inspect] {filePath} :: object {count} (elapsed {entryStopwatch.ElapsedMilliseconds} ms)");
                    }

                    // Every object comes back as either a known adapter's result or a
                    // RawObject fallback -- Adapt()/Inspect() never return null.
                    Assert.That(obj, Is.Not.Null, filePath);
                    count++;
                }

                TestContext.Progress.WriteLine(
                    $"[Inspect] {filePath} :: done, {count} objects in {entryStopwatch.ElapsedMilliseconds} ms");
            }
        }
    }
}
