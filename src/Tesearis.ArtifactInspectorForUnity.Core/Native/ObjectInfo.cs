using System.Runtime.InteropServices;

namespace Tesearis.ArtifactInspectorForUnity.Core.Native
{
    /// <summary>The native ObjectInfo struct, filled in by UFS_GetObjectInfo.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct ObjectInfo
    {
        public long Id;
        public long Offset;
        public long Size;
        public int TypeId;
    }
}