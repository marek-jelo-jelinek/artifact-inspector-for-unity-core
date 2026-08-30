using System;
using System.Collections.Generic;
using System.Text;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport
{
    /// <summary>A tiny fluent little-endian byte buffer builder for hand-crafting test fixtures.</summary>
    internal sealed class ByteBufferWriter
    {
        private readonly List<byte> _bytes = [];

        internal int Length => _bytes.Count;

        internal ByteBufferWriter WriteInt16(short value)
        {
            _bytes.AddRange(BitConverter.GetBytes(value));
            return this;
        }

        internal ByteBufferWriter WriteUInt16(ushort value)
        {
            _bytes.AddRange(BitConverter.GetBytes(value));
            return this;
        }

        internal ByteBufferWriter WriteInt32(int value)
        {
            _bytes.AddRange(BitConverter.GetBytes(value));
            return this;
        }

        internal ByteBufferWriter WriteUInt32(uint value)
        {
            _bytes.AddRange(BitConverter.GetBytes(value));
            return this;
        }

        /// <summary>Writes a uint32 in big-endian order, e.g. for a SerializedFile header's on-disk byte order.</summary>
        internal ByteBufferWriter WriteUInt32BigEndian(uint value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            _bytes.AddRange(bytes);
            return this;
        }

        /// <summary>Writes a uint64 in big-endian order, e.g. for a SerializedFile header's on-disk byte order.</summary>
        internal ByteBufferWriter WriteUInt64BigEndian(ulong value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            _bytes.AddRange(bytes);
            return this;
        }

        internal ByteBufferWriter WriteUInt64(ulong value)
        {
            _bytes.AddRange(BitConverter.GetBytes(value));
            return this;
        }

        internal ByteBufferWriter WriteInt64(long value)
        {
            _bytes.AddRange(BitConverter.GetBytes(value));
            return this;
        }

        internal ByteBufferWriter WriteSingle(float value)
        {
            _bytes.AddRange(BitConverter.GetBytes(value));
            return this;
        }

        internal ByteBufferWriter WriteByte(byte value)
        {
            _bytes.Add(value);
            return this;
        }

        internal ByteBufferWriter WriteSByte(sbyte value)
        {
            _bytes.Add(unchecked((byte)value));
            return this;
        }

        internal ByteBufferWriter WriteDouble(double value)
        {
            _bytes.AddRange(BitConverter.GetBytes(value));
            return this;
        }

        internal ByteBufferWriter WriteString(string value)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(value);
            WriteInt32(utf8.Length);
            _bytes.AddRange(utf8);
            return this;
        }

        internal ByteBufferWriter WriteRawBytes(byte[] value)
        {
            WriteInt32(value.Length);
            _bytes.AddRange(value);
            return this;
        }

        /// <summary>Appends bytes with no length prefix -- for hand-placed padding/skip regions or blob stand-ins.</summary>
        internal ByteBufferWriter WriteRawBytesNoPrefix(byte[] value)
        {
            _bytes.AddRange(value);
            return this;
        }

        /// <summary>Writes UTF8 bytes followed by a single 0x00 terminator, with no length prefix.</summary>
        internal ByteBufferWriter WriteNullTerminatedString(string value)
        {
            _bytes.AddRange(Encoding.UTF8.GetBytes(value));
            _bytes.Add(0);
            return this;
        }

        internal ByteBufferWriter WriteZeros(int count)
        {
            for (var i = 0; i < count; i++) _bytes.Add(0);
            return this;
        }

        /// <summary>Pads with zero bytes up to the next 4-byte boundary relative to the start of this buffer.</summary>
        internal ByteBufferWriter AlignTo4()
        {
            while (_bytes.Count % 4 != 0) _bytes.Add(0);
            return this;
        }

        internal byte[] ToArray()
        {
            return [.. _bytes];
        }
    }
}
