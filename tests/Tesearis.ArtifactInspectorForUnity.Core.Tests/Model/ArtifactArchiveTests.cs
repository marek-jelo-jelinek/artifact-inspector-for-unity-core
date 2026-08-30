using System;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Handles;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Model
{
    [TestFixture]
    public class ArtifactArchiveTests
    {
        private static ArtifactArchive CreateArchive(FakeUnityFileSystemApi api, params (string Path, long Size, ArchiveNodeFlags Flags)[] nodes)
        {
            foreach (var node in nodes)
            {
                api.ArchiveNodes.Add(new ArchiveNode(node.Path, node.Size, node.Flags));
            }

            var archiveHandle = new ArchiveHandle(api, api.NextHandle());
            return new ArtifactArchive(api, archiveHandle, ArchiveMountPoint.NewMountPoint());
        }

        [Test]
        public void EntryNames_ExcludesNonSerializedFileAndDeletedAndDirectoryEntries()
        {
            var api = new FakeUnityFileSystemApi();
            var archive = CreateArchive(
                api,
                ("data.assets", 100, ArchiveNodeFlags.SerializedFile),
                ("data.resS", 200, ArchiveNodeFlags.None),
                ("deleted.assets", 50, ArchiveNodeFlags.SerializedFile | ArchiveNodeFlags.Deleted),
                ("dir", 0, ArchiveNodeFlags.Directory));

            Assert.That(archive.EntryNames, Is.EqualTo(new[] { "data.assets" }));
        }

        [Test]
        public void Entries_IncludesNonSerializedFileEntries_ButExcludesDeletedAndDirectoryEntries()
        {
            var api = new FakeUnityFileSystemApi();
            var archive = CreateArchive(
                api,
                ("data.assets", 100, ArchiveNodeFlags.SerializedFile),
                ("data.resS", 200, ArchiveNodeFlags.None),
                ("deleted.assets", 50, ArchiveNodeFlags.SerializedFile | ArchiveNodeFlags.Deleted),
                ("dir", 0, ArchiveNodeFlags.Directory));

            Assert.That(archive.Entries.Count, Is.EqualTo(2));
        }

        [Test]
        public void Dispose_CalledTwice_IsIdempotent()
        {
            var api = new FakeUnityFileSystemApi();
            var archive = CreateArchive(api);

            archive.Dispose();

            Assert.DoesNotThrow(() => archive.Dispose());
            Assert.That(api.UnmountedArchiveHandles, Has.Count.EqualTo(1));
        }

        [Test]
        public void EntryNames_AfterDispose_ThrowsObjectDisposedException()
        {
            var api = new FakeUnityFileSystemApi();
            var archive = CreateArchive(api);
            archive.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _ = archive.EntryNames);
        }

        [Test]
        public void OpenSerializedFile_NullEntryName_ThrowsArgumentNullException()
        {
            var api = new FakeUnityFileSystemApi();
            var archive = CreateArchive(api);

            Assert.Throws<ArgumentNullException>(() => archive.OpenSerializedFile(null));
        }

        [Test]
        public void OpenSerializedFile_WhenOpeningFileHandleFails_DisposesSerializedFileHandleAndRethrows()
        {
            var api = new FakeUnityFileSystemApi();
            var archive = CreateArchive(api, ("entry", 10, ArchiveNodeFlags.SerializedFile));
            var thrown = new InvalidOperationException("boom");
            api.OpenFileOverride = _ => throw thrown;

            var ex = Assert.Throws<InvalidOperationException>(() => archive.OpenSerializedFile("entry"));

            Assert.That(ex, Is.SameAs(thrown));
            // The SerializedFileHandle opened just before the FileHandle open failed must have
            // been cleaned up rather than leaked.
            Assert.That(api.ClosedSerializedFileHandles, Has.Count.EqualTo(1));
        }

        [Test]
        public void Dispose_WithOutstandingSerializedFile_InvalidatesIt()
        {
            var api = new FakeUnityFileSystemApi();
            var archive = CreateArchive(api, ("entry", 10, ArchiveNodeFlags.SerializedFile));
            var serializedFile = archive.OpenSerializedFile("entry");

            archive.Dispose();

            // Disposing the archive while a SerializedFile it produced is still open must not
            // leave that SerializedFile looking usable -- it should fail clearly instead of
            // making native calls against a mount that no longer exists.
            Assert.Throws<ObjectDisposedException>(() => _ = serializedFile.Objects);

            // The cascaded invalidation must itself be a real (idempotent-safe) disposal.
            Assert.DoesNotThrow(() => serializedFile.Dispose());
            Assert.That(api.ClosedSerializedFileHandles, Has.Count.EqualTo(1));
        }

        [Test]
        public void OpenSerializedFile_TwoArchivesWithTheSameEntryName_UseDistinctVirtualPaths()
        {
            // Two ArtifactArchives must not collide on the same virtual mount point -- otherwise
            // opening the same entry name from two different archives would resolve against
            // whichever archive happened to mount there, silently reading the wrong file.
            var apiA = new FakeUnityFileSystemApi();
            string capturedPathA = null;
            apiA.OpenSerializedFileOverride = path =>
            {
                capturedPathA = path;
                return apiA.NextHandle();
            };
            var archiveA = CreateArchive(apiA, ("entry", 10, ArchiveNodeFlags.SerializedFile));

            var apiB = new FakeUnityFileSystemApi();
            string capturedPathB = null;
            apiB.OpenSerializedFileOverride = path =>
            {
                capturedPathB = path;
                return apiB.NextHandle();
            };
            var archiveB = CreateArchive(apiB, ("entry", 10, ArchiveNodeFlags.SerializedFile));

            archiveA.OpenSerializedFile("entry");
            archiveB.OpenSerializedFile("entry");

            Assert.That(capturedPathA, Is.Not.Null);
            Assert.That(capturedPathB, Is.Not.Null);
            Assert.That(capturedPathA, Is.Not.EqualTo(capturedPathB));
        }

        [Test]
        public void Dispose_AfterSerializedFileAlreadyDisposedItself_DoesNotThrow()
        {
            var api = new FakeUnityFileSystemApi();
            var archive = CreateArchive(api, ("entry", 10, ArchiveNodeFlags.SerializedFile));
            var serializedFile = archive.OpenSerializedFile("entry");
            serializedFile.Dispose();

            Assert.DoesNotThrow(() => archive.Dispose());
            // Not double-closed by the archive's own cascading invalidation on top of the
            // SerializedFile's own normal disposal.
            Assert.That(api.ClosedSerializedFileHandles, Has.Count.EqualTo(1));
        }
    }
}
