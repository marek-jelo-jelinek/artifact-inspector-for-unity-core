using System;
using Tesearis.ArtifactInspectorForUnity.Core.Adapters;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Handles;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Adapters
{
    [TestFixture]
    public class StreamingInfoTests
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
        public void Constructor_ExposesOffsetSizeAndPath()
        {
            var info = new StreamingInfo(1234L, 5678L, "archive:/CAB-abc.resS");

            Assert.That(info.Offset, Is.EqualTo(1234L));
            Assert.That(info.Size, Is.EqualTo(5678L));
            Assert.That(info.Path, Is.EqualTo("archive:/CAB-abc.resS"));
        }

        [Test]
        public void ReadBytes_ReturnsExpectedByteRangeFromArchive()
        {
            var api = new FakeUnityFileSystemApi { Content = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 } };
            var archive = CreateArchive(api, ("entry", 10, ArchiveNodeFlags.SerializedFile));
            var info = new StreamingInfo(2, 4, "entry");

            Assert.That(info.ReadBytes(archive), Is.EqualTo(new byte[] { 2, 3, 4, 5 }));
        }

        [Test]
        public void ReadBytes_ZeroSize_ReturnsEmptyArray()
        {
            var api = new FakeUnityFileSystemApi { Content = new byte[] { 0, 1, 2, 3, 4 } };
            var archive = CreateArchive(api, ("entry", 5, ArchiveNodeFlags.SerializedFile));
            var info = new StreamingInfo(0, 0, "entry");

            Assert.That(info.ReadBytes(archive), Is.Empty);
        }

        [Test]
        public void ReadBytes_SizeExceedsIntMaxValue_ThrowsOverflowException()
        {
            var api = new FakeUnityFileSystemApi { Content = new byte[] { 0, 1, 2, 3, 4 } };
            var archive = CreateArchive(api, ("entry", 5, ArchiveNodeFlags.SerializedFile));
            var info = new StreamingInfo(0, (long)int.MaxValue + 1, "entry");

            Assert.Throws<OverflowException>(() => info.ReadBytes(archive));
        }

        [Test]
        public void OpenByteSource_ReturnsSourceWithMatchingLengthAndReadableRange()
        {
            var api = new FakeUnityFileSystemApi { Content = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 } };
            var archive = CreateArchive(api, ("entry", 10, ArchiveNodeFlags.SerializedFile));
            var info = new StreamingInfo(2, 4, "entry");

            var source = info.OpenByteSource(archive);
            Assert.That(source.Length, Is.EqualTo(10));

            var buffer = new byte[info.Size];
            source.Read(info.Offset, buffer, 0, (int)info.Size);
            Assert.That(buffer, Is.EqualTo(new byte[] { 2, 3, 4, 5 }));
        }
    }
}
