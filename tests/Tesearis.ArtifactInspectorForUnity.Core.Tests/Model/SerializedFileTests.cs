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

        [Test]
        public void MaterializeAll_WithTypeIdFilter_OnlySnapshotsMatchingObjects()
        {
            // TypeId 2's object sits more than BufferedByteSource's 64 KiB window away from the TypeId 1
            // objects, so reading it later can't be coincidentally satisfied by a window a filtered-out
            // MaterializeAll pass never had reason to fetch.
            var api = new FakeUnityFileSystemApi { Content = new byte[100004] };
            api.Objects.Add(new ObjectInfo { Id = 1, Offset = 0, Size = 4, TypeId = 1 });
            api.Objects.Add(new ObjectInfo { Id = 2, Offset = 100000, Size = 4, TypeId = 2 });
            api.Objects.Add(new ObjectInfo { Id = 3, Offset = 8, Size = 4, TypeId = 1 });
            var serializedFile = Create(api);

            var result = serializedFile.MaterializeAll(new MaterializeOptions { TypeIdFilter = typeId => typeId == 1 });

            Assert.That(result.SucceededCount, Is.EqualTo(2));

            var readsAfterMaterialize = api.ReadFileCallCount;
            Assert.That(serializedFile.TryGetSnapshot(1, out _), Is.True);
            Assert.That(api.ReadFileCallCount, Is.EqualTo(readsAfterMaterialize), "TypeId 1 was already materialized by the filtered pass");

            Assert.That(serializedFile.TryGetSnapshot(2, out _), Is.True);
            Assert.That(api.ReadFileCallCount, Is.GreaterThan(readsAfterMaterialize), "TypeId 2 was excluded by the filter, so it wasn't cached yet");
        }

        [Test]
        public void MaterializeAll_ProcessesObjectsInAscendingByteOffsetOrder_RegardlessOfInsertionOrder()
        {
            var api = new FakeUnityFileSystemApi { Content = new byte[300000] };

            // Inserted out of offset order (highest first) -- MaterializeAll must sort by ByteOffset itself
            // rather than trust native/insertion order, which isn't documented or guaranteed to be sorted.
            api.Objects.Add(new ObjectInfo { Id = 3, Offset = 200000, Size = 4, TypeId = 1 });
            api.Objects.Add(new ObjectInfo { Id = 1, Offset = 0, Size = 4, TypeId = 1 });
            api.Objects.Add(new ObjectInfo { Id = 2, Offset = 100000, Size = 4, TypeId = 1 });
            var serializedFile = Create(api);

            serializedFile.MaterializeAll();

            // Each object sits more than BufferedByteSource's 64 KiB window apart, so reading it forces a
            // fresh native seek -- the recorded seek order reveals visitation order.
            var seeksAtObjectOffsets = api.SeekOffsets.FindAll(o => o == 0 || o == 100000 || o == 200000);
            Assert.That(seeksAtObjectOffsets, Is.EqualTo(new long[] { 0, 100000, 200000 }));
        }

        [Test]
        public void MaterializeAll_ObjectWithUnsupportedManagedReferenceShape_RecordsFailureWithoutAbortingTheRest()
        {
            var api = new FakeUnityFileSystemApi
            {
                Content = new byte[] { 1, 0, 0, 0 },
                GetTypeTreeNodeInfoOverride = _ => new TypeTreeNodeInfo(
                    "managedReference", "v", 0, -1, TypeTreeFlags.IsManagedReference, TypeTreeMetaFlags.None, firstChildNode: 0, nextNode: 0),
            };
            api.Objects.Add(new ObjectInfo { Id = 1, Offset = 0, Size = 4, TypeId = 1 });
            var serializedFile = Create(api);

            var result = serializedFile.MaterializeAll();

            Assert.That(result.SucceededCount, Is.EqualTo(0));
            Assert.That(result.Failures, Has.Count.EqualTo(1));
            Assert.That(result.Failures[0].PathId, Is.EqualTo(1));
            Assert.That(result.Failures[0].Error, Is.InstanceOf<UnsupportedManagedReferenceShapeException>());
        }
    }
}
