using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TypeTree
{
    /// <summary>
    /// Covers <see cref="TypeTreeNode"/>'s container unwrapping: both a
    /// "vector"-wrapped array (the shape real Unity type trees actually use for
    /// array-valued struct fields) and a bare "Array" node with no wrapper (the
    /// more defensive shape, tolerated identically).
    /// </summary>
    [TestFixture]
    public class ContainerUnwrapTests
    {
        [Test]
        public void VectorWrappedArray_UnwrapsToArrayLikeWithCorrectElements()
        {
            var vectorNode = FakeTypeTreeBuilder.Vector("m_Items", FakeTypeTreeBuilder.Int32("data"));

            var buffer = new ByteBufferWriter()
                .WriteInt32(3)
                .WriteInt32(10)
                .WriteInt32(20)
                .WriteInt32(30)
                .ToArray();

            var reader = new TypeTreeReader(vectorNode, new InMemoryByteSource(buffer), 0);

            Assert.That(vectorNode.IsArrayLike, Is.True);
            Assert.That(reader.ArrayLength(), Is.EqualTo(3));
            Assert.That(reader.Element(0).AsInt32(), Is.EqualTo(10));
            Assert.That(reader.Element(1).AsInt32(), Is.EqualTo(20));
            Assert.That(reader.Element(2).AsInt32(), Is.EqualTo(30));
        }

        [Test]
        public void BareArrayNode_WithNoWrapper_IsToleratedIdentically()
        {
            var arrayNode = FakeTypeTreeBuilder.Array(FakeTypeTreeBuilder.Int32("data"));

            var buffer = new ByteBufferWriter()
                .WriteInt32(2)
                .WriteInt32(7)
                .WriteInt32(8)
                .ToArray();

            var reader = new TypeTreeReader(arrayNode, new InMemoryByteSource(buffer), 0);

            Assert.That(arrayNode.IsArrayLike, Is.True);
            Assert.That(reader.ArrayLength(), Is.EqualTo(2));
            Assert.That(reader.Element(0).AsInt32(), Is.EqualTo(7));
            Assert.That(reader.Element(1).AsInt32(), Is.EqualTo(8));
        }

        [Test]
        public void ArrayLikeNode_MissingNativeIsArrayFlag_ThrowsOnConstruction()
        {
            // FakeTypeTreeBuilder.Struct always sets TypeTreeFlags.None, so a node
            // named "Array" built through it looks array-like by name but is missing
            // the flag a real Unity type tree would always set alongside it --
            // simulating a malformed/unexpected type tree.
            Assert.Throws<ArtifactInspectorException>(() =>
                FakeTypeTreeBuilder.Struct("m_Items", "Array", FakeTypeTreeBuilder.Int32("size"), FakeTypeTreeBuilder.Int32("data")));
        }
    }
}