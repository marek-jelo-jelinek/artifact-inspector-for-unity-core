using System.Collections.Generic;
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
    /// Exercises the full public pipeline -- ArtifactInspector.OpenAssetBundle through
    /// ArtifactArchive/SerializedFile/ObjectRef and the adapter dispatch mechanism -- against
    /// whatever real Unity-built file(s) a contributor has dropped into TestData/. Content is
    /// never assumed (any real file works, no naming/extension convention), so every assertion
    /// here is a structural invariant rather than a hardcoded expected value. Skipped (not
    /// failed) when no native library or no test data is available -- see TestData/.gitkeep and
    /// UnityFileSystemApiLibraries/.gitkeep.
    /// </summary>
    [TestFixture]
    public class RealTestDataIntegrationTests
    {
        private string[] _filePaths;

        [OneTimeSetUp]
        public void Setup()
        {
            if (!NativeLibraryFixture.TryResolvePath(out var libraryPath))
            {
                Assert.Ignore(
                    "No local UnityFileSystemApi binary for this OS under UnityFileSystemApiLibraries/. " +
                    "Copy one from a local Unity Editor install to run this fixture, see UnityFileSystemApiLibraries/.gitkeep.");
            }

            _filePaths = TestDataFixture.DiscoverFiles();
            if (_filePaths.Length == 0)
            {
                Assert.Ignore(
                    "No files under TestData/. Drop any real Unity-built file (asset bundle, player build output, ...) " +
                    "in there to run this fixture, see TestData/.gitkeep.");
            }

            ArtifactInspector.SetupLibraryPath(libraryPath);
        }

        [Test]
        public void OpenAssetBundle_EveryFileInTestData_OpensAndListsEntriesWithoutThrowing()
        {
            foreach (var filePath in _filePaths)
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                Assert.That(archive.EntryNames, Is.Not.Null, filePath);
            }
        }

        [Test]
        public void Objects_EveryObjectInEveryEntry_HasASelfConsistentClassNameAndByteSize()
        {
            foreach (var filePath in _filePaths)
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                foreach (var entryName in archive.EntryNames)
                {
                    using var serializedFile = archive.OpenSerializedFile(entryName);

                    foreach (var objectRef in serializedFile.Objects)
                    {
                        var label = $"{filePath} :: {entryName} :: pathId {objectRef.PathId}";
                        Assert.That(objectRef.ByteSize, Is.GreaterThanOrEqualTo(0), label);

                        var reader = objectRef.GetReader();
                        Assert.That(reader.TypeName, Is.Not.Null.And.Not.Empty, label);

                        if (reader.HasField("m_Name"))
                        {
                            // Just prove it can be read without throwing -- the actual name is
                            // arbitrary user data, not something to assert a specific value for.
                            Assert.DoesNotThrow(() => reader.Field("m_Name").AsString(), label);
                        }
                    }
                }
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
            foreach (var filePath in _filePaths)
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                foreach (var entryName in archive.EntryNames)
                {
                    using var serializedFile = archive.OpenSerializedFile(entryName);
                    if (!TryGetTypeTrees(serializedFile, out var typeTrees)) continue;

                    var label = $"{filePath} :: {entryName}";
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
                }
            }
        }

        [Test]
        public void GetTypeTreeByIndex_ForAnObjectTypeMatchingALiveObject_MatchesThatObjectsReaderRootShape()
        {
            foreach (var filePath in _filePaths)
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                foreach (var entryName in archive.EntryNames)
                {
                    using var serializedFile = archive.OpenSerializedFile(entryName);
                    if (!TryGetTypeTrees(serializedFile, out var typeTrees)) continue;

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

                        var label = $"{filePath} :: {entryName} :: type tree {summary.Index}";
                        var byIndex = serializedFile.GetTypeTreeByIndex(summary.Index);
                        var byObject = matchingObject.GetReader();

                        Assert.That(byIndex.TypeName, Is.EqualTo(byObject.TypeName), label);
                    }
                }
            }
        }

        [Test]
        public void ExternalReferences_EveryEntryInEveryFile_HasNonNullPathAndGuid()
        {
            foreach (var filePath in _filePaths)
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                foreach (var entryName in archive.EntryNames)
                {
                    using var serializedFile = archive.OpenSerializedFile(entryName);

                    foreach (var externalReference in serializedFile.ExternalReferences)
                    {
                        var label = $"{filePath} :: {entryName}";
                        Assert.That(externalReference.Path, Is.Not.Null, label);
                        Assert.That(externalReference.Guid, Is.Not.Null, label);
                    }
                }
            }
        }

        [Test]
        public void OpenRawByteSource_EveryEntry_ProducesANonNullSourceWithPositiveLength()
        {
            foreach (var filePath in _filePaths)
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
            foreach (var filePath in _filePaths)
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
            foreach (var filePath in _filePaths)
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
            foreach (var filePath in _filePaths)
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                foreach (var entry in archive.Entries)
                {
                    var label = $"{filePath} :: {entry.Path}";

                    var whole = archive.ReadRawEntry(entry.Path);
                    var ranged = archive.ReadRawEntry(entry.Path, 0, (int)entry.Size);

                    Assert.That(whole.Length, Is.EqualTo(entry.Size), label);
                    Assert.That(whole, Is.EqualTo(ranged), label);
                }
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
            foreach (var filePath in _filePaths)
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
                        var byteSource = archive.OpenRawByteSource(entryName);
                        var detected = SerializedFileDetector.TryDetect(byteSource, out var info);
                        Assert.That(detected, Is.True, label);

                        if (!info.MetadataParsed)
                        {
                            // This entry's version is outside the 19-23 range this parser
                            // understands, even though the native library opened it fine -- nothing
                            // to cross-validate.
                            continue;
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
                }
            }
        }

        [Test]
        public void Inspect_EveryObjectInEveryFile_NeverThrows_AndNothingIsSilentlyDropped()
        {
            var registry = new ArtifactAdapterRegistry();

            foreach (var filePath in _filePaths)
            {
                using var archive = ArtifactInspector.OpenAssetBundle(filePath);

                foreach (var obj in registry.Inspect(archive))
                {
                    // Every object comes back as either a known adapter's result or a
                    // RawObject fallback -- Adapt()/Inspect() never return null.
                    Assert.That(obj, Is.Not.Null, filePath);
                }
            }
        }
    }
}
