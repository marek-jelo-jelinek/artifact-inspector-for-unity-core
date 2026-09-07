using System;
using System.Collections.Generic;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat
{
    /// <summary>
    /// Detects and parses a Unity SerializedFile directly off bytes. No TypeTree, no native
    /// UnityFileSystemApi call involved. This is the only way to inspect a "stripped" file (one
    /// built with EnableTypeTree=false, as most shipped Player builds are): UFS_OpenSerializedFile
    /// refuses to open those at all, even though the object list and external references don't
    /// actually require a TypeTree to read.
    ///
    /// Full metadata parsing (Unity version, EnableTypeTree, object list, external references) is
    /// only implemented for format versions 22 and 23 (Unity 6000.3.x, see
    /// <see cref="SerializedFileInfo.MetadataParsed"/>).
    /// </summary>
    public static class SerializedFileDetector
    {
        private static readonly HashSet<uint> SupportedMetadataVersions = new() { 22, 23 };
        private const int MonoBehaviourClassId = 114;
        private const int UndefinedPersistentTypeId = -1;

        private static readonly IReadOnlyList<StrippedObjectInfo> EmptyObjects = Array.Empty<StrippedObjectInfo>();
        private static readonly IReadOnlyList<ExternalReference> EmptyExternalReferences = Array.Empty<ExternalReference>();

        /// <summary>
        /// Detects and, when the version is supported, fully parses a SerializedFile from
        /// <paramref name="source"/>. Returns false only when the header itself isn't recognized as
        /// a SerializedFile at all. An unsupported version, or an unparseable leading metadata
        /// field, still returns true with <see cref="SerializedFileInfo.MetadataParsed"/> false.
        /// Throws <see cref="ArtifactInspectorException"/> if the metadata section is internally
        /// inconsistent (a count that doesn't fit the remaining bytes, a truncated field) once
        /// parsing has gotten far enough to know this really is a SerializedFile of a version this
        /// library understands.
        /// </summary>
        public static bool TryDetect(IRandomAccessByteSource source, out SerializedFileInfo info)
        {
            info = default;

            if (!SerializedFileHeaderParser.TryParse(source, out var header)) return false;

            if (!SupportedMetadataVersions.Contains(header.Version))
            {
                info = HeaderOnlyInfo(header,
                    "Metadata parsing is not supported for SerializedFile version " + header.Version +
                    ". Only versions 22 and 23 (Unity 6000.3.x) are supported.");
                return true;
            }

            if (!TryParseLeadingMetadata(source, header, out var unityVersion, out var targetPlatform,
                    out var enableTypeTree, out var leadingError))
            {
                info = HeaderOnlyInfo(header, leadingError);
                return true;
            }

            ParseExtendedMetadata(source, header, enableTypeTree, out var objects, out var externalReferences);

            info = new SerializedFileInfo(header.Version, header.FileSize,
                header.MetadataSize, header.DataOffset, header.IsBigEndian, metadataParsed: true,
                metadataParseError: null, unityVersion, targetPlatform, enableTypeTree, objects, externalReferences);
            return true;
        }

        /// <summary>Convenience overload that opens and owns its own <see cref="FileStreamByteSource"/>.</summary>
        public static bool TryDetect(string filePath, out SerializedFileInfo info)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));

            using var source = new FileStreamByteSource(filePath);
            return TryDetect(source, out info);
        }

        /// <summary>
        /// Cheap fast path: reads only the header and the three leading metadata fields (skipping
        /// the type/object/external-reference lists), so it can be called before attempting a
        /// native open without paying for a full parse. Returns true only when it can positively
        /// confirm EnableTypeTree is false. False for files that have TypeTrees and for anything
        /// that can't be parsed (never throws).
        /// </summary>
        public static bool IsMissingTypeTrees(IRandomAccessByteSource source)
        {
            try
            {
                if (!SerializedFileHeaderParser.TryParse(source, out var header)) return false;
                if (!SupportedMetadataVersions.Contains(header.Version)) return false;

                return TryParseLeadingMetadata(source, header, out _, out _, out var enableTypeTree, out _)
                    && !enableTypeTree;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Convenience overload that opens and owns its own <see cref="FileStreamByteSource"/>.</summary>
        public static bool IsMissingTypeTrees(string filePath)
        {
            try
            {
                if (filePath == null) return false;

                using var source = new FileStreamByteSource(filePath);
                return IsMissingTypeTrees(source);
            }
            catch
            {
                return false;
            }
        }

        private static SerializedFileInfo HeaderOnlyInfo(SerializedFileHeader header, string error)
        {
            return new SerializedFileInfo(header.Version, header.FileSize,
                header.MetadataSize, header.DataOffset, header.IsBigEndian, metadataParsed: false,
                metadataParseError: error, unityVersion: null, targetPlatform: 0, enableTypeTree: false,
                EmptyObjects, EmptyExternalReferences);
        }

        /// <summary>
        /// Reads the Unity version string, target platform, and EnableTypeTree byte. The cheap
        /// leading portion of the metadata section. Never throws: any failure is reported via the
        /// out error parameter instead, since this is also called from the exception-swallowing
        /// <see cref="IsMissingTypeTrees(IRandomAccessByteSource)"/> fast path.
        /// </summary>
        private static bool TryParseLeadingMetadata(IRandomAccessByteSource source, SerializedFileHeader header,
            out string unityVersion, out uint targetPlatform, out bool enableTypeTree, out string error)
        {
            unityVersion = null;
            targetPlatform = 0;
            enableTypeTree = false;
            error = null;

            try
            {
                var reader = new SerializedFileByteReader(source, header.MetadataStartOffset, header.IsBigEndian);

                var version = reader.ReadNullTerminatedAsciiString();
                if (version.Length is 0 or > 64)
                {
                    error = "Unity version string has unexpected length (" + version.Length + ").";
                    return false;
                }

                var platform = reader.ReadUInt32();
                var typeTree = reader.ReadByte() != 0;

                unityVersion = version;
                targetPlatform = platform;
                enableTypeTree = typeTree;
                return true;
            }
            catch (Exception ex)
            {
                error = "An error occurred while parsing the leading metadata fields: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Walks the type list, object list, script-type list, and external-reference list. Only
        /// called once the leading metadata fields have already parsed successfully.
        /// </summary>
        private static void ParseExtendedMetadata(IRandomAccessByteSource source, SerializedFileHeader header,
            bool enableTypeTree, out List<StrippedObjectInfo> objects, out List<ExternalReference> externalReferences)
        {
            var reader = new SerializedFileByteReader(source, header.MetadataStartOffset, header.IsBigEndian);

            // Re-walk the three leading fields already validated by TryParseLeadingMetadata.
            reader.ReadNullTerminatedAsciiString();
            reader.ReadUInt32();
            reader.ReadByte();

            // Type list (m_Types) walked only to skip past it correctly and to resolve each
            // object's typeIndex to a persistentTypeID (ClassID); nothing else about a type entry is
            // surfaced in the public model.
            var typeCount = ReadNonNegativeCount(reader, "type");
            var typePersistentIds = new int[typeCount];
            for (var i = 0; i < typeCount; i++)
            {
                typePersistentIds[i] = ReadTypeEntryAndSkip(reader, enableTypeTree);
            }

            var objectCount = ReadNonNegativeCount(reader, "object");
            objects = new List<StrippedObjectInfo>(objectCount);
            for (var i = 0; i < objectCount; i++)
            {
                reader.AlignTo4(header.MetadataStartOffset);
                var pathId = reader.ReadInt64();
                var byteStart = (long)reader.ReadUInt64();
                var byteOffset = byteStart + (long)header.DataOffset;
                long byteSize = reader.ReadUInt32();
                var typeIndex = reader.ReadInt32();
                var typeId = typeIndex >= 0 && typeIndex < typePersistentIds.Length
                    ? typePersistentIds[typeIndex]
                    : typeIndex;
                objects.Add(new StrippedObjectInfo(pathId, typeId, byteOffset, byteSize));
            }

            // Script-type list parsed only to advance the cursor correctly; not needed by this feature's output, so discarded.
            var scriptTypeCount = ReadNonNegativeCount(reader, "script-type");
            for (var i = 0; i < scriptTypeCount; i++)
            {
                reader.ReadInt32(); // fileId
                reader.AlignTo4(header.MetadataStartOffset);
                reader.ReadInt64(); // pathId
            }

            var externalsCount = ReadNonNegativeCount(reader, "external-reference");
            externalReferences = new List<ExternalReference>(externalsCount);
            for (var i = 0; i < externalsCount; i++)
            {
                reader.ReadNullTerminatedAsciiString();
                var d0 = reader.ReadUInt32();
                var d1 = reader.ReadUInt32();
                var d2 = reader.ReadUInt32();
                var d3 = reader.ReadUInt32();
                var guid = GuidFormatting.FormatUnityGuid(d0, d1, d2, d3);
                var type = (ExternalReferenceType)reader.ReadInt32();
                var pathName = reader.ReadNullTerminatedAsciiString();
                externalReferences.Add(new ExternalReference(pathName, guid, type));
            }

            // Stop here -- m_RefTypes (SerializeReference type entries) isn't needed by this
            // feature and is deliberately not read.
        }

        /// <summary>
        /// Reads one type-list entry, advancing the reader past every field including the TypeTree
        /// blob when present, and returns its persistentTypeID (ClassID) for later object
        /// typeIndex resolution.
        /// </summary>
        private static int ReadTypeEntryAndSkip(SerializedFileByteReader reader, bool enableTypeTree)
        {
            var persistentTypeId = reader.ReadInt32();
            reader.ReadByte(); // isStrippedType (not surfaced in the public model)
            var scriptTypeIndex = reader.ReadInt16();

            var hasScriptId = persistentTypeId == UndefinedPersistentTypeId
                            || persistentTypeId == MonoBehaviourClassId
                            || scriptTypeIndex >= 0;
            if (hasScriptId)
            {
                reader.Skip(16); // scriptID (Hash128, 4x uint32)
            }

            reader.Skip(16); // oldTypeHash (Hash128)

            if (!enableTypeTree) return persistentTypeId;

            // TypeTree blob (versions 22/23 always use the "blob" node-table format, never the
            // older recursive text-node format): numberOfNodes (int32), stringBufferSize (int32),
            // then numberOfNodes fixed-size node records, then the string buffer itself. No
            // per-type "typeTreeSize" length prefix exists in this format -- the total size has to
            // be computed from these three pieces.
            var numberOfNodes = ReadNonNegativeCount(reader, "type tree node");
            var stringBufferSize = ReadNonNegativeCount(reader, "type tree string buffer");
            reader.Skip(numberOfNodes * (long)TypeTreeNodeBlobRecordSize + stringBufferSize);

            var depCount = ReadNonNegativeCount(reader, "type dependency");
            reader.Skip(depCount * 4L);

            return persistentTypeId;
        }

        /// <summary>
        /// Byte size of one fixed-size TypeTree node record in the on-disk blob format: UInt16
        /// version, byte level, byte typeFlags, UInt32 typeStrOffset, UInt32 nameStrOffset, int32
        /// byteSize, int32 index, int32 metaFlag, UInt64 refTypeHash (present for every format
        /// version this project supports, 22 and 23).
        /// </summary>
        private const int TypeTreeNodeBlobRecordSize = 32;

        private static int ReadNonNegativeCount(SerializedFileByteReader reader, string fieldName)
        {
            var count = reader.ReadInt32();
            if (count < 0)
            {
                throw new ArtifactInspectorException(
                    "Negative " + fieldName + " count (" + count + ") at metadata offset " + reader.Position + " -- the file is likely truncated or corrupt.");
            }

            if (count > reader.RemainingBytes)
            {
                throw new ArtifactInspectorException(
                    fieldName + " count (" + count + ") at metadata offset " + reader.Position + " exceeds the " +
                    reader.RemainingBytes + " bytes remaining in the underlying data. The file is likely truncated or corrupt.");
            }

            return count;
        }
    }
}
