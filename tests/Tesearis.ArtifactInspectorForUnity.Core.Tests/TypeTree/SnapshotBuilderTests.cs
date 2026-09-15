using System;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TypeTree
{
    [TestFixture]
    public class SnapshotBuilderTests
    {
        [Test]
        public void Build_ScalarInt_DecodesEagerlyAndNeverReadsAgain()
        {
            var node = FakeTypeTreeBuilder.Int32("v");
            var buffer = new ByteBufferWriter().WriteInt32(-7).ToArray();
            var byteSource = new CountingByteSource(new InMemoryByteSource(buffer));

            var field = SnapshotBuilder.Build(node, 0, byteSource, MaterializeOptions.Default);
            var readsAfterBuild = byteSource.ReadCallCount;

            Assert.That(field.IsDeferred, Is.False);
            Assert.That(field.AsInt32(), Is.EqualTo(-7));
            Assert.That(byteSource.ReadCallCount, Is.EqualTo(readsAfterBuild), "an eagerly-decoded scalar must not read again");
        }

        [Test]
        public void Build_StringUnderThreshold_DecodesEagerlyAndNeverReadsAgain()
        {
            var node = FakeTypeTreeBuilder.String("s");
            var buffer = new ByteBufferWriter().WriteString("hello").ToArray();
            var byteSource = new CountingByteSource(new InMemoryByteSource(buffer));

            var field = SnapshotBuilder.Build(node, 0, byteSource, MaterializeOptions.Default);
            var readsAfterBuild = byteSource.ReadCallCount;

            Assert.That(field.IsDeferred, Is.False);
            Assert.That(field.AsString(), Is.EqualTo("hello"));
            Assert.That(byteSource.ReadCallCount, Is.EqualTo(readsAfterBuild));
        }

        [Test]
        public void Build_StringOverThreshold_IsDeferredButStillReadableViaFallback()
        {
            var node = FakeTypeTreeBuilder.String("s");
            var buffer = new ByteBufferWriter().WriteString("hello world").ToArray();
            var options = new MaterializeOptions { MaxInlineFieldSizeBytes = 4 };

            var field = SnapshotBuilder.Build(node, 0, new InMemoryByteSource(buffer), options);

            Assert.That(field.IsDeferred, Is.True);
            Assert.That(field.AsString(), Is.EqualTo("hello world"));
        }

        [Test]
        public void Build_StringExactlyAtThreshold_IsEager_OneByteOver_IsDeferred()
        {
            var node = FakeTypeTreeBuilder.String("s");
            var buffer = new ByteBufferWriter().WriteString(new string('a', 8)).ToArray();

            var atThreshold = SnapshotBuilder.Build(node, 0, new InMemoryByteSource(buffer), new MaterializeOptions { MaxInlineFieldSizeBytes = 8 });
            Assert.That(atThreshold.IsDeferred, Is.False);

            var overThreshold = SnapshotBuilder.Build(node, 0, new InMemoryByteSource(buffer), new MaterializeOptions { MaxInlineFieldSizeBytes = 7 });
            Assert.That(overThreshold.IsDeferred, Is.True);
        }

        [Test]
        public void Build_TypelessDataUnderThreshold_DecodesEagerly()
        {
            var node = FakeTypeTreeBuilder.TypelessData("raw");
            var buffer = new ByteBufferWriter().WriteRawBytes(new byte[] { 1, 2, 3, 4 }).ToArray();

            var field = SnapshotBuilder.Build(node, 0, new InMemoryByteSource(buffer), MaterializeOptions.Default);

            Assert.That(field.IsDeferred, Is.False);
            Assert.That(field.ReadRawBytes(), Is.EqualTo(buffer));
        }

        [Test]
        public void Build_FixedElementArrayUnderThreshold_DecodesEagerlyAndNeverReadsAgain()
        {
            var arrayNode = FakeTypeTreeBuilder.Array(FakeTypeTreeBuilder.Int32("data"));
            var buffer = new ByteBufferWriter().WriteInt32(3).WriteInt32(10).WriteInt32(20).WriteInt32(30).ToArray();
            var byteSource = new CountingByteSource(new InMemoryByteSource(buffer));

            var field = SnapshotBuilder.Build(arrayNode, 0, byteSource, MaterializeOptions.Default);
            var readsAfterBuild = byteSource.ReadCallCount;

            Assert.That(field.IsDeferred, Is.False);
            Assert.That(field.ArrayLength(), Is.EqualTo(3));
            Assert.That(field.Element(0).AsInt32(), Is.EqualTo(10));
            Assert.That(field.Element(1).AsInt32(), Is.EqualTo(20));
            Assert.That(field.Element(2).AsInt32(), Is.EqualTo(30));
            Assert.That(byteSource.ReadCallCount, Is.EqualTo(readsAfterBuild), "decoded elements must not re-read");
        }

        [Test]
        public void Build_FixedElementArrayOverThreshold_IsDeferredButStillReadableViaFallback()
        {
            var arrayNode = FakeTypeTreeBuilder.Array(FakeTypeTreeBuilder.Int32("data"));
            var buffer = new ByteBufferWriter().WriteInt32(3).WriteInt32(10).WriteInt32(20).WriteInt32(30).ToArray();
            var options = new MaterializeOptions { MaxInlineFieldSizeBytes = 4 };

            var field = SnapshotBuilder.Build(arrayNode, 0, new InMemoryByteSource(buffer), options);

            Assert.That(field.IsDeferred, Is.True);
            Assert.That(field.ArrayLength(), Is.EqualTo(3));
            Assert.That(field.Element(1).AsInt32(), Is.EqualTo(20));
        }

        [Test]
        public void Build_StructElementArrayUnderThreshold_DecodesEagerly()
        {
            var elementTemplate = FakeTypeTreeBuilder.Struct("data", "Pair", FakeTypeTreeBuilder.Int32("a"), FakeTypeTreeBuilder.Int32("b"));
            var arrayNode = FakeTypeTreeBuilder.Array(elementTemplate);
            var buffer = new ByteBufferWriter().WriteInt32(2).WriteInt32(1).WriteInt32(2).WriteInt32(3).WriteInt32(4).ToArray();

            var field = SnapshotBuilder.Build(arrayNode, 0, new InMemoryByteSource(buffer), MaterializeOptions.Default);

            Assert.That(field.IsDeferred, Is.False);
            Assert.That(field.Element(0).Field("a").AsInt32(), Is.EqualTo(1));
            Assert.That(field.Element(0).Field("b").AsInt32(), Is.EqualTo(2));
            Assert.That(field.Element(1).Field("a").AsInt32(), Is.EqualTo(3));
            Assert.That(field.Element(1).Field("b").AsInt32(), Is.EqualTo(4));
        }

        [Test]
        public void Build_StructElementArrayOverThreshold_IsDeferredButStillReadableViaFallback()
        {
            var elementTemplate = FakeTypeTreeBuilder.Struct("data", "Pair", FakeTypeTreeBuilder.Int32("a"), FakeTypeTreeBuilder.Int32("b"));
            var arrayNode = FakeTypeTreeBuilder.Array(elementTemplate);
            var buffer = new ByteBufferWriter().WriteInt32(2).WriteInt32(1).WriteInt32(2).WriteInt32(3).WriteInt32(4).ToArray();
            var options = new MaterializeOptions { MaxInlineFieldSizeBytes = 4 };

            var field = SnapshotBuilder.Build(arrayNode, 0, new InMemoryByteSource(buffer), options);

            Assert.That(field.IsDeferred, Is.True);
            Assert.That(field.Element(1).Field("a").AsInt32(), Is.EqualTo(3));
        }

        [Test]
        public void Build_Struct_FieldLookupAndAsPPtrWork()
        {
            var pptrNode = FakeTypeTreeBuilder.Struct(
                "m_Script", "PPtr<MonoScript>",
                FakeTypeTreeBuilder.Int32("m_FileID"),
                FakeTypeTreeBuilder.Leaf("m_PathID", "SInt64", 8));
            var buffer = new ByteBufferWriter().WriteInt32(0).WriteInt64(12345L).ToArray();

            var field = SnapshotBuilder.Build(pptrNode, 0, new InMemoryByteSource(buffer), MaterializeOptions.Default);
            var pptr = field.AsPPtr();

            Assert.That(pptr.FileId, Is.EqualTo(0));
            Assert.That(pptr.PathId, Is.EqualTo(12345L));
            Assert.That(pptr.IsLocal, Is.True);
        }

        [Test]
        public void AsSingle_OnMismatchedScalarKind_ReinterpretsSameBitsAsTypeTreeReader()
        {
            var node = FakeTypeTreeBuilder.Int32("v");
            var buffer = new ByteBufferWriter().WriteInt32(1067282596).ToArray(); // bit pattern of 1.5f

            var field = SnapshotBuilder.Build(node, 0, new InMemoryByteSource(buffer), MaterializeOptions.Default);
            var reader = new TypeTreeReader(node, new InMemoryByteSource(buffer), 0);

            Assert.That(field.AsSingle(), Is.EqualTo(reader.AsSingle()));
        }

        [Test]
        public void Build_ManagedReferenceShape_ThrowsUnsupportedManagedReferenceShapeException()
        {
            var node = FakeTypeTreeBuilder.ManagedReference("v");

            Assert.Throws<UnsupportedManagedReferenceShapeException>(
                () => SnapshotBuilder.Build(node, 0, new InMemoryByteSource(new byte[8]), MaterializeOptions.Default));
        }

        [Test]
        public void Field_MissingField_ThrowsSameExceptionTypeAsTypeTreeReader()
        {
            var structNode = FakeTypeTreeBuilder.Struct("Base", "Size", FakeTypeTreeBuilder.Int32("m_Width"));
            var field = SnapshotBuilder.Build(structNode, 0, new InMemoryByteSource(new byte[4]), MaterializeOptions.Default);

            Assert.Throws<ArtifactInspectorException>(() => field.Field("m_DoesNotExist"));
        }

        [Test]
        public void TryGetField_MissingField_ReturnsFalse()
        {
            var structNode = FakeTypeTreeBuilder.Struct("Base", "Size", FakeTypeTreeBuilder.Int32("m_Width"));
            var field = SnapshotBuilder.Build(structNode, 0, new InMemoryByteSource(new byte[4]), MaterializeOptions.Default);

            Assert.That(field.TryGetField("m_DoesNotExist", out _), Is.False);
        }

        [Test]
        public void Element_IndexAtOrBeyondArrayLength_ThrowsArgumentOutOfRangeException()
        {
            var arrayNode = FakeTypeTreeBuilder.Array(FakeTypeTreeBuilder.Int32("data"));
            var buffer = new ByteBufferWriter().WriteInt32(2).WriteInt32(7).WriteInt32(8).ToArray();
            var field = SnapshotBuilder.Build(arrayNode, 0, new InMemoryByteSource(buffer), MaterializeOptions.Default);

            Assert.That(field.Element(0).AsInt32(), Is.EqualTo(7));
            Assert.That(field.Element(1).AsInt32(), Is.EqualTo(8));
            Assert.Throws<ArgumentOutOfRangeException>(() => field.Element(2));
        }

        [Test]
        public void ArrayLength_OnNonArrayField_Throws()
        {
            var field = SnapshotBuilder.Build(FakeTypeTreeBuilder.Int32("v"), 0, new InMemoryByteSource(new byte[4]), MaterializeOptions.Default);

            Assert.Throws<ArtifactInspectorException>(() => field.ArrayLength());
        }

        [Test]
        public void Build_FixedElementArrayWithAlignedElementTemplate_MatchesTypeTreeReaderOffsetsAndSizes()
        {
            // Regression test: the fixed-leaf-element fast path must not skip the per-element alignment
            // ComputeArraySize/TypeTreeReader both apply after every element -- see SnapshotBuilder.BuildArray's
            // isFixedLeafElement guard. Each aligned byte element pads the running offset up to the next 4-byte
            // boundary, so a naive "pack elements back-to-back" fast path would compute the wrong total size.
            var elementTemplate = FakeTypeTreeBuilder.Byte("data", aligned: true);
            var arrayNode = FakeTypeTreeBuilder.Array(elementTemplate);
            var buffer = new ByteBufferWriter()
                .WriteInt32(2)
                .WriteByte(10).AlignTo4()
                .WriteByte(20).AlignTo4()
                .ToArray();

            var field = SnapshotBuilder.Build(arrayNode, 0, new InMemoryByteSource(buffer), MaterializeOptions.Default);
            var reader = new TypeTreeReader(arrayNode, new InMemoryByteSource(buffer), 0);

            Assert.That(field.IsDeferred, Is.False);
            Assert.That(field.ArrayLength(), Is.EqualTo(2));
            Assert.That(field.Element(0).AsByte(), Is.EqualTo(10));
            Assert.That(field.Element(1).AsByte(), Is.EqualTo(20));
            Assert.That(field.ByteSize, Is.EqualTo(reader.ByteSize));
            Assert.That(field.Element(1).ByteOffset, Is.EqualTo(reader.Element(1).ByteOffset));
        }

        [Test]
        public void ToReader_OnDeferredField_ReturnsAWorkingLiveReader()
        {
            var node = FakeTypeTreeBuilder.String("s");
            var buffer = new ByteBufferWriter().WriteString("a long deferred string").ToArray();
            var options = new MaterializeOptions { MaxInlineFieldSizeBytes = 1 };

            var field = SnapshotBuilder.Build(node, 0, new InMemoryByteSource(buffer), options);

            Assert.That(field.IsDeferred, Is.True);
            Assert.That(field.ToReader().AsString(), Is.EqualTo("a long deferred string"));
        }

        [Test]
        public void Build_ConstantSizeStructElementArrayOverThreshold_IsDeferredWithoutReadingElements()
        {
            var elementTemplate = FakeTypeTreeBuilder.Struct("data", "SubMesh",
                FakeTypeTreeBuilder.Int32("firstByte"),
                FakeTypeTreeBuilder.Int32("indexCount"),
                FakeTypeTreeBuilder.Int32("topology"));

            var arrayNode = FakeTypeTreeBuilder.Array(elementTemplate);
            const int elementCount = 1000;
            var writer = new ByteBufferWriter().WriteInt32(elementCount);
            for (var i = 0; i < elementCount; i++)
            {
                writer.WriteInt32(i * 10).WriteInt32(i * 20).WriteInt32(4);
            }

            var buffer = writer.ToArray();
            var byteSource = new CountingByteSource(new InMemoryByteSource(buffer));
            var options = new MaterializeOptions { MaxInlineFieldSizeBytes = 1024 };

            var field = SnapshotBuilder.Build(arrayNode, 0, byteSource, options);

            var readsDuringBuild = byteSource.ReadCallCount;
            Assert.That(field.IsDeferred, Is.True);
            // Only length prefix was read during build, elements were not touched
            Assert.That(readsDuringBuild, Is.EqualTo(1));

            Assert.That(field.ArrayLength(), Is.EqualTo(elementCount));
            Assert.That(field.ByteSize, Is.EqualTo(4L + (long)elementCount * 12));
            Assert.That(field.Element(500).Field("indexCount").AsInt32(), Is.EqualTo(500 * 20));
        }
    }
}
