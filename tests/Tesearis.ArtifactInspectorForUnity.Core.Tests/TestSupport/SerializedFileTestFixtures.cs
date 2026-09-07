using System;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport
{
    /// <summary>
    /// Hand-builds byte-for-byte Unity SerializedFile fixtures (header + metadata section) for
    /// BinaryFormat tests, mirroring exactly what SerializedFileDetector expects to read. Only
    /// covers the fields that parser actually reads/skips -- not a general-purpose writer. Always
    /// builds the modern (48-byte, 64-bit) header/metadata layout shared by versions 22 and 23
    /// (Unity 6000.3.x); a `version` parameter is still accepted so tests can exercise how an
    /// unsupported version is rejected.
    /// </summary>
    internal static class SerializedFileTestFixtures
    {
        /// <summary>One entry for <see cref="AppendTypeEntry"/>.</summary>
        internal sealed class TypeEntrySpec
        {
            internal int PersistentTypeId;
            internal short ScriptTypeIndex = -1;

            /// <summary>
            /// When enableTypeTree is true: how many (filler) TypeTree node records to write.
            /// Zero means no embedded TypeTree (e.g. an externally-sourced one).
            /// </summary>
            internal int TypeTreeNodeCount;

            /// <summary>When enableTypeTree is true: how many (filler) string-buffer bytes to write after the node records.</summary>
            internal int TypeTreeStringBufferSize;

            internal int[] TypeDependencies = Array.Empty<int>();
        }

        /// <summary>Builds just a header, with zero-filled filler bytes standing in for a metadata
        /// section whose content this method never writes -- for header-parser-only tests.</summary>
        internal static byte[] BuildHeader(uint version, byte endianness, ulong metadataSize, ulong fileSize,
            ulong dataOffset, int trailingByteCount)
        {
            var writer = new ByteBufferWriter();

            writer.WriteZeros(8)
                .WriteUInt32BigEndian(version)
                .WriteZeros(4)
                .WriteUInt64BigEndian(metadataSize)
                .WriteUInt64BigEndian(fileSize)
                .WriteUInt64BigEndian(dataOffset)
                .WriteByte(endianness)
                .WriteZeros(7);

            writer.WriteZeros(trailingByteCount);
            return writer.ToArray();
        }

        /// <summary>Prepends a header (big-endian on disk, matching real files) whose declared
        /// metadataSize/fileSize/dataOffset are computed from metadataBytes' actual length. When
        /// dataOffset falls beyond the end of the metadata section (as it validly can -- header
        /// validation requires dataOffset &lt;= fileSize), the buffer is padded out with zero bytes
        /// so fileSize can honestly cover it; no real "data section" content is ever needed since
        /// this feature's parser never reads past the metadata section.</summary>
        internal static byte[] WrapWithHeader(byte[] metadataBytes, uint version, byte endianness, ulong dataOffset)
        {
            const int headerLength = 48;
            var metadataSize = (ulong)metadataBytes.Length;
            var contentEnd = (ulong)(headerLength + metadataBytes.Length);
            var fileSize = Math.Max(contentEnd, dataOffset);

            var header = BuildHeader(version, endianness, metadataSize, fileSize, dataOffset, trailingByteCount: 0);

            var result = new byte[fileSize]; // zero-filled; covers any gap between metadata's end and dataOffset
            Buffer.BlockCopy(header, 0, result, 0, header.Length);
            Buffer.BlockCopy(metadataBytes, 0, result, header.Length, metadataBytes.Length);
            return result;
        }

        /// <summary>Appends the three leading metadata fields (Unity version, target platform, EnableTypeTree byte).</summary>
        internal static ByteBufferWriter AppendLeadingMetadata(ByteBufferWriter writer, string unityVersion,
            uint targetPlatform, bool enableTypeTree)
        {
            return writer.WriteNullTerminatedString(unityVersion)
                .WriteUInt32(targetPlatform)
                .WriteByte((byte)(enableTypeTree ? 1 : 0));
        }

        /// <summary>Appends one type-list entry, matching SerializedFileDetector's read/skip order exactly.</summary>
        internal static ByteBufferWriter AppendTypeEntry(ByteBufferWriter writer, bool enableTypeTree, TypeEntrySpec spec)
        {
            writer.WriteInt32(spec.PersistentTypeId)
                .WriteByte(0) // isStrippedType
                .WriteInt16(spec.ScriptTypeIndex);

            var hasScriptId = spec.PersistentTypeId == -1 || spec.PersistentTypeId == 114 || spec.ScriptTypeIndex >= 0;
            if (hasScriptId) writer.WriteZeros(16); // scriptID (Hash128)
            writer.WriteZeros(16); // oldTypeHash (Hash128), always present

            if (!enableTypeTree) return writer;

            // TypeTree blob (the "node table" format every version this project supports uses):
            // numberOfNodes, stringBufferSize, then that many fixed-size (32-byte) node records,
            // then the string buffer itself. Content is never read by this feature (only skipped
            // past), so filler bytes stand in for both.
            const int nodeRecordSize = 32;
            writer.WriteInt32(spec.TypeTreeNodeCount);
            writer.WriteInt32(spec.TypeTreeStringBufferSize);
            if (spec.TypeTreeNodeCount > 0) writer.WriteZeros(spec.TypeTreeNodeCount * nodeRecordSize);
            if (spec.TypeTreeStringBufferSize > 0) writer.WriteZeros(spec.TypeTreeStringBufferSize);

            var deps = spec.TypeDependencies ?? Array.Empty<int>();
            writer.WriteInt32(deps.Length);
            foreach (var dep in deps) writer.WriteInt32(dep);

            return writer;
        }

        /// <summary>Appends one object-list entry, including the 4-byte alignment SerializedFileDetector applies first.</summary>
        internal static ByteBufferWriter AppendObjectEntry(ByteBufferWriter writer, long pathId,
            long byteStart, long byteSize, int typeIndex)
        {
            writer.AlignTo4();
            writer.WriteInt64(pathId);
            writer.WriteUInt64((ulong)byteStart);
            writer.WriteUInt32((uint)byteSize);
            writer.WriteInt32(typeIndex);
            return writer;
        }

        /// <summary>Appends one script-type-list entry (fileId + aligned pathId) -- parsed only to advance the cursor.</summary>
        internal static ByteBufferWriter AppendScriptTypeEntry(ByteBufferWriter writer, int fileId, long pathId)
        {
            writer.WriteInt32(fileId);
            writer.AlignTo4();
            writer.WriteInt64(pathId);
            return writer;
        }

        /// <summary>Appends one external-reference entry.</summary>
        internal static ByteBufferWriter AppendExternalReference(ByteBufferWriter writer, uint d0, uint d1, uint d2,
            uint d3, int type, string path)
        {
            writer.WriteNullTerminatedString(""); // tempEmpty, always empty in practice
            writer.WriteUInt32(d0).WriteUInt32(d1).WriteUInt32(d2).WriteUInt32(d3);
            writer.WriteInt32(type);
            writer.WriteNullTerminatedString(path);
            return writer;
        }
    }
}
