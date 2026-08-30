using System.Collections.Generic;
using Tesearis.ArtifactInspectorForUnity.Core.Model;

namespace Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat
{
    /// <summary>
    /// Everything <see cref="SerializedFileDetector"/> can determine about a SerializedFile directly
    /// off bytes, with no TypeTree or native call involved.
    ///
    /// The header fields (<see cref="Version"/> through <see cref="IsBigEndian"/>) are always
    /// populated when <see cref="SerializedFileDetector.TryDetect(TypeTree.IRandomAccessByteSource, out SerializedFileInfo)"/>
    /// returns true. The remaining fields require walking the metadata section, which this library
    /// only knows how to do for format version 23 (Unity 6000.3+).
    /// </summary>
    public readonly struct SerializedFileInfo
    {
        public uint Version { get; }
        public ulong FileSize { get; }
        public ulong MetadataSize { get; }
        public ulong DataOffset { get; }
        public bool IsBigEndian { get; }

        public bool MetadataParsed { get; }
        public string MetadataParseError { get; }

        public string UnityVersion { get; }
        public uint TargetPlatform { get; }
        public bool EnableTypeTree { get; }
        public IReadOnlyList<StrippedObjectInfo> Objects { get; }
        public IReadOnlyList<ExternalReference> ExternalReferences { get; }

        /// <summary>True only when metadata parsing succeeded and positively confirmed there's no TypeTree.</summary>
        public bool IsMissingTypeTrees => MetadataParsed && !EnableTypeTree;

        internal SerializedFileInfo(uint version, ulong fileSize, ulong metadataSize,
            ulong dataOffset, bool isBigEndian, bool metadataParsed, string metadataParseError,
            string unityVersion, uint targetPlatform, bool enableTypeTree,
            IReadOnlyList<StrippedObjectInfo> objects, IReadOnlyList<ExternalReference> externalReferences)
        {
            Version = version;
            FileSize = fileSize;
            MetadataSize = metadataSize;
            DataOffset = dataOffset;
            IsBigEndian = isBigEndian;
            MetadataParsed = metadataParsed;
            MetadataParseError = metadataParseError;
            UnityVersion = unityVersion;
            TargetPlatform = targetPlatform;
            EnableTypeTree = enableTypeTree;
            Objects = objects;
            ExternalReferences = externalReferences;
        }
    }
}
