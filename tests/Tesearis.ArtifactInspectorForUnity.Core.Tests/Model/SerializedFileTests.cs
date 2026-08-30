using System;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Handles;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Model
{
    [TestFixture]
    public class SerializedFileTests
    {
        private static SerializedFile Create(FakeUnityFileSystemApi api)
        {
            var handle = new SerializedFileHandle(api, api.NextHandle());
            var fileHandle = new FileHandle(api, api.NextHandle());
            return new SerializedFile(handle, fileHandle, new TypeTreeCache());
        }

        [Test]
        public void Dispose_CalledTwice_IsIdempotent()
        {
            var api = new FakeUnityFileSystemApi();
            var serializedFile = Create(api);

            serializedFile.Dispose();

            Assert.DoesNotThrow(() => serializedFile.Dispose());
            Assert.That(api.ClosedSerializedFileHandles, Has.Count.EqualTo(1));
            Assert.That(api.ClosedFileHandles, Has.Count.EqualTo(1));
        }

        [Test]
        public void Objects_AfterDispose_ThrowsObjectDisposedException()
        {
            var api = new FakeUnityFileSystemApi();
            var serializedFile = Create(api);
            serializedFile.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _ = serializedFile.Objects);
        }

        [Test]
        public void TryGetObject_KnownAndUnknownPathId()
        {
            var api = new FakeUnityFileSystemApi();
            api.Objects.Add(new ObjectInfo { Id = 42, Offset = 0, Size = 8, TypeId = 1 });
            var serializedFile = Create(api);

            Assert.That(serializedFile.TryGetObject(42, out var found), Is.True);
            Assert.That(found.PathId, Is.EqualTo(42));

            Assert.That(serializedFile.TryGetObject(999, out _), Is.False);
        }

        [Test]
        public void Version_IsLazilyFetchedAndCached()
        {
            var api = new FakeUnityFileSystemApi { SerializedFileVersion = 23 };
            var serializedFile = Create(api);

            Assert.That(api.GetSerializedFileVersionCallCount, Is.EqualTo(0));

            Assert.That(serializedFile.Version, Is.EqualTo(23));
            Assert.That(serializedFile.Version, Is.EqualTo(23));

            Assert.That(api.GetSerializedFileVersionCallCount, Is.EqualTo(1));
        }

        [Test]
        public void TypeTrees_IsLazilyFetchedAndCached()
        {
            var api = new FakeUnityFileSystemApi();
            api.TypeTreeInfos.Add(default);
            var serializedFile = Create(api);

            Assert.That(api.GetTypeTreeCountCallCount, Is.EqualTo(0));

            Assert.That(serializedFile.TypeTrees, Has.Count.EqualTo(1));
            Assert.That(serializedFile.TypeTrees, Has.Count.EqualTo(1));

            Assert.That(api.GetTypeTreeCountCallCount, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_InvokesOnDisposedCallbackExactlyOnce()
        {
            var api = new FakeUnityFileSystemApi();
            var serializedFile = Create(api);
            var callCount = 0;
            serializedFile.SetOwner(sf =>
            {
                callCount++;
                Assert.That(sf, Is.SameAs(serializedFile));
            });

            serializedFile.Dispose();
            serializedFile.Dispose(); // idempotent -- callback must not fire again

            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void Invalidate_DoesNotInvokeOnDisposedCallback_ButStillDisposes()
        {
            var api = new FakeUnityFileSystemApi();
            var serializedFile = Create(api);
            var invoked = false;
            serializedFile.SetOwner(_ => invoked = true);

            serializedFile.Invalidate();

            Assert.That(invoked, Is.False);
            Assert.Throws<ObjectDisposedException>(() => _ = serializedFile.Objects);
            Assert.That(api.ClosedSerializedFileHandles, Has.Count.EqualTo(1));
        }
    }
}
