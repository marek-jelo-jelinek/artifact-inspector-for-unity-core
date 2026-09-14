using System.Collections.Generic;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Handles;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Model
{
    /// <summary>
    /// Covers <see cref="ObjectRef.Snapshot"/>/<see cref="SerializedFile.TryGetSnapshot"/> -- the fix for
    /// repeatedly re-walking the same object's type tree from scratch (formerly prototyped, narrowly, by the
    /// now-removed CachedFieldResolver). Every object here shares one GameObject-shaped tree (m_Component, an
    /// array, before m_Name, a string, before m_Target, a PPtr) since FakeUnityFileSystemApi's
    /// GetTypeTreeNodeInfoOverride isn't per-object, only per-node-index.
    /// </summary>
    [TestFixture]
    public class ObjectSnapshotTests
    {
        private static readonly Dictionary<int, TypeTreeNodeInfo> NodesByIndex = new()
        {
            [0] = new TypeTreeNodeInfo("GameObject", "Base", 0, -1, TypeTreeFlags.None, TypeTreeMetaFlags.None, firstChildNode: 1, nextNode: 0),
            [1] = new TypeTreeNodeInfo("vector", "m_Component", 0, -1, TypeTreeFlags.None, TypeTreeMetaFlags.None, firstChildNode: 2, nextNode: 6),
            [2] = new TypeTreeNodeInfo("Array", "Array", 0, -1, TypeTreeFlags.IsArray, TypeTreeMetaFlags.None, firstChildNode: 3, nextNode: 0),
            [3] = new TypeTreeNodeInfo("int", "size", 0, 4, TypeTreeFlags.None, TypeTreeMetaFlags.None, firstChildNode: 0, nextNode: 4),
            [4] = new TypeTreeNodeInfo("int", "data", 0, 4, TypeTreeFlags.None, TypeTreeMetaFlags.None, firstChildNode: 0, nextNode: 0),
            [6] = new TypeTreeNodeInfo("string", "m_Name", 0, -1, TypeTreeFlags.None, TypeTreeMetaFlags.None, firstChildNode: 7, nextNode: 11),
            [7] = new TypeTreeNodeInfo("Array", "Array", 0, -1, TypeTreeFlags.IsArray, TypeTreeMetaFlags.None, firstChildNode: 8, nextNode: 0),
            [8] = new TypeTreeNodeInfo("int", "size", 0, 4, TypeTreeFlags.None, TypeTreeMetaFlags.None, firstChildNode: 0, nextNode: 9),
            [9] = new TypeTreeNodeInfo("UInt8", "data", 0, 1, TypeTreeFlags.None, TypeTreeMetaFlags.None, firstChildNode: 0, nextNode: 0),
            [11] = new TypeTreeNodeInfo("PPtr<GameObject>", "m_Target", 0, -1, TypeTreeFlags.None, TypeTreeMetaFlags.None, firstChildNode: 12, nextNode: 0),
            [12] = new TypeTreeNodeInfo("int", "m_FileID", 0, 4, TypeTreeFlags.None, TypeTreeMetaFlags.None, firstChildNode: 0, nextNode: 13),
            [13] = new TypeTreeNodeInfo("SInt64", "m_PathID", 0, 8, TypeTreeFlags.None, TypeTreeMetaFlags.None, firstChildNode: 0, nextNode: 0),
        };

        private static byte[] WriteObject(int[] components, string name, int targetFileId, long targetPathId)
        {
            var writer = new ByteBufferWriter().WriteInt32(components.Length);
            foreach (var value in components) writer.WriteInt32(value);
            writer.WriteString(name);
            writer.WriteInt32(targetFileId).WriteInt64(targetPathId);
            return writer.ToArray();
        }

        private static FakeUnityFileSystemApi BuildApi(params (long pathId, int[] components, string name, int targetFileId, long targetPathId)[] objects)
        {
            var api = new FakeUnityFileSystemApi { GetTypeTreeNodeInfoOverride = i => NodesByIndex[i] };
            var content = new List<byte>();
            foreach (var (pathId, components, name, targetFileId, targetPathId) in objects)
            {
                var bytes = WriteObject(components, name, targetFileId, targetPathId);
                api.Objects.Add(new ObjectInfo { Id = pathId, Offset = content.Count, Size = bytes.Length, TypeId = 1 });
                content.AddRange(bytes);
            }

            api.Content = content.ToArray();
            return api;
        }

        private static SerializedFile Create(FakeUnityFileSystemApi api)
        {
            var handle = new SerializedFileHandle(api, api.NextHandle());
            var fileHandle = new FileHandle(api, api.NextHandle());
            return new SerializedFile(handle, fileHandle, new TypeTreeCache());
        }

        [Test]
        public void Snapshot_RepeatedLookupsOfSameObject_ReadTheFileOnce()
        {
            var api = BuildApi((1, new[] { 10, 20 }, "GameObjectA", 0, 0));
            var serializedFile = Create(api);
            var objectRef = serializedFile.Objects[0];

            var first = objectRef.Snapshot().Field("m_Name").AsString();
            var callsAfterFirst = api.ReadFileCallCount;
            Assert.That(first, Is.EqualTo("GameObjectA"));
            Assert.That(callsAfterFirst, Is.GreaterThan(0));

            for (var i = 0; i < 5; i++)
            {
                Assert.That(objectRef.Snapshot().Field("m_Name").AsString(), Is.EqualTo("GameObjectA"));
            }

            Assert.That(api.ReadFileCallCount, Is.EqualTo(callsAfterFirst),
                "repeated lookups of an already-cached object -- e.g. from several sibling components -- must not read the file again");
        }

        [Test]
        public void Snapshot_DistinctPathIds_ResolveIndependently()
        {
            var api = BuildApi(
                (1, new[] { 1 }, "A", 0, 0),
                (2, new[] { 1, 2 }, "B", 0, 0));
            var serializedFile = Create(api);

            Assert.That(serializedFile.Objects[0].Snapshot().Field("m_Name").AsString(), Is.EqualTo("A"));
            Assert.That(serializedFile.Objects[1].Snapshot().Field("m_Name").AsString(), Is.EqualTo("B"));
        }

        [Test]
        public void TryGetSnapshot_UnknownPathId_ReturnsFalseWithoutThrowing()
        {
            var api = BuildApi((1, new[] { 1 }, "A", 0, 0));
            var serializedFile = Create(api);

            Assert.That(serializedFile.TryGetSnapshot(999, out _), Is.False);
        }

        [Test]
        public void TryResolveSnapshot_PPtrChase_WarmedTargetCostsNoFurtherReads()
        {
            var api = BuildApi(
                (1, new[] { 1 }, "A", 0, 2),
                (2, new[] { 1, 2 }, "B", 0, 0));
            var serializedFile = Create(api);

            var a = serializedFile.Objects[0];
            var target = a.Snapshot().Field("m_Target").AsPPtr();
            Assert.That(target.PathId, Is.EqualTo(2));

            Assert.That(target.TryResolveSnapshot(serializedFile, out var bSnapshot), Is.True);
            Assert.That(bSnapshot.Field("m_Name").AsString(), Is.EqualTo("B"));

            var callsAfterWarming = api.ReadFileCallCount;

            // Chasing the same PPtr again -- e.g. from a different sibling component -- must not touch the
            // byte source at all. This is the direct regression test for the shared-buffer thrash risk
            // PERFORMANCE_IMPROVEMENTS.md flags: a cached lookup never chases a live cursor into another object.
            Assert.That(target.TryResolveSnapshot(serializedFile, out var bAgain), Is.True);
            Assert.That(bAgain.Field("m_Name").AsString(), Is.EqualTo("B"));
            Assert.That(api.ReadFileCallCount, Is.EqualTo(callsAfterWarming));
        }

        [Test]
        public void TryResolveSnapshot_CrossFileReference_ReturnsFalse()
        {
            var api = BuildApi((1, new[] { 1 }, "A", 2, 5)); // m_FileID = 2 -> not local
            var serializedFile = Create(api);

            var target = serializedFile.Objects[0].Snapshot().Field("m_Target").AsPPtr();

            Assert.That(target.IsLocal, Is.False);
            Assert.That(target.TryResolveSnapshot(serializedFile, out _), Is.False);
        }
    }
}
