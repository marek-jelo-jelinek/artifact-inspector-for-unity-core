using System;

namespace Tesearis.ArtifactInspectorForUnity.Core.Native
{
    /// <summary>Type-tree node meta flags returned by UFS_GetTypeTreeNodeInfo.</summary>
    [Flags]
    internal enum TypeTreeMetaFlags
    {
        None = 0,
        AlignBytes = 1 << 14,
        AnyChildUsesAlignBytes = 1 << 15,
    }
}