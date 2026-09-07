using System.Text;
using Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.BinaryFormat
{
    [TestFixture]
    public class StrippedObjectNameReaderTests
    {
        private const int Texture2DClassId = 28;
        private const int GameObjectClassId = 1;
        private const int MonoBehaviourClassId = 114;

        [Test]
        public void TryReadName_WhitelistedClass_ReadsLeadingLengthPrefixedString()
        {
            var buffer = new ByteBufferWriter().WriteString("MyTexture").ToArray();

            var name = StrippedObjectNameReader.TryReadName(new InMemoryByteSource(buffer), Texture2DClassId,
                byteOffset: 0, byteSize: buffer.Length, bigEndian: false);

            Assert.That(name, Is.EqualTo("MyTexture"));
        }

        [Test]
        public void TryReadName_NonWhitelistedClass_NeverReadsEvenWithAValidLeadingString()
        {
            var buffer = new ByteBufferWriter().WriteString("LooksValid").ToArray();

            var name = StrippedObjectNameReader.TryReadName(new InMemoryByteSource(buffer), GameObjectClassId,
                byteOffset: 0, byteSize: buffer.Length, bigEndian: false);

            Assert.That(name, Is.Null);
        }

        [Test]
        public void TryReadName_MonoBehaviour_ReadsStringAtOffset28()
        {
            var writer = new ByteBufferWriter().WriteZeros(28).WriteString("MyBehaviour");
            var buffer = writer.ToArray();

            var name = StrippedObjectNameReader.TryReadName(new InMemoryByteSource(buffer), MonoBehaviourClassId,
                byteOffset: 0, byteSize: buffer.Length, bigEndian: false);

            Assert.That(name, Is.EqualTo("MyBehaviour"));
        }

        [Test]
        public void TryReadName_ZeroLength_ReturnsNull()
        {
            var buffer = new ByteBufferWriter().WriteInt32(0).ToArray();

            var name = StrippedObjectNameReader.TryReadName(new InMemoryByteSource(buffer), Texture2DClassId,
                byteOffset: 0, byteSize: buffer.Length, bigEndian: false);

            Assert.That(name, Is.Null);
        }

        [Test]
        public void TryReadName_LengthExceedsAvailableBytes_ReturnsNull()
        {
            var buffer = new ByteBufferWriter().WriteInt32(1000).WriteZeros(8).ToArray();

            var name = StrippedObjectNameReader.TryReadName(new InMemoryByteSource(buffer), Texture2DClassId,
                byteOffset: 0, byteSize: buffer.Length, bigEndian: false);

            Assert.That(name, Is.Null);
        }

        [Test]
        public void TryReadName_LengthExceedsMaxPlausibleNameLength_ReturnsNullEvenIfBytesArePresent()
        {
            var payload = new byte[600];
            var buffer = new ByteBufferWriter().WriteInt32(600).WriteRawBytesNoPrefix(payload).ToArray();

            var name = StrippedObjectNameReader.TryReadName(new InMemoryByteSource(buffer), Texture2DClassId,
                byteOffset: 0, byteSize: buffer.Length, bigEndian: false);

            Assert.That(name, Is.Null);
        }

        [Test]
        public void TryReadName_InvalidUtf8Bytes_ReturnsNullInsteadOfThrowing()
        {
            var invalidUtf8 = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC };
            var buffer = new ByteBufferWriter().WriteInt32(invalidUtf8.Length).WriteRawBytesNoPrefix(invalidUtf8).ToArray();

            string name = null;
            Assert.DoesNotThrow(() => name = StrippedObjectNameReader.TryReadName(new InMemoryByteSource(buffer),
                Texture2DClassId, byteOffset: 0, byteSize: buffer.Length, bigEndian: false));
            Assert.That(name, Is.Null);
        }

        [Test]
        public void TryReadName_ControlCharactersInDecodedString_ReturnsNull()
        {
            var bytes = Encoding.UTF8.GetBytes("badname");
            var buffer = new ByteBufferWriter().WriteInt32(bytes.Length).WriteRawBytesNoPrefix(bytes).ToArray();

            var name = StrippedObjectNameReader.TryReadName(new InMemoryByteSource(buffer), Texture2DClassId,
                byteOffset: 0, byteSize: buffer.Length, bigEndian: false);

            Assert.That(name, Is.Null);
        }

        [Test]
        public void TryReadName_TruncatedSource_ReturnsNullInsteadOfThrowing()
        {
            var buffer = new byte[] { 1, 2 }; // shorter than the 4-byte length prefix itself

            string name = null;
            Assert.DoesNotThrow(() => name = StrippedObjectNameReader.TryReadName(new InMemoryByteSource(buffer),
                Texture2DClassId, byteOffset: 0, byteSize: buffer.Length, bigEndian: false));
            Assert.That(name, Is.Null);
        }

        [Test]
        public void TryReadName_BigEndianLengthPrefix_DecodesCorrectly()
        {
            var content = Encoding.UTF8.GetBytes("BigEndianName");
            var buffer = new ByteBufferWriter().WriteUInt32BigEndian((uint)content.Length).WriteRawBytesNoPrefix(content).ToArray();

            var name = StrippedObjectNameReader.TryReadName(new InMemoryByteSource(buffer), Texture2DClassId,
                byteOffset: 0, byteSize: buffer.Length, bigEndian: true);

            Assert.That(name, Is.EqualTo("BigEndianName"));
        }
    }
}
