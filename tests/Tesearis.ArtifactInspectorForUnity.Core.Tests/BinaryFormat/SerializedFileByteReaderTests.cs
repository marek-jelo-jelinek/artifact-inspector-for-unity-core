using Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.BinaryFormat
{
    [TestFixture]
    public class SerializedFileByteReaderTests
    {
        [Test]
        public void ReadUInt32_NoSwap_ReturnsValueAsWritten()
        {
            var buffer = new ByteBufferWriter().WriteUInt32(0x11223344u).ToArray();
            var reader = new SerializedFileByteReader(new InMemoryByteSource(buffer), 0, swap: false);

            Assert.That(reader.ReadUInt32(), Is.EqualTo(0x11223344u));
        }

        [Test]
        public void ReadUInt32_Swap_ReturnsByteSwappedValue()
        {
            var buffer = new ByteBufferWriter().WriteUInt32(0x11223344u).ToArray();
            var reader = new SerializedFileByteReader(new InMemoryByteSource(buffer), 0, swap: true);

            Assert.That(reader.ReadUInt32(), Is.EqualTo(0x44332211u));
        }

        [Test]
        public void ReadInt32_Swap_ReturnsByteSwappedValue()
        {
            var buffer = new ByteBufferWriter().WriteInt32(1).ToArray();
            var reader = new SerializedFileByteReader(new InMemoryByteSource(buffer), 0, swap: true);

            Assert.That(reader.ReadInt32(), Is.EqualTo(unchecked((int)0x01000000)));
        }

        [Test]
        public void ReadUInt16_Swap_ReturnsByteSwappedValue()
        {
            var buffer = new ByteBufferWriter().WriteUInt16(0x1122).ToArray();
            var reader = new SerializedFileByteReader(new InMemoryByteSource(buffer), 0, swap: true);

            Assert.That(reader.ReadUInt16(), Is.EqualTo((ushort)0x2211));
        }

        [Test]
        public void ReadInt16_NoSwap_ReturnsValueAsWritten()
        {
            var buffer = new ByteBufferWriter().WriteInt16(-5).ToArray();
            var reader = new SerializedFileByteReader(new InMemoryByteSource(buffer), 0, swap: false);

            Assert.That(reader.ReadInt16(), Is.EqualTo((short)-5));
        }

        [Test]
        public void ReadUInt64_Swap_ReturnsByteSwappedValue()
        {
            var buffer = new ByteBufferWriter().WriteUInt64(0x0102030405060708UL).ToArray();
            var reader = new SerializedFileByteReader(new InMemoryByteSource(buffer), 0, swap: true);

            Assert.That(reader.ReadUInt64(), Is.EqualTo(0x0807060504030201UL));
        }

        [Test]
        public void ReadInt64_NoSwap_ReturnsValueAsWritten()
        {
            var buffer = new ByteBufferWriter().WriteInt64(-42L).ToArray();
            var reader = new SerializedFileByteReader(new InMemoryByteSource(buffer), 0, swap: false);

            Assert.That(reader.ReadInt64(), Is.EqualTo(-42L));
        }

        [Test]
        public void ReadByte_AdvancesPositionByOne()
        {
            var buffer = new byte[] { 0xAB, 0xCD };
            var reader = new SerializedFileByteReader(new InMemoryByteSource(buffer), 0, swap: false);

            Assert.That(reader.ReadByte(), Is.EqualTo(0xAB));
            Assert.That(reader.ReadByte(), Is.EqualTo(0xCD));
        }

        [Test]
        public void ReadNullTerminatedAsciiString_HappyPath_ReadsStringAndAdvancesPastTerminator()
        {
            var buffer = new ByteBufferWriter().WriteNullTerminatedString("hi").WriteByte(0x99).ToArray();
            var reader = new SerializedFileByteReader(new InMemoryByteSource(buffer), 0, swap: false);

            Assert.That(reader.ReadNullTerminatedAsciiString(), Is.EqualTo("hi"));
            Assert.That(reader.ReadByte(), Is.EqualTo(0x99)); // proves the cursor stopped right after the terminator
        }

        [Test]
        public void ReadNullTerminatedAsciiString_EmptyString_ReturnsEmpty()
        {
            var buffer = new ByteBufferWriter().WriteNullTerminatedString("").ToArray();
            var reader = new SerializedFileByteReader(new InMemoryByteSource(buffer), 0, swap: false);

            Assert.That(reader.ReadNullTerminatedAsciiString(), Is.EqualTo(""));
        }

        [Test]
        public void ReadNullTerminatedAsciiString_NoTerminatorBeforeEndOfSource_Throws()
        {
            var buffer = new byte[] { (byte)'a', (byte)'b', (byte)'c' }; // no 0x00 anywhere
            var reader = new SerializedFileByteReader(new InMemoryByteSource(buffer), 0, swap: false);

            Assert.Throws<ArtifactInspectorException>(() => reader.ReadNullTerminatedAsciiString());
        }

        [Test]
        public void ReadUInt32_ShortRead_Throws()
        {
            var buffer = new byte[] { 1, 2 }; // only 2 bytes, need 4
            var reader = new SerializedFileByteReader(new InMemoryByteSource(buffer), 0, swap: false);

            Assert.Throws<ArtifactInspectorException>(() => reader.ReadUInt32());
        }

        [Test]
        public void AlignTo4_AtVariousPositions_RoundsUpToNextBoundaryRelativeToBase()
        {
            var buffer = new byte[16];
            var reader = new SerializedFileByteReader(new InMemoryByteSource(buffer), 1, swap: false);

            reader.AlignTo4(0); // relative offset 1 -> aligned relative 4 -> absolute 4
            Assert.That(reader.Position, Is.EqualTo(4L));

            reader.AlignTo4(0); // already aligned -> no change
            Assert.That(reader.Position, Is.EqualTo(4L));

            reader.Position = 5;
            reader.AlignTo4(2); // relative offset 3 -> aligned relative 4 -> absolute 6
            Assert.That(reader.Position, Is.EqualTo(6L));
        }

        [Test]
        public void Skip_AdvancesPositionByByteCount()
        {
            var buffer = new byte[16];
            var reader = new SerializedFileByteReader(new InMemoryByteSource(buffer), 3, swap: false);

            reader.Skip(5);

            Assert.That(reader.Position, Is.EqualTo(8L));
        }

        [Test]
        public void SwapUInt16_ReversesByteOrder()
        {
            Assert.That(EndianUtility.SwapUInt16(0x1122), Is.EqualTo((ushort)0x2211));
        }

        [Test]
        public void SwapUInt32_ReversesByteOrder()
        {
            Assert.That(EndianUtility.SwapUInt32(0x11223344u), Is.EqualTo(0x44332211u));
        }

        [Test]
        public void SwapUInt64_ReversesByteOrder()
        {
            Assert.That(EndianUtility.SwapUInt64(0x0102030405060708UL), Is.EqualTo(0x0807060504030201UL));
        }
    }
}
