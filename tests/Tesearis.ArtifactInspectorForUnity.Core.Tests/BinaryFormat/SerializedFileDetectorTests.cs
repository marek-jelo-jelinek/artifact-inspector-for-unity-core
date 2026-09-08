using System;
using System.IO;
using Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using NUnit.Framework;
using static Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport.SerializedFileTestFixtures;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.BinaryFormat
{
    [TestFixture]
    public class SerializedFileDetectorTests
    {
        [Test]
        public void TryDetect_NoTypeTree_ParsesObjectsAndExternalReferences()
        {
            var writer = new ByteBufferWriter();
            AppendLeadingMetadata(writer, "6000.3.0f1", 5, enableTypeTree: false);
            writer.WriteInt32(4);
            AppendTypeEntry(writer, false, new TypeEntrySpec { PersistentTypeId = 1, ScriptTypeIndex = -1 });   // no scriptId
            AppendTypeEntry(writer, false, new TypeEntrySpec { PersistentTypeId = 114, ScriptTypeIndex = -1 }); // scriptId via MonoBehaviour
            AppendTypeEntry(writer, false, new TypeEntrySpec { PersistentTypeId = 200, ScriptTypeIndex = 3 });  // scriptId via scriptTypeIndex>=0
            AppendTypeEntry(writer, false, new TypeEntrySpec { PersistentTypeId = -1, ScriptTypeIndex = -1 });  // scriptId via undefined persistentTypeId
            writer.WriteInt32(3);
            AppendObjectEntry(writer, pathId: 1001, byteStart: 0, byteSize: 64, typeIndex: 0);
            AppendObjectEntry(writer, pathId: 1002, byteStart: 64, byteSize: 128, typeIndex: 1);
            AppendObjectEntry(writer, pathId: 1003, byteStart: 999, byteSize: 16, typeIndex: 99); // out-of-range typeIndex
            writer.WriteInt32(0);
            writer.WriteInt32(1);
            AppendExternalReference(writer, 0x11223344, 0x55667788, 0x99AABBCC, 0xDDEEFF00, type: 2, path: "Assets/Foo.cs");

            var buffer = WrapWithHeader(writer.ToArray(), version: 23, endianness: 0, dataOffset: 1000);

            var detected = SerializedFileDetector.TryDetect(new InMemoryByteSource(buffer), out var info);

            Assert.That(detected, Is.True);
            Assert.That(info.MetadataParsed, Is.True);
            Assert.That(info.Version, Is.EqualTo(23u));
            Assert.That(info.UnityVersion, Is.EqualTo("6000.3.0f1"));
            Assert.That(info.TargetPlatform, Is.EqualTo(5u));
            Assert.That(info.EnableTypeTree, Is.False);
            Assert.That(info.IsMissingTypeTrees, Is.True);

            Assert.That(info.Objects.Count, Is.EqualTo(3));
            Assert.That((info.Objects[0].PathId, info.Objects[0].TypeId, info.Objects[0].ByteOffset, info.Objects[0].ByteSize),
                Is.EqualTo((1001L, 1, 1000L, 64L)));
            Assert.That(info.Objects[0].ClassName, Is.EqualTo("GameObject"));
            Assert.That((info.Objects[1].PathId, info.Objects[1].TypeId, info.Objects[1].ByteOffset, info.Objects[1].ByteSize),
                Is.EqualTo((1002L, 114, 1064L, 128L)));
            Assert.That(info.Objects[1].ClassName, Is.EqualTo("MonoBehaviour"));
            // typeIndex 99 is out of range (only 4 types) -- TypeId must fall back to the raw index.
            Assert.That((info.Objects[2].PathId, info.Objects[2].TypeId, info.Objects[2].ByteOffset, info.Objects[2].ByteSize),
                Is.EqualTo((1003L, 99, 1999L, 16L)));

            Assert.That(info.ExternalReferences.Count, Is.EqualTo(1));
            Assert.That(info.ExternalReferences[0].Path, Is.EqualTo("Assets/Foo.cs"));
            Assert.That(info.ExternalReferences[0].Type, Is.EqualTo(ExternalReferenceType.SerializedAssetType));
            Assert.That(info.ExternalReferences[0].Guid,
                Is.EqualTo(GuidFormatting.FormatUnityGuid(0x11223344, 0x55667788, 0x99AABBCC, 0xDDEEFF00)));
        }

        [Test]
        public void TryDetect_WhitelistedNamedObjectClass_PopulatesNameFromLeadingString()
        {
            var writer = new ByteBufferWriter();
            AppendLeadingMetadata(writer, "6000.3.13f1", 13, enableTypeTree: false);
            writer.WriteInt32(1);
            AppendTypeEntry(writer, false, new TypeEntrySpec { PersistentTypeId = 28, ScriptTypeIndex = -1 }); // Texture2D
            writer.WriteInt32(1);
            AppendObjectEntry(writer, pathId: 1, byteStart: 0, byteSize: 100, typeIndex: 0);
            writer.WriteInt32(0); // script types
            writer.WriteInt32(0); // external references
            var nameOffsetInWriter = writer.Length;
            writer.WriteString("MyTexture");

            var buffer = WrapWithHeader(writer.ToArray(), version: 23, endianness: 0, dataOffset: (ulong)(48 + nameOffsetInWriter));

            var detected = SerializedFileDetector.TryDetect(new InMemoryByteSource(buffer), out var info);

            Assert.That(detected, Is.True);
            Assert.That(info.MetadataParsed, Is.True);
            Assert.That(info.Objects.Count, Is.EqualTo(1));
            Assert.That(info.Objects[0].Name, Is.EqualTo("MyTexture"));
        }

        [Test]
        public void TryDetect_MonoBehaviour_PopulatesNameFromOffset28()
        {
            var writer = new ByteBufferWriter();
            AppendLeadingMetadata(writer, "6000.3.13f1", 13, enableTypeTree: false);
            writer.WriteInt32(1);
            AppendTypeEntry(writer, false, new TypeEntrySpec { PersistentTypeId = 114, ScriptTypeIndex = -1 }); // MonoBehaviour
            writer.WriteInt32(1);
            AppendObjectEntry(writer, pathId: 1, byteStart: 0, byteSize: 100, typeIndex: 0);
            writer.WriteInt32(0);
            writer.WriteInt32(0);
            var objectStartInWriter = writer.Length;
            writer.WriteZeros(28); // PPtr<GameObject> + m_Enabled + padding + PPtr<MonoScript>
            writer.WriteString("MyBehaviour");

            var buffer = WrapWithHeader(writer.ToArray(), version: 23, endianness: 0, dataOffset: (ulong)(48 + objectStartInWriter));

            var detected = SerializedFileDetector.TryDetect(new InMemoryByteSource(buffer), out var info);

            Assert.That(detected, Is.True);
            Assert.That(info.Objects[0].Name, Is.EqualTo("MyBehaviour"));
        }

        [Test]
        public void TryDetect_NonWhitelistedClass_NeverAttemptsNameEvenWhenLeadingBytesLookLikeAString()
        {
            var writer = new ByteBufferWriter();
            AppendLeadingMetadata(writer, "6000.3.13f1", 13, enableTypeTree: false);
            writer.WriteInt32(1);
            AppendTypeEntry(writer, false, new TypeEntrySpec { PersistentTypeId = 1, ScriptTypeIndex = -1 }); // GameObject -- not a NamedObject
            writer.WriteInt32(1);
            AppendObjectEntry(writer, pathId: 1, byteStart: 0, byteSize: 100, typeIndex: 0);
            writer.WriteInt32(0);
            writer.WriteInt32(0);
            var nameOffsetInWriter = writer.Length;
            writer.WriteString("LooksLikeAName"); // would parse as a valid name if this class were whitelisted

            var buffer = WrapWithHeader(writer.ToArray(), version: 23, endianness: 0, dataOffset: (ulong)(48 + nameOffsetInWriter));

            var detected = SerializedFileDetector.TryDetect(new InMemoryByteSource(buffer), out var info);

            Assert.That(detected, Is.True);
            Assert.That(info.Objects[0].Name, Is.Null);
        }

        [Test]
        public void TryDetect_WhitelistedClassWithCorruptLeadingLength_NameIsNullNotGarbage()
        {
            var writer = new ByteBufferWriter();
            AppendLeadingMetadata(writer, "6000.3.13f1", 13, enableTypeTree: false);
            writer.WriteInt32(1);
            AppendTypeEntry(writer, false, new TypeEntrySpec { PersistentTypeId = 28, ScriptTypeIndex = -1 }); // Texture2D
            writer.WriteInt32(1);
            AppendObjectEntry(writer, pathId: 1, byteStart: 0, byteSize: 100, typeIndex: 0);
            writer.WriteInt32(0);
            writer.WriteInt32(0);
            var nameOffsetInWriter = writer.Length;
            writer.WriteInt32(int.MaxValue); // absurd length -- not a real name field
            writer.WriteZeros(16);

            var buffer = WrapWithHeader(writer.ToArray(), version: 23, endianness: 0, dataOffset: (ulong)(48 + nameOffsetInWriter));

            var detected = SerializedFileDetector.TryDetect(new InMemoryByteSource(buffer), out var info);

            Assert.That(detected, Is.True);
            Assert.That(info.Objects[0].Name, Is.Null);
        }

        [Test]
        public void TryDetect_ManyObjectsWithFarApartNameOffsets_DoesNotThrashTheBufferedWindow()
        {
            const int objectCount = 20;
            const int spacing = 70_000; // > BufferedByteSource's 64 KiB window: each name lookup forces its own refill
            const int nameByteSize = 64; // plenty for the 4-byte length prefix + a short name

            var metadata = new ByteBufferWriter();
            AppendLeadingMetadata(metadata, "6000.3.13f1", 13, enableTypeTree: false);
            metadata.WriteInt32(1);
            AppendTypeEntry(metadata, false, new TypeEntrySpec { PersistentTypeId = 28, ScriptTypeIndex = -1 }); // Texture2D -- name-bearing
            metadata.WriteInt32(objectCount);

            var nameOffsets = new long[objectCount];
            for (var i = 0; i < objectCount; i++)
            {
                var byteStart = (long)(i + 1) * spacing;
                nameOffsets[i] = byteStart;
                AppendObjectEntry(metadata, pathId: i + 1, byteStart: byteStart, byteSize: nameByteSize, typeIndex: 0);
            }

            metadata.WriteInt32(0); // script types
            metadata.WriteInt32(0); // external references

            var metadataBytes = metadata.ToArray();
            const int headerLength = 48;
            var totalLength = (int)(nameOffsets[^1] + nameByteSize);
            var buffer = new byte[totalLength];

            var header = BuildHeader(version: 23, endianness: 0, metadataSize: (ulong)metadataBytes.Length,
                fileSize: (ulong)totalLength, dataOffset: 0, trailingByteCount: 0);
            Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            Buffer.BlockCopy(metadataBytes, 0, buffer, headerLength, metadataBytes.Length);

            for (var i = 0; i < objectCount; i++)
            {
                var nameBytes = new ByteBufferWriter().WriteString("Object" + i).ToArray();
                Buffer.BlockCopy(nameBytes, 0, buffer, (int)nameOffsets[i], nameBytes.Length);
            }

            var counting = new CountingByteSource(new InMemoryByteSource(buffer));
            var source = new BufferedByteSource(counting);

            var detected = SerializedFileDetector.TryDetect(source, out var info);

            Assert.That(detected, Is.True);
            Assert.That(info.MetadataParsed, Is.True);
            Assert.That(info.Objects.Count, Is.EqualTo(objectCount));
            Assert.That(info.Objects[5].Name, Is.EqualTo("Object5"));

            Assert.That(counting.ReadCallCount, Is.LessThanOrEqualTo(objectCount + 5));
        }

        [Test]
        public void TryDetect_Version22_ParsesObjectsAndExternalReferences()
        {
            // Same layout as the version-23 case above -- version 22 (real Unity 6000.3.x Player
            // builds, e.g. an Android Gradle export built with 6000.3.13f1) uses an identical
            // metadata layout, just a different version number in the header.
            var writer = new ByteBufferWriter();
            AppendLeadingMetadata(writer, "6000.3.13f1", 13, enableTypeTree: false);
            writer.WriteInt32(1);
            AppendTypeEntry(writer, false, new TypeEntrySpec { PersistentTypeId = 1, ScriptTypeIndex = -1 });
            writer.WriteInt32(1);
            AppendObjectEntry(writer, pathId: 1, byteStart: 0, byteSize: 64, typeIndex: 0);
            writer.WriteInt32(0);
            writer.WriteInt32(0);

            var buffer = WrapWithHeader(writer.ToArray(), version: 22, endianness: 0, dataOffset: 1000);

            var detected = SerializedFileDetector.TryDetect(new InMemoryByteSource(buffer), out var info);

            Assert.That(detected, Is.True);
            Assert.That(info.MetadataParsed, Is.True);
            Assert.That(info.Version, Is.EqualTo(22u));
            Assert.That(info.UnityVersion, Is.EqualTo("6000.3.13f1"));
            Assert.That(info.TargetPlatform, Is.EqualTo(13u));
            Assert.That(info.EnableTypeTree, Is.False);
            Assert.That(info.Objects.Count, Is.EqualTo(1));
            Assert.That((info.Objects[0].PathId, info.Objects[0].TypeId, info.Objects[0].ByteOffset, info.Objects[0].ByteSize),
                Is.EqualTo((1L, 1, 1000L, 64L)));
            Assert.That(info.ExternalReferences, Is.Empty);
        }

        [Test]
        public void TryDetect_Version23WithInlineTypeTreeBlobs_SkipsBlobsAndParsesObjectsCorrectly()
        {
            var writer = new ByteBufferWriter();
            AppendLeadingMetadata(writer, "6000.0.10f1", 19, enableTypeTree: true);
            writer.WriteInt32(2);
            AppendTypeEntry(writer, true, new TypeEntrySpec
            {
                PersistentTypeId = 1,
                ScriptTypeIndex = -1,
                TypeTreeNodeCount = 2,
                TypeTreeStringBufferSize = 10,
            });
            AppendTypeEntry(writer, true, new TypeEntrySpec
            {
                PersistentTypeId = 114,
                ScriptTypeIndex = -1,
                // Zero nodes and an empty string buffer -- extracted-to-external-store case.
            });
            writer.WriteInt32(2);
            AppendObjectEntry(writer, pathId: 2001, byteStart: 500, byteSize: 32, typeIndex: 0);
            AppendObjectEntry(writer, pathId: 2002, byteStart: 532, byteSize: 64, typeIndex: 1);
            writer.WriteInt32(0);
            writer.WriteInt32(0);

            var buffer = WrapWithHeader(writer.ToArray(), version: 23, endianness: 0, dataOffset: 0);

            var detected = SerializedFileDetector.TryDetect(new InMemoryByteSource(buffer), out var info);

            Assert.That(detected, Is.True);
            Assert.That(info.MetadataParsed, Is.True);
            Assert.That(info.EnableTypeTree, Is.True);
            Assert.That(info.IsMissingTypeTrees, Is.False);
            Assert.That(info.Objects.Count, Is.EqualTo(2));
            Assert.That((info.Objects[0].PathId, info.Objects[0].TypeId, info.Objects[0].ByteOffset, info.Objects[0].ByteSize),
                Is.EqualTo((2001L, 1, 500L, 32L)));
            Assert.That((info.Objects[1].PathId, info.Objects[1].TypeId, info.Objects[1].ByteOffset, info.Objects[1].ByteSize),
                Is.EqualTo((2002L, 114, 532L, 64L)));
            Assert.That(info.ExternalReferences.Count, Is.EqualTo(0));
        }

        [Test]
        public void TryDetect_VersionBelowSupported_MetadataNotParsedButHeaderFieldsPresent()
        {
            var buffer = BuildHeader(version: 21, endianness: 0, metadataSize: 0, fileSize: 48, dataOffset: 0, trailingByteCount: 0);

            var detected = SerializedFileDetector.TryDetect(new InMemoryByteSource(buffer), out var info);

            Assert.That(detected, Is.True);
            Assert.That(info.MetadataParsed, Is.False);
            Assert.That(info.MetadataParseError, Is.Not.Null.And.Not.Empty);
            Assert.That(info.Version, Is.EqualTo(21u));
            Assert.That(info.Objects, Is.Empty);
            Assert.That(info.ExternalReferences, Is.Empty);
        }

        [Test]
        public void TryDetect_VersionAboveSupported_MetadataNotParsedButHeaderFieldsPresent()
        {
            var buffer = BuildHeader(version: 30, endianness: 0, metadataSize: 0, fileSize: 48, dataOffset: 0, trailingByteCount: 0);

            var detected = SerializedFileDetector.TryDetect(new InMemoryByteSource(buffer), out var info);

            Assert.That(detected, Is.True);
            Assert.That(info.MetadataParsed, Is.False);
            Assert.That(info.MetadataParseError, Is.Not.Null.And.Not.Empty);
            Assert.That(info.Version, Is.EqualTo(30u));
        }

        [Test]
        public void TryDetect_UnterminatedUnityVersionString_MetadataNotParsedWithoutThrowing()
        {
            var metadataBytes = new ByteBufferWriter().WriteRawBytesNoPrefix(new byte[] { (byte)'a', (byte)'b', (byte)'c' }).ToArray();
            var buffer = WrapWithHeader(metadataBytes, version: 23, endianness: 0, dataOffset: 0);

            SerializedFileInfo info = default;
            Assert.DoesNotThrow(() => SerializedFileDetector.TryDetect(new InMemoryByteSource(buffer), out info));
            Assert.That(info.MetadataParsed, Is.False);
            Assert.That(info.MetadataParseError, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void TryDetect_EmptyUnityVersionString_MetadataNotParsedWithoutThrowing()
        {
            var metadataBytes = new ByteBufferWriter().WriteNullTerminatedString("").ToArray();
            var buffer = WrapWithHeader(metadataBytes, version: 23, endianness: 0, dataOffset: 0);

            var detected = SerializedFileDetector.TryDetect(new InMemoryByteSource(buffer), out var info);

            Assert.That(detected, Is.True);
            Assert.That(info.MetadataParsed, Is.False);
            Assert.That(info.MetadataParseError, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void TryDetect_NegativeTypeCount_Throws()
        {
            var writer = new ByteBufferWriter();
            AppendLeadingMetadata(writer, "6000.3.0f1", 5, enableTypeTree: false);
            writer.WriteInt32(-1); // corrupt: negative typeCount
            var buffer = WrapWithHeader(writer.ToArray(), version: 23, endianness: 0, dataOffset: 0);

            Assert.Throws<ArtifactInspectorException>(() => SerializedFileDetector.TryDetect(new InMemoryByteSource(buffer), out _));
        }

        [Test]
        public void TryDetect_TypeCountClaimsMoreEntriesThanRemainingBytesSupport_Throws()
        {
            var writer = new ByteBufferWriter();
            AppendLeadingMetadata(writer, "6000.3.0f1", 5, enableTypeTree: false);
            writer.WriteInt32(1); // claims one type entry, but the buffer ends right here
            var buffer = WrapWithHeader(writer.ToArray(), version: 23, endianness: 0, dataOffset: 0);

            Assert.Throws<ArtifactInspectorException>(() => SerializedFileDetector.TryDetect(new InMemoryByteSource(buffer), out _));
        }

        [Test]
        public void TryDetect_TypeCountFarExceedsRemainingBytes_ThrowsCleanExceptionInsteadOfHugeAllocation()
        {
            // A small file with a huge count field here couldtrigger a multi-gigabyte allocation attempt instead of failing cleanly.
            var writer = new ByteBufferWriter();
            AppendLeadingMetadata(writer, "6000.3.0f1", 5, enableTypeTree: false);
            writer.WriteInt32(int.MaxValue); // corrupt: absurd typeCount for a tiny buffer
            var buffer = WrapWithHeader(writer.ToArray(), version: 23, endianness: 0, dataOffset: 0);

            Assert.Throws<ArtifactInspectorException>(() => SerializedFileDetector.TryDetect(new InMemoryByteSource(buffer), out _));
        }

        [Test]
        public void IsMissingTypeTrees_EnableTypeTreeFalse_ReturnsTrue()
        {
            var metadataBytes = new ByteBufferWriter();
            AppendLeadingMetadata(metadataBytes, "6000.3.0f1", 5, enableTypeTree: false);
            var buffer = WrapWithHeader(metadataBytes.ToArray(), version: 23, endianness: 0, dataOffset: 0);

            Assert.That(SerializedFileDetector.IsMissingTypeTrees(new InMemoryByteSource(buffer)), Is.True);
        }

        [Test]
        public void IsMissingTypeTrees_EnableTypeTreeTrue_ReturnsFalse()
        {
            var metadataBytes = new ByteBufferWriter();
            AppendLeadingMetadata(metadataBytes, "6000.3.0f1", 5, enableTypeTree: true);
            var buffer = WrapWithHeader(metadataBytes.ToArray(), version: 23, endianness: 0, dataOffset: 0);

            Assert.That(SerializedFileDetector.IsMissingTypeTrees(new InMemoryByteSource(buffer)), Is.False);
        }

        [Test]
        public void IsMissingTypeTrees_VersionOutOfRange_ReturnsFalse()
        {
            var buffer = BuildHeader(version: 21, endianness: 0, metadataSize: 0, fileSize: 48, dataOffset: 0, trailingByteCount: 0);

            Assert.That(SerializedFileDetector.IsMissingTypeTrees(new InMemoryByteSource(buffer)), Is.False);
        }

        [Test]
        public void IsMissingTypeTrees_NotASerializedFile_ReturnsFalse()
        {
            var buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };

            Assert.That(SerializedFileDetector.IsMissingTypeTrees(new InMemoryByteSource(buffer)), Is.False);
        }

        [Test]
        public void IsMissingTypeTrees_CorruptionOnlyInExtendedRegionItNeverReaches_DoesNotThrowAndStillReportsCorrectly()
        {
            var writer = new ByteBufferWriter();
            AppendLeadingMetadata(writer, "6000.3.0f1", 5, enableTypeTree: false);
            writer.WriteInt32(-1); // corrupt extended-region data IsMissingTypeTrees never reads
            var buffer = WrapWithHeader(writer.ToArray(), version: 23, endianness: 0, dataOffset: 0);

            var result = false;
            Assert.DoesNotThrow(() => result = SerializedFileDetector.IsMissingTypeTrees(new InMemoryByteSource(buffer)));
            Assert.That(result, Is.True);
        }

        [Test]
        public void TryDetect_FilePathOverload_MatchesByteSourceOverload()
        {
            var writer = new ByteBufferWriter();
            AppendLeadingMetadata(writer, "6000.3.0f1", 5, enableTypeTree: false);
            writer.WriteInt32(0).WriteInt32(0).WriteInt32(0).WriteInt32(0);
            var buffer = WrapWithHeader(writer.ToArray(), version: 23, endianness: 0, dataOffset: 0);

            TempFile.WithContent(buffer, path =>
            {
                var detected = SerializedFileDetector.TryDetect(path, out var info);

                Assert.That(detected, Is.True);
                Assert.That(info.MetadataParsed, Is.True);
                Assert.That(info.EnableTypeTree, Is.False);
            });
        }

        [Test]
        public void IsMissingTypeTrees_FilePathOverload_MatchesByteSourceOverload()
        {
            var metadataBytes = new ByteBufferWriter();
            AppendLeadingMetadata(metadataBytes, "6000.3.0f1", 5, enableTypeTree: false);
            var buffer = WrapWithHeader(metadataBytes.ToArray(), version: 23, endianness: 0, dataOffset: 0);

            TempFile.WithContent(buffer, path => Assert.That(SerializedFileDetector.IsMissingTypeTrees(path), Is.True));
        }

        [Test]
        public void TryDetect_NullFilePath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => SerializedFileDetector.TryDetect((string)null, out _));
        }

        [Test]
        public void IsMissingTypeTrees_NullFilePath_ThrowsArgumentNullException()
        {
            // A bad path is never swallowed by the string-path convenience overloads, only
            // format-level detection failures are -- consistent with TryDetect(string, ...) above.
            Assert.Throws<ArgumentNullException>(() => SerializedFileDetector.IsMissingTypeTrees((string)null));
        }

        [Test]
        public void IsMissingTypeTrees_FilePathDoesNotExist_ThrowsInsteadOfReturningFalse()
        {
            var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".missing");

            Assert.Throws<FileNotFoundException>(() => SerializedFileDetector.IsMissingTypeTrees(missingPath));
        }
    }
}
