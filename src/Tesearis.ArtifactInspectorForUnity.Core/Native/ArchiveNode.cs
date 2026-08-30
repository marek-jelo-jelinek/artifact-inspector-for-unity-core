using System;

namespace Tesearis.ArtifactInspectorForUnity.Core.Native
{
    /// <summary>Archive-node flags returned by UFS_GetArchiveNode.</summary>
    [Flags]
    internal enum ArchiveNodeFlags
    {
        None = 0,
        Directory = 0b1,
        Deleted = 0b10,
        SerializedFile = 0b100
    }

    /// <summary>
    /// One entry in a mounted archive, as returned by UFS_GetArchiveNode.
    /// </summary>
    internal readonly struct ArchiveNode
    {
        public string Path { get; }
        public long Size { get; }
        public ArchiveNodeFlags Flags { get; }

        public ArchiveNode(string path, long size, ArchiveNodeFlags flags)
        {
            Path = path;
            Size = size;
            Flags = flags;
        }

        /// <summary>True when this entry is a SerializedFile that has not been deleted.</summary>
        public bool IsSerializedFile => (Flags & (ArchiveNodeFlags.SerializedFile | ArchiveNodeFlags.Deleted)) == ArchiveNodeFlags.SerializedFile;
    }
}