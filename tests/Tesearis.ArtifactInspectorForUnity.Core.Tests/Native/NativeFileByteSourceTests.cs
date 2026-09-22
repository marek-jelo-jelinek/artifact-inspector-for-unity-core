using System;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Handles;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Native
{
    [TestFixture]
    public class NativeFileByteSourceTests
    {
        [Test]
        public void Constructor_NullFileHandle_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new NativeFileByteSource(null));
        }

        [Test]
        public void Read_NullBuffer_ThrowsArgumentNullException()
        {
            var api = new FakeUnityFileSystemApi();
            var handle = new FileHandle(api, api.NextHandle());
            var source = new NativeFileByteSource(handle);

            Assert.Throws<ArgumentNullException>(() => source.Read(0, null, 0, 4));
        }

        [Test]
        public void Read_NegativeOffset_ThrowsArgumentOutOfRangeException()
        {
            var api = new FakeUnityFileSystemApi();
            var handle = new FileHandle(api, api.NextHandle());
            var source = new NativeFileByteSource(handle);

            Assert.Throws<ArgumentOutOfRangeException>(() => source.Read(0, new byte[10], -1, 4));
        }

        [Test]
        public void Read_NegativeCount_ThrowsArgumentOutOfRangeException()
        {
            var api = new FakeUnityFileSystemApi();
            var handle = new FileHandle(api, api.NextHandle());
            var source = new NativeFileByteSource(handle);

            Assert.Throws<ArgumentOutOfRangeException>(() => source.Read(0, new byte[10], 0, -1));
        }

        [Test]
        public void Read_BufferTooSmall_ThrowsArgumentException()
        {
            var api = new FakeUnityFileSystemApi();
            var handle = new FileHandle(api, api.NextHandle());
            var source = new NativeFileByteSource(handle);

            Assert.Throws<ArgumentException>(() => source.Read(0, new byte[10], 5, 6));
        }

        [Test]
        public void Read_CountZero_ReturnsZeroWithoutNativeCall()
        {
            var api = new FakeUnityFileSystemApi();
            var handle = new FileHandle(api, api.NextHandle());
            var source = new NativeFileByteSource(handle);

            var read = source.Read(0, new byte[10], 0, 0);

            Assert.That(read, Is.EqualTo(0));
        }
    }
}
