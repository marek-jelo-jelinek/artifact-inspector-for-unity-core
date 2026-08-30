using Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.BinaryFormat
{
    /// <summary>
    /// Exercises <see cref="SerializedFileHeaderParser"/> directly, in isolation from metadata-section
    /// parsing -- every fixture here is a header plus zero-filled filler bytes standing in for a
    /// metadata section whose content is never read by this parser.
    /// </summary>
    [TestFixture]
    public class SerializedFileHeaderParserTests
    {
        [Test]
        public void TryParse_LittleEndian_ParsesAllFields()
        {
            var buffer = SerializedFileTestFixtures.BuildHeader(version: 23, endianness: 0, metadataSize: 100, fileSize: 148, dataOffset: 148, trailingByteCount: 100);

            var parsed = SerializedFileHeaderParser.TryParse(new InMemoryByteSource(buffer), out var header);

            Assert.That(parsed, Is.True);
            Assert.That(header.Version, Is.EqualTo(23u));
            Assert.That(header.MetadataSize, Is.EqualTo(100UL));
            Assert.That(header.FileSize, Is.EqualTo(148UL));
            Assert.That(header.DataOffset, Is.EqualTo(148UL));
            Assert.That(header.IsBigEndian, Is.False);
            Assert.That(header.MetadataStartOffset, Is.EqualTo(48));
        }

        [Test]
        public void TryParse_BigEndianDataSection_ParsesAllFields()
        {
            var buffer = SerializedFileTestFixtures.BuildHeader(version: 23, endianness: 1, metadataSize: 50, fileSize: 98, dataOffset: 98, trailingByteCount: 50);

            var parsed = SerializedFileHeaderParser.TryParse(new InMemoryByteSource(buffer), out var header);

            Assert.That(parsed, Is.True);
            Assert.That(header.Version, Is.EqualTo(23u));
            Assert.That(header.MetadataSize, Is.EqualTo(50UL));
            Assert.That(header.FileSize, Is.EqualTo(98UL));
            Assert.That(header.DataOffset, Is.EqualTo(98UL));
            Assert.That(header.IsBigEndian, Is.True); // the data section's endianness, independent of the header's own big-endian-on-disk encoding
            Assert.That(header.MetadataStartOffset, Is.EqualTo(48));
        }

        [Test]
        public void TryParse_FileSizeSentinel_ReportsUnknownAsUlongMaxValue()
        {
            var buffer = SerializedFileTestFixtures.BuildHeader(version: 23, endianness: 0, metadataSize: 0, fileSize: ulong.MaxValue, dataOffset: 0, trailingByteCount: 0);

            var parsed = SerializedFileHeaderParser.TryParse(new InMemoryByteSource(buffer), out var header);

            Assert.That(parsed, Is.True);
            Assert.That(header.FileSize, Is.EqualTo(ulong.MaxValue));
        }

        [Test]
        public void TryParse_TooShortBuffer_ReturnsFalse()
        {
            var buffer = new byte[10]; // below the 48-byte header minimum

            Assert.That(SerializedFileHeaderParser.TryParse(new InMemoryByteSource(buffer), out _), Is.False);
        }

        [Test]
        public void TryParse_MetadataSizeSentinel_ReturnsFalse()
        {
            var buffer = SerializedFileTestFixtures.BuildHeader(version: 23, endianness: 0, metadataSize: ulong.MaxValue, fileSize: 48, dataOffset: 0, trailingByteCount: 0);

            Assert.That(SerializedFileHeaderParser.TryParse(new InMemoryByteSource(buffer), out _), Is.False);
        }

        [Test]
        public void TryParse_DataOffsetExceedsFileSize_ReturnsFalse()
        {
            var buffer = SerializedFileTestFixtures.BuildHeader(version: 23, endianness: 0, metadataSize: 0, fileSize: 48, dataOffset: 1000, trailingByteCount: 0);

            Assert.That(SerializedFileHeaderParser.TryParse(new InMemoryByteSource(buffer), out _), Is.False);
        }

        [Test]
        public void TryParse_FileSizeFarBeyondSourceLength_ReturnsFalse()
        {
            var buffer = SerializedFileTestFixtures.BuildHeader(version: 23, endianness: 0, metadataSize: 0, fileSize: 1_000_000, dataOffset: 0, trailingByteCount: 0);

            Assert.That(SerializedFileHeaderParser.TryParse(new InMemoryByteSource(buffer), out _), Is.False);
        }

        [Test]
        public void TryParse_MetadataSizeExceedsSourceLength_ReturnsFalse()
        {
            var buffer = SerializedFileTestFixtures.BuildHeader(version: 23, endianness: 0, metadataSize: 1_000_000, fileSize: 48, dataOffset: 0, trailingByteCount: 0);

            Assert.That(SerializedFileHeaderParser.TryParse(new InMemoryByteSource(buffer), out _), Is.False);
        }

        [Test]
        public void TryParse_RandomNonHeaderBytes_ReturnsFalse()
        {
            var buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };

            Assert.That(SerializedFileHeaderParser.TryParse(new InMemoryByteSource(buffer), out _), Is.False);
        }

        [Test]
        public void TryParse_VersionOutOfSaneRange_ReturnsFalse()
        {
            // Neither the value as-is nor byte-swapped falls within the 1-50 sane range.
            var buffer = SerializedFileTestFixtures.BuildHeader(version: 1_000_000, endianness: 0, metadataSize: 0, fileSize: 48, dataOffset: 0, trailingByteCount: 0);

            Assert.That(SerializedFileHeaderParser.TryParse(new InMemoryByteSource(buffer), out _), Is.False);
        }

        [Test]
        public void TryParse_YamlMagicBytes_ReturnsFalse_AndYamlDetectorAgreesOnTheSameBuffer()
        {
            var buffer = System.Text.Encoding.ASCII.GetBytes("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!1 &1\n");

            Assert.That(SerializedFileHeaderParser.TryParse(new InMemoryByteSource(buffer), out _), Is.False);
            Assert.That(YamlSerializedFileDetector.IsYamlSerializedFile(new InMemoryByteSource(buffer)), Is.True);
        }
    }
}
