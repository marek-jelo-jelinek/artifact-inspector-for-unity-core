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
        [Test]
        public void GetOrBuild_ModeratelyNestedChain_BuildsWithoutThrowing()
        {
            var (handle, api) = BuildChain(chainLength: 10);

            var root = new TypeTreeCache().GetOrBuild(handle, objectId: 1);

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

            Assert.Throws<ArtifactInspectorException>(() => new TypeTreeCache().GetOrBuild(handle, objectId: 1));
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
