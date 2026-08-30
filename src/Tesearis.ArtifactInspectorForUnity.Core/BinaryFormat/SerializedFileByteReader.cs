using System;
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
            var buffer = ReadExact(1);
            return buffer[0];
        }

        internal short ReadInt16()
        {
            return (short)ReadUInt16();
        }

        internal ushort ReadUInt16()
        {
            var buffer = ReadExact(2);
            var value = BitConverter.ToUInt16(buffer, 0);
            return _swap ? EndianUtility.SwapUInt16(value) : value;
        }

        internal int ReadInt32()
        {
            return (int)ReadUInt32();
        }

        internal uint ReadUInt32()
        {
            var buffer = ReadExact(4);
            var value = BitConverter.ToUInt32(buffer, 0);
            return _swap ? EndianUtility.SwapUInt32(value) : value;
        }

        internal long ReadInt64()
        {
            return (long)ReadUInt64();
        }

        internal ulong ReadUInt64()
        {
            var buffer = ReadExact(8);
            var value = BitConverter.ToUInt64(buffer, 0);
            return _swap ? EndianUtility.SwapUInt64(value) : value;
        }

        /// <summary>Reads a null-terminated ASCII string, advancing past the terminator.</summary>
        internal string ReadNullTerminatedAsciiString()
        {
            var sb = new StringBuilder();
            byte b;
            while ((b = ReadByte()) != 0)
            {
                sb.Append((char)b);
            }

            return sb.ToString();
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

        private byte[] ReadExact(int count)
        {
            var buffer = new byte[count];
            var read = _source.Read(Position, buffer, 0, count);
            if (read != count)
            {
                throw new ArtifactInspectorException(
                    "Unexpected end of data while reading " + count + " byte(s) at offset " + Position + ".");
            }

            Position += count;
            return buffer;
        }
    }
}
