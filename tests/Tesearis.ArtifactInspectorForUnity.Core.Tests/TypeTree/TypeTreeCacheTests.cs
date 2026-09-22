using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Handles;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TypeTree
{
    /// <summary>
    /// Covers <see cref="TypeTreeCache"/>'s recursive native-tree walk against a fake
    /// <see cref="IUnityFileSystemApi"/>, in particular the depth guard that turns a corrupt or
    /// self-referential type tree into an <see cref="ArtifactInspectorException"/> instead of an
    /// uncatchable stack overflow.
    /// </summary>
    [TestFixture]
    public class TypeTreeCacheTests
    {
        // An arbitrary non-MonoBehaviour ClassID (GameObject), used throughout to exercise the
        // shared-across-instances cache path. 114 (MonoBehaviour) is the one ClassID that must NOT
        // share -- see the dedicated MonoBehaviour tests below.
        private const int GameObjectTypeId = 1;
        private const int MonoBehaviourTypeId = 114;

        [Test]
        public void GetOrBuild_ModeratelyNestedChain_BuildsWithoutThrowing()
        {
            var (handle, api) = BuildChain(chainLength: 10);

            var root = new TypeTreeCache().GetOrBuild(handle, objectId: 1, typeId: GameObjectTypeId);

            var depth = 0;
            var node = root;
            while (node.Children.Count > 0)
            {
                node = node.Children[0];
                depth++;
            }

            Assert.That(depth, Is.EqualTo(10));
        }

        [Test]
        public void GetOrBuild_ChainNestingBeyondMaxDepth_ThrowsInsteadOfOverflowingTheStack()
        {
            var (handle, _) = BuildChain(chainLength: 200);

            Assert.Throws<ArtifactInspectorException>(() =>
                new TypeTreeCache().GetOrBuild(handle, objectId: 1, typeId: GameObjectTypeId));
        }

        [Test]
        public void GetOrBuild_ManyObjectsSameNonMonoBehaviourTypeId_WalksNativeTypeTreeOnce()
        {
            var (handle, api) = BuildChain(chainLength: 3);
            var cache = new TypeTreeCache();

            TypeTreeNode first = null;
            for (long objectId = 0; objectId < 5000; objectId++)
            {
                var node = cache.GetOrBuild(handle, objectId, GameObjectTypeId);
                first ??= node;
                Assert.That(node, Is.SameAs(first), "every same-TypeId object should share the cached node instance");
            }

            Assert.That(api.GetTypeTreeCallCount, Is.EqualTo(1));
        }

        [Test]
        public void GetOrBuild_ManyMonoBehaviourObjects_WalksNativeTypeTreeOncePerObject()
        {
            var (handle, api) = BuildChain(chainLength: 3);
            var cache = new TypeTreeCache();

            const int objectCount = 500;
            for (long objectId = 0; objectId < objectCount; objectId++)
            {
                cache.GetOrBuild(handle, objectId, MonoBehaviourTypeId);
            }

            // Regression guard: MonoBehaviour (schema varies per script) must NOT be shared across
            // instances the way other ClassIDs are -- this is the correctness-sensitive opt-out.
            Assert.That(api.GetTypeTreeCallCount, Is.EqualTo(objectCount));
        }

        [Test]
        public void GetOrBuild_TwoMonoBehaviourInstancesWithDifferentSchemas_AreNotCacheCollided()
        {
            var api = new FakeUnityFileSystemApi();
            api.GetTypeTreeNodeInfoByHandleOverride = (typeTreeHandle, nodeIndex) =>
            {
                var objectId = api.TypeTreeHandleToObjectId[typeTreeHandle];
                var typeName = objectId == 1 ? "ScriptA" : "ScriptB";
                return new TypeTreeNodeInfo(typeName, "base", 0, -1, TypeTreeFlags.None, TypeTreeMetaFlags.None,
                    firstChildNode: 0, nextNode: 0);
            };
            var handle = new SerializedFileHandle(api, api.NextHandle());
            var cache = new TypeTreeCache();

            var scriptA = cache.GetOrBuild(handle, objectId: 1, MonoBehaviourTypeId);
            var scriptB = cache.GetOrBuild(handle, objectId: 2, MonoBehaviourTypeId);

            Assert.That(scriptA.TypeName, Is.EqualTo("ScriptA"));
            Assert.That(scriptB.TypeName, Is.EqualTo("ScriptB"));
            Assert.That(scriptA, Is.Not.SameAs(scriptB));
        }

        [Test]
        public void GetOrBuild_NegativeTypeId_WalksNativeTypeTreeOncePerObject()
        {
            var (handle, api) = BuildChain(chainLength: 3);
            var cache = new TypeTreeCache();

            const int objectCount = 10;
            const int undefinedTypeId = -1;
            for (long objectId = 0; objectId < objectCount; objectId++)
            {
                cache.GetOrBuild(handle, objectId, undefinedTypeId);
            }

            Assert.That(api.GetTypeTreeCallCount, Is.EqualTo(objectCount));
        }

        /// <summary>Builds a fake type tree that's a straight chain of chainLength nested struct wrappers around one leaf int.</summary>
        private static (SerializedFileHandle handle, FakeUnityFileSystemApi api) BuildChain(int chainLength)
        {
            var api = new FakeUnityFileSystemApi();
            api.GetTypeTreeNodeInfoOverride = index => index >= chainLength
                ? new TypeTreeNodeInfo("int", "leaf", 0, 4, TypeTreeFlags.None, TypeTreeMetaFlags.None, firstChildNode: 0, nextNode: 0)
                : new TypeTreeNodeInfo("Wrapper", "field", 0, -1, TypeTreeFlags.None, TypeTreeMetaFlags.None, firstChildNode: index + 1, nextNode: 0);

            var handle = new SerializedFileHandle(api, api.NextHandle());
            return (handle, api);
        }
    }
}
