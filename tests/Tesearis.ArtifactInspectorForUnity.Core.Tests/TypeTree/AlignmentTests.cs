using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TypeTree
{
    /// <summary>
    /// Covers <see cref="TypeTreeMetaFlags.AlignBytes"/>/<see cref="TypeTreeMetaFlags.AnyChildUsesAlignBytes"/>
    /// folded into <see cref="TypeTreeOffsetWalker.ApplyAlignment"/> as a single
    /// pass applied once per field, rather than a separate alignment walk.
    /// </summary>
    [TestFixture]
    public class AlignmentTests
    {
        [Test]
        public void ApplyAlignment_AlignedNode_RoundsUpToFourByteBoundary()
        {
            var aligned = FakeTypeTreeBuilder.Byte("flag", aligned: true);

            Assert.That(TypeTreeOffsetWalker.ApplyAlignment(aligned, 5), Is.EqualTo(8L));
            Assert.That(TypeTreeOffsetWalker.ApplyAlignment(aligned, 4), Is.EqualTo(4L));
            Assert.That(TypeTreeOffsetWalker.ApplyAlignment(aligned, 0), Is.EqualTo(0L));
        }

        [Test]
        public void ApplyAlignment_UnalignedNode_LeavesOffsetUntouched()
        {
            var unaligned = FakeTypeTreeBuilder.Byte("flag", aligned: false);

            Assert.That(TypeTreeOffsetWalker.ApplyAlignment(unaligned, 5), Is.EqualTo(5L));
        }

        [Test]
        public void IsAligned_AnyChildUsesAlignBytesWithoutAlignBytes_IsFalse()
        {
            // AnyChildUsesAlignBytes is a hint set on ancestor/container nodes meaning "some descendant
            // needs alignment somewhere in my subtree" -- it says nothing about whether *this* node's own
            // trailing edge needs padding. Only AlignBytes means that. Regression test for a bug where the
            // two were conflated, which spuriously padded after container nodes (structs/array-element
            // templates) containing a nested string/array anywhere below them.
            var node = FakeTypeTreeBuilder.LeafWithAnyChildUsesAlignBytesOnly("container", "SomeStruct", -1);

            Assert.That(node.IsAligned, Is.False);
        }

        [Test]
        public void GetChildByIndex_FieldWithOnlyAnyChildUsesAlignBytes_DoesNotPadTheNextSibling()
        {
            var structNode = FakeTypeTreeBuilder.Struct(
                "Base", "WithFalselyAlignedContainer",
                FakeTypeTreeBuilder.LeafWithAnyChildUsesAlignBytesOnly("flag", "UInt8", 1),
                FakeTypeTreeBuilder.Int32("value"));

            var buffer = new byte[5];
            var byteSource = new InMemoryByteSource(buffer);
            var reader = new TypeTreeReader(structNode, byteSource, 0);

            var flag = reader.Field("flag");
            var value = reader.Field("value");

            Assert.That(flag.ByteOffset, Is.EqualTo(0L));
            Assert.That(value.ByteOffset, Is.EqualTo(1L), "AnyChildUsesAlignBytes alone must not pad after this node's own data");
        }

        [Test]
        public void GetChildByIndex_AlignedBoolFollowedByInt_IntStartsOnFourByteBoundary()
        {
            var structNode = FakeTypeTreeBuilder.Struct(
                "Base", "WithAlignedBool",
                FakeTypeTreeBuilder.Bool("flag", aligned: true),
                FakeTypeTreeBuilder.Int32("value"));

            var buffer = new byte[8];
            var byteSource = new InMemoryByteSource(buffer);
            var reader = new TypeTreeReader(structNode, byteSource, 0);

            var flag = reader.Field("flag");
            var value = reader.Field("value");

            Assert.That(flag.ByteOffset, Is.EqualTo(0L));
            Assert.That(flag.ByteSize, Is.EqualTo(1L));
            Assert.That(value.ByteOffset, Is.EqualTo(4L), "the int should round up to the next 4-byte boundary, not sit at offset 1");
        }

        [Test]
        public void IsAligned_VectorField_ReadsAlignBytesOffTheInnerArrayNodeNotTheOuterWrapper()
        {
            var aligned = FakeTypeTreeBuilder.Vector("m_Items", FakeTypeTreeBuilder.Byte("data"), aligned: true);
            var unaligned = FakeTypeTreeBuilder.Vector("m_Items", FakeTypeTreeBuilder.Byte("data"), aligned: false);

            Assert.That(aligned.IsAligned, Is.True);
            Assert.That(unaligned.IsAligned, Is.False);
        }

        [Test]
        public void GetChildByIndex_AlignedVectorFieldFollowedByInt_IntStartsOnFourByteBoundary()
        {
            var structNode = FakeTypeTreeBuilder.Struct(
                "Base", "WithAlignedVector",
                FakeTypeTreeBuilder.Vector("m_Bytes", FakeTypeTreeBuilder.Byte("data"), aligned: true),
                FakeTypeTreeBuilder.Int32("value"));

            var buffer = new ByteBufferWriter()
                .WriteInt32(3) // vector length prefix
                .WriteByte(1).WriteByte(2).WriteByte(3) // 3 raw bytes -> ends at offset 7, unaligned
                .WriteByte(0) // padding byte to the next 4-byte boundary (offset 8)
                .WriteInt32(42)
                .ToArray();
            var byteSource = new InMemoryByteSource(buffer);
            var reader = new TypeTreeReader(structNode, byteSource, 0);

            var bytes = reader.Field("m_Bytes");
            var value = reader.Field("value");

            Assert.That((bytes.ByteOffset, bytes.ByteSize), Is.EqualTo((0L, 7L)));
            Assert.That(value.ByteOffset, Is.EqualTo(8L), "value should start after the vector's trailing alignment padding, not right after its 7 raw bytes");
            Assert.That(value.AsInt32(), Is.EqualTo(42));
        }
    }
}