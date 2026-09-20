using System.Collections.Generic;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TypeTree
{
    /// <summary>
    /// Exercises <see cref="TypeTreeOffsetWalker"/> directly against hand-built
    /// synthetic type trees and in-memory buffers - no native call or real Unity
    /// Editor process involved. End-to-end validation against a real AssetBundle
    /// happens manually inside a real Unity Editor later.
    /// </summary>
    [TestFixture]
    public class TypeTreeOffsetWalkerTests
    {
        [Test]
        public void GetChildByIndex_FixedSizeSequentialFields_ComputesSequentialOffsets()
        {
            var structNode = FakeTypeTreeBuilder.Struct(
                "Base", "Triple",
                FakeTypeTreeBuilder.Int32("a"),
                FakeTypeTreeBuilder.Int32("b"),
                FakeTypeTreeBuilder.Int32("c"));

            var buffer = new byte[12];
            var byteSource = new InMemoryByteSource(buffer);
            var reader = new TypeTreeReader(structNode, byteSource, 0);

            var a = reader.Field("a");
            var b = reader.Field("b");
            var c = reader.Field("c");

            Assert.That((a.ByteOffset, a.ByteSize), Is.EqualTo((0L, 4L)));
            Assert.That((b.ByteOffset, b.ByteSize), Is.EqualTo((4L, 4L)));
            Assert.That((c.ByteOffset, c.ByteSize), Is.EqualTo((8L, 4L)));
        }

        [Test]
        public void ComputeSize_NestedStruct_RecursesIntoChildOffsets()
        {
            var innerStruct = FakeTypeTreeBuilder.Struct(
                "inner", "Inner",
                FakeTypeTreeBuilder.Int32("b"),
                FakeTypeTreeBuilder.Int32("c"));

            var outerStruct = FakeTypeTreeBuilder.Struct(
                "Base", "Outer",
                FakeTypeTreeBuilder.Int32("a"),
                innerStruct,
                FakeTypeTreeBuilder.Int32("d"));

            var buffer = new byte[16];
            var byteSource = new InMemoryByteSource(buffer);
            var reader = new TypeTreeReader(outerStruct, byteSource, 0);

            var totalSize = TypeTreeOffsetWalker.ComputeSize(outerStruct, 0, byteSource);
            var inner = reader.Field("inner");
            var d = reader.Field("d");

            Assert.That(totalSize, Is.EqualTo(16L)); // a(4) + inner(8) + d(4)
            Assert.That((inner.ByteOffset, inner.ByteSize), Is.EqualTo((4L, 8L)));
            Assert.That((d.ByteOffset, d.ByteSize), Is.EqualTo((12L, 4L)));
        }

        [Test]
        public void GetChildByIndex_VariableSizeStringField_ShiftsSubsequentSiblingOffset()
        {
            var structNode = FakeTypeTreeBuilder.Struct(
                "Base", "WithString",
                FakeTypeTreeBuilder.String("s"),
                FakeTypeTreeBuilder.Int32("next"));

            var buffer = new ByteBufferWriter()
                .WriteString("hello") // 4-byte length prefix + 5 bytes = 9 bytes total
                .WriteInt32(42)
                .ToArray();
            var byteSource = new InMemoryByteSource(buffer);
            var reader = new TypeTreeReader(structNode, byteSource, 0);

            var s = reader.Field("s");
            var next = reader.Field("next");

            Assert.That((s.ByteOffset, s.ByteSize), Is.EqualTo((0L, 9L)));
            Assert.That((next.ByteOffset, next.ByteSize), Is.EqualTo((9L, 4L)));
        }

        [Test]
        public void ComputeSize_ArrayWithNegativeLengthPrefix_Throws()
        {
            var arrayNode = FakeTypeTreeBuilder.Array(FakeTypeTreeBuilder.Int32("data"));
            var buffer = new ByteBufferWriter().WriteInt32(-1).ToArray();
            var byteSource = new InMemoryByteSource(buffer);

            Assert.Throws<ArtifactInspectorException>(() => TypeTreeOffsetWalker.ComputeSize(arrayNode, 0, byteSource));
        }

        [Test]
        public void ComputeSize_StringWithNegativeLengthPrefix_Throws()
        {
            var stringNode = FakeTypeTreeBuilder.String("s");
            var buffer = new ByteBufferWriter().WriteInt32(-1).ToArray();
            var byteSource = new InMemoryByteSource(buffer);

            Assert.Throws<ArtifactInspectorException>(() => TypeTreeOffsetWalker.ComputeSize(stringNode, 0, byteSource));
        }

        [Test]
        public void ComputeSize_ArrayNodeMissingElementTemplateChild_Throws()
        {
            // Bypasses FakeTypeTreeBuilder.Array (which always produces a well-formed
            // [size, data] pair) to simulate a malformed type tree with only a size child.
            var malformedArrayNode = new TypeTreeNode(
                "Array", "Array", -1, TypeTreeFlags.IsArray, TypeTreeMetaFlags.None,
                new List<TypeTreeNode> { FakeTypeTreeBuilder.Int32("size") });

            var buffer = new ByteBufferWriter().WriteInt32(0).ToArray();
            var byteSource = new InMemoryByteSource(buffer);

            Assert.Throws<ArtifactInspectorException>(() => TypeTreeOffsetWalker.ComputeSize(malformedArrayNode, 0, byteSource));
        }

        [Test]
        public void ComputeSize_TypelessDataField_ReadsRealLengthPrefixInsteadOfFixedFiveBytes()
        {
            var node = FakeTypeTreeBuilder.TypelessData("image data");
            var buffer = new ByteBufferWriter().WriteInt32(6).WriteZeros(6).ToArray();
            var byteSource = new InMemoryByteSource(buffer);

            var size = TypeTreeOffsetWalker.ComputeSize(node, 0, byteSource);

            Assert.That(size, Is.EqualTo(10L), "4-byte length prefix + 6 real data bytes, not the fixed 4+1=5");
        }

        [Test]
        public void GetChildByIndex_TypelessDataFieldFollowedByAnotherField_SiblingStartsAfterRealPayload()
        {
            var structNode = FakeTypeTreeBuilder.Struct(
                "Texture2D", "Texture2D",
                FakeTypeTreeBuilder.TypelessData("image data"),
                FakeTypeTreeBuilder.Int32("m_Width"));

            var buffer = new ByteBufferWriter()
                .WriteInt32(3).WriteByte(1).WriteByte(2).WriteByte(3) // image data: 3-byte payload
                .WriteInt32(256) // m_Width
                .ToArray();
            var byteSource = new InMemoryByteSource(buffer);
            var reader = new TypeTreeReader(structNode, byteSource, 0);

            var imageData = reader.Field("image data");
            var width = reader.Field("m_Width");

            Assert.That((imageData.ByteOffset, imageData.ByteSize), Is.EqualTo((0L, 7L)));
            Assert.That(width.ByteOffset, Is.EqualTo(7L), "m_Width should start right after the real 3-byte payload, not after a fixed 5-byte TypelessData size");
            Assert.That(width.AsInt32(), Is.EqualTo(256));
        }

        [Test]
        public void ComputeSize_ModeratelyNestedStructChain_ComputesWithoutThrowing()
        {
            var node = BuildNestedStructChain(depth: 20);
            var byteSource = new InMemoryByteSource(new byte[4]);

            var size = TypeTreeOffsetWalker.ComputeSize(node, 0, byteSource);

            Assert.That(size, Is.EqualTo(4L));
        }

        [Test]
        public void ComputeSize_StructChainNestingBeyondMaxDepth_ThrowsInsteadOfOverflowingTheStack()
        {
            var node = BuildNestedStructChain(depth: 200);
            var byteSource = new InMemoryByteSource(new byte[4]);

            Assert.Throws<ArtifactInspectorException>(() => TypeTreeOffsetWalker.ComputeSize(node, 0, byteSource));
        }

        [Test]
        public void ComputeSize_ManagedReferenceShapedNode_Throws()
        {
            var node = FakeTypeTreeBuilder.ManagedReference("field");
            var byteSource = new InMemoryByteSource(new byte[4]);

            Assert.Throws<UnsupportedManagedReferenceShapeException>(() => TypeTreeOffsetWalker.ComputeSize(node, 0, byteSource));
        }

        [Test]
        public void GetChildByIndex_MonoBehaviourShapedTree_BaseFieldsBeforeManagedReferenceFieldAreReadable()
        {
            // Base MonoBehaviour fields declared before a user [SerializeReference] field, matching
            // Unity's real serialization order: reading them must not require sizing the field after them.
            var structNode = FakeTypeTreeBuilder.Struct(
                "Base", "MonoBehaviour",
                FakeTypeTreeBuilder.Int32("m_GameObject"),
                FakeTypeTreeBuilder.Bool("m_Enabled"),
                FakeTypeTreeBuilder.ManagedReference("m_UserField"));

            var buffer = new byte[5]; // m_GameObject(4) + m_Enabled(1); m_UserField's bytes are unreachable here
            var byteSource = new InMemoryByteSource(buffer);
            var reader = new TypeTreeReader(structNode, byteSource, 0);

            Assert.DoesNotThrow(() =>
            {
                var gameObject = reader.Field("m_GameObject");
                var enabled = reader.Field("m_Enabled");
                Assert.That((gameObject.ByteOffset, gameObject.ByteSize), Is.EqualTo((0L, 4L)));
                Assert.That((enabled.ByteOffset, enabled.ByteSize), Is.EqualTo((4L, 1L)));
            });

            // Resolving m_UserField's own offset still needs its size (to know where the *next*
            // field would start), so touching it -- unlike its unrelated earlier siblings -- throws.
            Assert.Throws<UnsupportedManagedReferenceShapeException>(() => reader.Field("m_UserField"));
        }

        /// <summary>Builds a straight chain of depth nested single-child structs wrapping one leaf int.</summary>
        private static TypeTreeNode BuildNestedStructChain(int depth)
        {
            var node = FakeTypeTreeBuilder.Int32("leaf");
            for (var i = 0; i < depth; i++)
            {
                node = FakeTypeTreeBuilder.Struct("wrapper" + i, "Wrapper", node);
            }

            return node;
        }
    }
}