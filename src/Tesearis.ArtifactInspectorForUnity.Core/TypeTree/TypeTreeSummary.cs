using System.Collections.Generic;
using Tesearis.ArtifactInspectorForUnity.Core.Native;

namespace Tesearis.ArtifactInspectorForUnity.Core.TypeTree
{
    /// <summary>
    /// One distinct type-tree entry from <c>SerializedFile.TypeTrees</c>, describing a type this
    /// file declares without requiring a live object of that type. Pass <see cref="Index"/> to
    /// <c>SerializedFile.GetTypeTreeByIndex</c> to walk its full tree.
    /// </summary>
    public readonly struct TypeTreeSummary
    {
        /// <summary>Position in <c>SerializedFile.TypeTrees</c>; also the argument to <c>GetTypeTreeByIndex</c>.</summary>
        public int Index { get; }

        public int TypeId { get; }
        public int SerializedSize { get; }
        public TypeTreeCategory Category { get; }

        /// <summary>The type's content hash (4x uint32), for change-detection against a previous build.</summary>
        public IReadOnlyList<uint> Hash { get; }

        /// <summary>
        /// The backing managed type's class/namespace/assembly name. Populated for
        /// <see cref="TypeTreeCategory.RefType"/> entries ([SerializeReference] payloads); empty
        /// for <see cref="TypeTreeCategory.ObjectType"/> entries.
        /// </summary>
        public string ClassName { get; }

        public string NamespaceName { get; }
        public string AssemblyName { get; }

        internal TypeTreeSummary(int index, TypeTreeInfo info)
        {
            Index = index;
            TypeId = info.TypeId;
            SerializedSize = info.SerializedSize;
            Category = info.Category;
            Hash = info.Hash ?? new uint[4];
            ClassName = info.ClassName ?? string.Empty;
            NamespaceName = info.NamespaceName ?? string.Empty;
            AssemblyName = info.AssemblyName ?? string.Empty;
        }
    }
}
