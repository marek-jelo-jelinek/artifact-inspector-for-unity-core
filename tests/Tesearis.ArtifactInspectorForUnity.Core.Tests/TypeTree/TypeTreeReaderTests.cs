using System;
using System.Linq;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TypeTree
{
    [TestFixture]
    public class TypeTreeReaderTests
    {
        [Test]
        public void TryGetField_ExistingField_ReturnsTrueWithCorrectValue()
        {
            var structNode = FakeTypeTreeBuilder.Struct(
                "Base", "Size",
                FakeTypeTreeBuilder.Int32("m_Width"),
                FakeTypeTreeBuilder.Int32("m_Height"));

            var buffer = new ByteBufferWriter().WriteInt32(1920).WriteInt32(1080).ToArray();
            var reader = new TypeTreeReader(structNode, new InMemoryByteSource(buffer), 0);

            Assert.That(reader.TryGetField("m_Height", out TypeTreeReader height), Is.True);
            Assert.That(height.AsInt32(), Is.EqualTo(1080));
        }

        [Test]
        public void TryGetField_MissingField_ReturnsFalse()
        {
            var structNode = FakeTypeTreeBuilder.Struct("Base", "Size", FakeTypeTreeBuilder.Int32("m_Width"));
            var reader = new TypeTreeReader(structNode, new InMemoryByteSource(new byte[4]), 0);

            Assert.That(reader.TryGetField("m_DoesNotExist", out _), Is.False);
        }

        [Test]
        public void Field_MissingField_Throws()
        {
            var structNode = FakeTypeTreeBuilder.Struct("Base", "Size", FakeTypeTreeBuilder.Int32("m_Width"));
            var reader = new TypeTreeReader(structNode, new InMemoryByteSource(new byte[4]), 0);

            Assert.Throws<ArtifactInspectorException>(() => reader.Field("m_DoesNotExist"));
        }

        [Test]
        public void LeafDecoders_DecodeAgainstHandBuiltBuffers()
        {
            var buffer = new ByteBufferWriter()
                .WriteInt32(-7)
                .WriteByte(1)
                .WriteString("hi")
                .ToArray();

            var intReader = new TypeTreeReader(FakeTypeTreeBuilder.Int32("v"), new InMemoryByteSource(buffer), 0);
            var boolReader = new TypeTreeReader(FakeTypeTreeBuilder.Bool("v"), new InMemoryByteSource(buffer), 4);
            var stringReader = new TypeTreeReader(FakeTypeTreeBuilder.String("v"), new InMemoryByteSource(buffer), 5);

            Assert.That(intReader.AsInt32(), Is.EqualTo(-7));
            Assert.That(boolReader.AsBoolean(), Is.True);
            Assert.That(stringReader.AsString(), Is.EqualTo("hi"));
        }

        [Test]
        public void RepeatedFieldAccess_ReusesMemoizedOffsets_InsteadOfReReadingLengthPrefixes()
        {
            // A string field followed by two ints: resolving offsets for the ints
            // requires knowing the string's length, i.e. one 4-byte read. Once
            // resolved, revisiting any of the three fields must not read again.
            var structNode = FakeTypeTreeBuilder.Struct(
                "Base", "WithString",
                FakeTypeTreeBuilder.String("s"),
                FakeTypeTreeBuilder.Int32("f1"),
                FakeTypeTreeBuilder.Int32("f2"));

            var buffer = new ByteBufferWriter()
                .WriteString("abc")
                .WriteInt32(1)
                .WriteInt32(2)
                .ToArray();

            var byteSource = new CountingByteSource(new InMemoryByteSource(buffer));
            var reader = new TypeTreeReader(structNode, byteSource, 0);

            // Resolving "f2"'s offset requires walking through "s" and "f1" first;
            // only "s" (variable-size) needs an actual read to know its length.
            var f2 = reader.Field("f2");
            var callsAfterFirstResolution = byteSource.ReadCallCount;
            Assert.That(callsAfterFirstResolution, Is.EqualTo(1), "only the string's own length prefix should need a read to resolve offsets");

            // Re-resolving the same fields again must hit the per-instance
            // memoized offsets cache, not re-walk/re-read anything.
            reader.Field("s");
            reader.Field("f1");
            reader.Field("f2");
            Assert.That(byteSource.ReadCallCount, Is.EqualTo(callsAfterFirstResolution));

            // Decoding f2's actual value is a separate, expected read -- values
            // themselves are never memoized, only the offsets used to find them.
            Assert.That(f2.AsInt32(), Is.EqualTo(2));
            Assert.That(byteSource.ReadCallCount, Is.EqualTo(callsAfterFirstResolution + 1));
        }

        [Test]
        public void ArrayLength_NegativeLengthPrefix_Throws()
        {
            var arrayNode = FakeTypeTreeBuilder.Array(FakeTypeTreeBuilder.Int32("data"));
            var buffer = new ByteBufferWriter().WriteInt32(-1).ToArray();
            var reader = new TypeTreeReader(arrayNode, new InMemoryByteSource(buffer), 0);

            Assert.Throws<ArtifactInspectorException>(() => reader.ArrayLength());
        }

        [Test]
        public void ArrayLength_LengthPrefixExceedsRemainingBytes_ThrowsCleanExceptionInsteadOfHugeAllocation()
        {
            var arrayNode = FakeTypeTreeBuilder.Array(FakeTypeTreeBuilder.Int32("data"));
            var buffer = new ByteBufferWriter().WriteInt32(int.MaxValue).ToArray();
            var reader = new TypeTreeReader(arrayNode, new InMemoryByteSource(buffer), 0);

            Assert.Throws<ArtifactInspectorException>(() => reader.Elements().ToList());
        }

        [Test]
        public void AsUInt32_DecodesLittleEndianValue()
        {
            var buffer = new ByteBufferWriter().WriteUInt32(4000000000).ToArray();
            var reader = new TypeTreeReader(FakeTypeTreeBuilder.Leaf("v", "unsigned int", 4), new InMemoryByteSource(buffer), 0);

            Assert.That(reader.AsUInt32(), Is.EqualTo(4000000000u));
        }

        [Test]
        public void AsInt64_DecodesLittleEndianValue()
        {
            var buffer = new ByteBufferWriter().WriteInt64(-123456789012345L).ToArray();
            var reader = new TypeTreeReader(FakeTypeTreeBuilder.Leaf("v", "SInt64", 8), new InMemoryByteSource(buffer), 0);

            Assert.That(reader.AsInt64(), Is.EqualTo(-123456789012345L));
        }

        [Test]
        public void AsUInt64_DecodesLittleEndianValue()
        {
            var buffer = new ByteBufferWriter().WriteUInt64(18000000000000000000UL).ToArray();
            var reader = new TypeTreeReader(FakeTypeTreeBuilder.Leaf("v", "UInt64", 8), new InMemoryByteSource(buffer), 0);

            Assert.That(reader.AsUInt64(), Is.EqualTo(18000000000000000000UL));
        }

        [Test]
        public void AsSingle_DecodesLittleEndianValue()
        {
            var buffer = new ByteBufferWriter().WriteSingle(3.5f).ToArray();
            var reader = new TypeTreeReader(FakeTypeTreeBuilder.Leaf("v", "float", 4), new InMemoryByteSource(buffer), 0);

            Assert.That(reader.AsSingle(), Is.EqualTo(3.5f));
        }

        [Test]
        public void AsInt16_DecodesLittleEndianValue()
        {
            var buffer = new ByteBufferWriter().WriteInt16(-1234).ToArray();
            var reader = new TypeTreeReader(FakeTypeTreeBuilder.Leaf("v", "SInt16", 2), new InMemoryByteSource(buffer), 0);

            Assert.That(reader.AsInt16(), Is.EqualTo(-1234));
        }

        [Test]
        public void AsUInt16_DecodesLittleEndianValue()
        {
            var buffer = new ByteBufferWriter().WriteUInt16(60000).ToArray();
            var reader = new TypeTreeReader(FakeTypeTreeBuilder.Leaf("v", "UInt16", 2), new InMemoryByteSource(buffer), 0);

            Assert.That(reader.AsUInt16(), Is.EqualTo(60000));
        }

        [Test]
        public void AsSByte_DecodesValue()
        {
            var buffer = new ByteBufferWriter().WriteSByte(-100).ToArray();
            var reader = new TypeTreeReader(FakeTypeTreeBuilder.Leaf("v", "SInt8", 1), new InMemoryByteSource(buffer), 0);

            Assert.That(reader.AsSByte(), Is.EqualTo(-100));
        }

        [Test]
        public void AsDouble_DecodesLittleEndianValue()
        {
            var buffer = new ByteBufferWriter().WriteDouble(3.14159265358979).ToArray();
            var reader = new TypeTreeReader(FakeTypeTreeBuilder.Leaf("v", "double", 8), new InMemoryByteSource(buffer), 0);

            Assert.That(reader.AsDouble(), Is.EqualTo(3.14159265358979));
        }

        [Test]
        public void HasField_ExistingField_ReturnsTrue()
        {
            var structNode = FakeTypeTreeBuilder.Struct("Base", "Size", FakeTypeTreeBuilder.Int32("m_Width"));
            var reader = new TypeTreeReader(structNode, new InMemoryByteSource(new byte[4]), 0);

            Assert.That(reader.HasField("m_Width"), Is.True);
        }

        [Test]
        public void HasField_MissingField_ReturnsFalse()
        {
            var structNode = FakeTypeTreeBuilder.Struct("Base", "Size", FakeTypeTreeBuilder.Int32("m_Width"));
            var reader = new TypeTreeReader(structNode, new InMemoryByteSource(new byte[4]), 0);

            Assert.That(reader.HasField("m_DoesNotExist"), Is.False);
        }

        [Test]
        public void ArrayLength_OnNonArrayNode_Throws()
        {
            var reader = new TypeTreeReader(FakeTypeTreeBuilder.Int32("v"), new InMemoryByteSource(new byte[4]), 0);

            Assert.Throws<ArtifactInspectorException>(() => reader.ArrayLength());
        }

        [Test]
        public void Element_OnNonArrayNode_Throws()
        {
            var reader = new TypeTreeReader(FakeTypeTreeBuilder.Int32("v"), new InMemoryByteSource(new byte[4]), 0);

            Assert.Throws<ArtifactInspectorException>(() => reader.Element(0));
        }

        [Test]
        public void Element_NegativeIndex_ThrowsArgumentOutOfRangeException()
        {
            var arrayNode = FakeTypeTreeBuilder.Array(FakeTypeTreeBuilder.Int32("data"));
            var buffer = new ByteBufferWriter().WriteInt32(2).WriteInt32(7).WriteInt32(8).ToArray();
            var reader = new TypeTreeReader(arrayNode, new InMemoryByteSource(buffer), 0);

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Element(-1));
        }

        [Test]
        public void Element_IndexAtOrBeyondArrayLength_ThrowsArgumentOutOfRangeException()
        {
            var arrayNode = FakeTypeTreeBuilder.Array(FakeTypeTreeBuilder.Int32("data"));
            var buffer = new ByteBufferWriter().WriteInt32(2).WriteInt32(7).WriteInt32(8).ToArray();
            var reader = new TypeTreeReader(arrayNode, new InMemoryByteSource(buffer), 0);

            // In-range indices still work...
            Assert.That(reader.Element(0).AsInt32(), Is.EqualTo(7));
            Assert.That(reader.Element(1).AsInt32(), Is.EqualTo(8));

            // ...but an index at or beyond ArrayLength() must not silently walk into whatever
            // bytes happen to follow the array instead of failing clearly.
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Element(2));
        }

        [Test]
        public void Elements_OnNonArrayNode_Throws()
        {
            var reader = new TypeTreeReader(FakeTypeTreeBuilder.Int32("v"), new InMemoryByteSource(new byte[4]), 0);

            Assert.Throws<ArtifactInspectorException>(() => reader.Elements().ToList());
        }

        [Test]
        public void ReadRawBytes_NoArgs_ReturnsAllOfThisFieldsBytes()
        {
            var buffer = new ByteBufferWriter().WriteInt32(-7).ToArray();
            var reader = new TypeTreeReader(FakeTypeTreeBuilder.Int32("v"), new InMemoryByteSource(buffer), 0);

            Assert.That(reader.ReadRawBytes(), Is.EqualTo(buffer));
        }

        [Test]
        public void ReadRawBytes_WithOffsetAndCount_ReturnsExactlyTheRequestedRange()
        {
            var buffer = new byte[] { 1, 2, 3, 4, 5 };
            var reader = new TypeTreeReader(FakeTypeTreeBuilder.Leaf("v", "SomeBlob", 5), new InMemoryByteSource(buffer), 0);

            Assert.That(reader.ReadRawBytes(2, 3), Is.EqualTo(new byte[] { 3, 4, 5 }));
        }

        [Test]
        public void ReadRawBytes_ShortRead_ThrowsEvenWhenTheDeclaredLengthWouldHaveAllowedIt()
        {
            // Length lies generously (as if the file were bigger), but the actual backing data is short --
            // isolates the pre-existing "fewer bytes were actually read than requested" check from the newer
            // declared-length bounds check, which wouldn't trigger here.
            var buffer = new byte[] { 1, 2 };
            var byteSource = new LengthOverrideByteSource(new InMemoryByteSource(buffer), length: 1000);
            var reader = new TypeTreeReader(FakeTypeTreeBuilder.Leaf("v", "SomeBlob", 2), byteSource, 0);

            Assert.Throws<ArtifactInspectorException>(() => reader.ReadRawBytes(0, 10));
        }

        [Test]
        public void AsString_LengthPrefixExceedsRemainingBytes_ThrowsCleanExceptionInsteadOfHugeAllocation()
        {
            var buffer = new ByteBufferWriter().WriteInt32(int.MaxValue).ToArray();
            var reader = new TypeTreeReader(FakeTypeTreeBuilder.String("v"), new InMemoryByteSource(buffer), 0);

            Assert.Throws<ArtifactInspectorException>(() => reader.AsString());
        }

        [Test]
        public void ReadRawBytes_CountExceedsRemainingBytes_ThrowsCleanExceptionInsteadOfHugeAllocation()
        {
            var buffer = new byte[] { 1, 2 };
            var reader = new TypeTreeReader(FakeTypeTreeBuilder.Leaf("v", "SomeBlob", 2), new InMemoryByteSource(buffer), 0);

            Assert.Throws<ArtifactInspectorException>(() => reader.ReadRawBytes(0, int.MaxValue));
        }

        [Test]
        public void ReadRawBytes_NegativeCount_ThrowsArtifactInspectorException()
        {
            var buffer = new byte[] { 1, 2 };
            var reader = new TypeTreeReader(FakeTypeTreeBuilder.Leaf("v", "SomeBlob", 2), new InMemoryByteSource(buffer), 0);

            Assert.Throws<ArtifactInspectorException>(() => reader.ReadRawBytes(0, -1));
        }

        [Test]
        public void AsPPtr_LocalReference_DecodesFileIdAndPathId()
        {
            var pptrNode = FakeTypeTreeBuilder.Struct(
                "m_Script", "PPtr<MonoScript>",
                FakeTypeTreeBuilder.Int32("m_FileID"),
                FakeTypeTreeBuilder.Leaf("m_PathID", "SInt64", 8));

            var buffer = new ByteBufferWriter().WriteInt32(0).WriteInt64(12345L).ToArray();
            var reader = new TypeTreeReader(pptrNode, new InMemoryByteSource(buffer), 0);

            var pptr = reader.AsPPtr();

            Assert.That(pptr.FileId, Is.EqualTo(0));
            Assert.That(pptr.PathId, Is.EqualTo(12345L));
            Assert.That(pptr.IsLocal, Is.True);
            Assert.That(pptr.IsNull, Is.False);
        }

        [Test]
        public void AsPPtr_CrossFileReference_DecodesFileIdAndPathId()
        {
            var pptrNode = FakeTypeTreeBuilder.Struct(
                "m_Script", "PPtr<MonoScript>",
                FakeTypeTreeBuilder.Int32("m_FileID"),
                FakeTypeTreeBuilder.Leaf("m_PathID", "SInt64", 8));

            var buffer = new ByteBufferWriter().WriteInt32(2).WriteInt64(999L).ToArray();
            var reader = new TypeTreeReader(pptrNode, new InMemoryByteSource(buffer), 0);

            var pptr = reader.AsPPtr();

            Assert.That(pptr.FileId, Is.EqualTo(2));
            Assert.That(pptr.PathId, Is.EqualTo(999L));
            Assert.That(pptr.IsLocal, Is.False);
        }

        [Test]
        public void AsPPtr_NullReference_IsNullReturnsTrue()
        {
            var pptrNode = FakeTypeTreeBuilder.Struct(
                "m_Script", "PPtr<MonoScript>",
                FakeTypeTreeBuilder.Int32("m_FileID"),
                FakeTypeTreeBuilder.Leaf("m_PathID", "SInt64", 8));

            var buffer = new ByteBufferWriter().WriteInt32(0).WriteInt64(0L).ToArray();
            var reader = new TypeTreeReader(pptrNode, new InMemoryByteSource(buffer), 0);

            Assert.That(reader.AsPPtr().IsNull, Is.True);
        }
    }
}