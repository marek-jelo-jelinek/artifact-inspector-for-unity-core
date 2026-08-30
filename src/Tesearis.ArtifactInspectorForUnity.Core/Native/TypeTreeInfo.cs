using System.Runtime.InteropServices;

namespace Tesearis.ArtifactInspectorForUnity.Core.Native
{
    /// <summary>
    /// The native TypeTreeInfo struct, filled in by UFS_GetTypeTreeInfo. Unlike most of this
    /// library's native calls, this one fills a single fixed-layout struct by value rather than
    /// caller-supplied buffers, so the fields are marshaled directly (ByValTStr/ByValArray)
    /// instead of going through the ToNativeUtf8/FromNativeUtf8 buffer convention used elsewhere
    /// in Native/Interop/UnityFileSystemApi.cs.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct TypeTreeInfo
    {
        public int TypeId;
        public int SerializedSize;
        public TypeTreeCategory Category;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] Hash;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string ClassName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string NamespaceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AssemblyName;
    }
}
