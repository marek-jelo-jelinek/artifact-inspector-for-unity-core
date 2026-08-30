using System;

namespace Tesearis.ArtifactInspectorForUnity.Core.Native
{
    /// <summary>Type-tree node flags returned by UFS_GetTypeTreeNodeInfo.</summary>
    [Flags]
    internal enum TypeTreeFlags
    {
        None = 0,
        IsArray = 1,
        IsManagedReference = 2,
        IsManagedReferenceRegistry = 4,
        IsArrayOfRefs = 8,
    }
}