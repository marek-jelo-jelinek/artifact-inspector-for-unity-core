using System;
using Tesearis.ArtifactInspectorForUnity.Core.Adapters;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Adapters
{
    [TestFixture]
    public class ArtifactAdapterContextTests
    {
        [Test]
        public void Constructor_NullReader_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ArtifactAdapterContext(default(ObjectRef), null, null, null));
        }

        [Test]
        public void Snapshot_NullSerializedFile_ThrowsInvalidOperationException()
        {
            var context = new ArtifactAdapterContext(
                default(ObjectRef),
                new Core.TypeTree.TypeTreeReader(FakeTypeTreeBuilder.Int32("Test"), new InMemoryByteSource([0, 0, 0, 0]), 0),
                null,
                null);

            var ex = Assert.Throws<InvalidOperationException>(() => context.Snapshot());
            Assert.That(ex.Message, Does.Contain("SerializedFile"));
        }
    }
}
