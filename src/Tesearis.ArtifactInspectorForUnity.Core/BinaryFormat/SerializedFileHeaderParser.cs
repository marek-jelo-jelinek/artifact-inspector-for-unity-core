using System;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat
{
    /// <summary>
    /// Sniffs and validates a Unity SerializedFile header directly off bytes, with no TypeTree or
    /// native call involved. Only the modern header layout used by Unity 6000.3+ (and, before it,
    /// every version back to 22) is supported: 48 bytes, 64-bit offsets/sizes, endianness byte at
    /// offset 40.
    ///
    /// The header itself is always stored big-endian on disk, independent of the m_Endianness byte,
    /// which instead describes the endianness of the metadata/data sections that follow the header.
    /// </summary>
    internal static class SerializedFileHeaderParser
    {
        private const int ModernHeaderSize = 48;

        private const uint MinSaneVersion = 1;
        private const uint MaxSaneVersion = 50;

        private const byte LittleEndian = 0;
        private const byte BigEndian = 1;

        internal static bool TryParse(IRandomAccessByteSource source, out SerializedFileHeader header)
        {
            header = default;

            if (source == null) return false;

            try
            {
                if (source.Length < ModernHeaderSize) return false;

                var buffer = new byte[ModernHeaderSize];
                var bytesRead = source.Read(0, buffer, 0, ModernHeaderSize);
                if (bytesRead < ModernHeaderSize) return false;

                // The version field sits at byte offset 8. The header is always big-endian on disk,
                // so try a host-endian read first and fall back to the byte-swapped interpretation.
                var versionAsIs = BitConverter.ToUInt32(buffer, 8);
                var versionSwapped = EndianUtility.SwapUInt32(versionAsIs);

                uint version;
                bool needsSwap;
                if (versionAsIs >= MinSaneVersion && versionAsIs <= MaxSaneVersion)
                {
                    version = versionAsIs;
                    needsSwap = false;
                }
                else if (versionSwapped >= MinSaneVersion && versionSwapped <= MaxSaneVersion)
                {
                    version = versionSwapped;
                    needsSwap = true;
                }
                else
                {
                    return false;
                }

                var endianness = buffer[40];
                if (endianness != LittleEndian && endianness != BigEndian) return false;

                var metadataSize = ReadUInt64(buffer, 16, needsSwap);
                var fileSize = ReadUInt64(buffer, 24, needsSwap);
                var dataOffset = ReadUInt64(buffer, 32, needsSwap);

                // All-ones (ulong.MaxValue) is Unity's on-disk sentinel for "size not populated". fileSize
                // legitimately carries it to mean "unknown", so the two checks below skip validation against
                // it rather than rejecting the header. 
                if (metadataSize == ulong.MaxValue) return false;
                if (fileSize != ulong.MaxValue && dataOffset > fileSize) return false;
                if (fileSize != ulong.MaxValue && fileSize > (ulong)source.Length + 1024) return false;
                if (metadataSize > (ulong)source.Length) return false;

                header = new SerializedFileHeader(version, fileSize, metadataSize, dataOffset,
                    endianness == BigEndian, ModernHeaderSize);
                return true;
            }
            catch
            {
                // Any unexpected failure while reading/parsing means this isn't a valid, or at least
                // not a safely-parseable, SerializedFile header.
                return false;
            }
        }

        private static ulong ReadUInt64(byte[] buffer, int offset, bool swap)
        {
            var value = BitConverter.ToUInt64(buffer, offset);
            return swap ? EndianUtility.SwapUInt64(value) : value;
        }
    }
}
