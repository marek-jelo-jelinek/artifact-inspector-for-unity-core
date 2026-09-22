using System;
using System.Collections.Generic;
using System.Text;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat
{
    /// <summary>
    /// Swap-aware sequential cursor reader over an <see cref="IRandomAccessByteSource"/>, used to
    /// walk a SerializedFile's metadata section field by field. Every primitive read throws
    /// <see cref="ArtifactInspectorException"/> on a short read (unexpected end of data) rather than
    /// returning a partial/garbage value.
    /// </summary>
    internal sealed class SerializedFileByteReader
    {
        private readonly IRandomAccessByteSource _source;
        private readonly bool _swap;
        private readonly byte[] _primitiveBuffer = new byte[8];
        private readonly byte[] _utf8Buffer = new byte[64];

        internal SerializedFileByteReader(IRandomAccessByteSource source, long startPosition, bool swap)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            Position = startPosition;
            _swap = swap;
        }

        internal long Position { get; set; }

        internal long RemainingBytes => _source.Length - Position;

        internal byte ReadByte()
        {
            ReadBuffer(1);
            return _primitiveBuffer[0];
        }

        internal short ReadInt16()
        {
            return (short)ReadUInt16();
        }

        internal ushort ReadUInt16()
        {
            ReadBuffer(2);
            var value = BitConverter.ToUInt16(_primitiveBuffer, 0);
            return _swap ? EndianUtility.SwapUInt16(value) : value;
        }

        internal int ReadInt32()
        {
            return (int)ReadUInt32();
        }

        internal uint ReadUInt32()
        {
            ReadBuffer(4);
            var value = BitConverter.ToUInt32(_primitiveBuffer, 0);
            return _swap ? EndianUtility.SwapUInt32(value) : value;
        }

        internal long ReadInt64()
        {
            return (long)ReadUInt64();
        }

        internal ulong ReadUInt64()
        {
            ReadBuffer(8);
            var value = BitConverter.ToUInt64(_primitiveBuffer, 0);
            return _swap ? EndianUtility.SwapUInt64(value) : value;
        }

        /// <summary>Reads a null-terminated UTF-8 string, advancing past the terminator.</summary>
        internal string ReadNullTerminatedUtf8String()
        {
            List<byte> bytes = null;
            while (true)
            {
                var remaining = _source.Length - Position;
                if (remaining <= 0)
                {
                    throw new ArtifactInspectorException(
                        "Unexpected end of data while reading 1 byte(s) at offset " + Position + ".");
                }

                var toRead = (int)Math.Min(_utf8Buffer.Length, remaining);
                var read = _source.Read(Position, _utf8Buffer, 0, toRead);
                if (read <= 0)
                {
                    throw new ArtifactInspectorException(
                        "Unexpected end of data while reading 1 byte(s) at offset " + Position + ".");
                }

                var nullIndex = Array.IndexOf(_utf8Buffer, (byte)0, 0, read);
                if (nullIndex >= 0)
                {
                    Position += nullIndex + 1;
                    if (bytes == null)
                    {
                        return nullIndex == 0 ? string.Empty : Encoding.UTF8.GetString(_utf8Buffer, 0, nullIndex);
                    }

                    for (var i = 0; i < nullIndex; i++)
                    {
                        bytes.Add(_utf8Buffer[i]);
                    }

                    return Encoding.UTF8.GetString(bytes.ToArray());
                }

                bytes ??= new List<byte>();
                for (var i = 0; i < read; i++)
                {
                    bytes.Add(_utf8Buffer[i]);
                }

                Position += read;
            }
        }

        /// <summary>Advances the cursor to the next 4-byte boundary measured from baseOffset.</summary>
        internal void AlignTo4(long baseOffset)
        {
            var relative = Position - baseOffset;
            var aligned = (relative + 3) & ~3L;
            Position = baseOffset + aligned;
        }

        internal void Skip(long byteCount)
        {
            if (byteCount < 0) throw new ArgumentOutOfRangeException(nameof(byteCount), byteCount, "Byte count must not be negative.");
            Position += byteCount;
        }

        private void ReadBuffer(int count)
        {
            var read = _source.Read(Position, _primitiveBuffer, 0, count);
            if (read != count)
            {
                throw new ArtifactInspectorException(
                    "Unexpected end of data while reading " + count + " byte(s) at offset " + Position + ".");
            }

            Position += count;
        }
    }
}
