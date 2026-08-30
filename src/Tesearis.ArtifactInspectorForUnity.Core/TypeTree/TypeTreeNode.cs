using System;
using System.Collections.Generic;
using Tesearis.ArtifactInspectorForUnity.Core.Native;

namespace Tesearis.ArtifactInspectorForUnity.Core.TypeTree
{
    /// <summary>
    /// An immutable node in a Unity SerializedFile type tree, describing the shape of one field.
    /// </summary>
    public sealed class TypeTreeNode
    {
        private const string ArrayTypeName = "Array";

        public string Name { get; }
        public string TypeName { get; }
        public int ByteSize { get; }
        internal TypeTreeFlags Flags { get; }
        internal TypeTreeMetaFlags MetaFlags { get; }
        public bool HasVariableByteSize => ByteSize == -1;

        /// <summary>
        /// True when the serialized position immediately after this node's data must be rounded up to the next 4-byte boundary.
        /// </summary>
        public bool IsAligned { get; }

        /// <summary>True when this node is an array: its data is [sizeNode, elementTemplateNode].</summary>
        public bool IsArrayLike { get; }

        public IReadOnlyList<TypeTreeNode> Children { get; }

        internal TypeTreeNode(
            string name,
            string typeName,
            int byteSize,
            TypeTreeFlags flags,
            TypeTreeMetaFlags metaFlags,
            IReadOnlyList<TypeTreeNode> children)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
            ByteSize = byteSize;
            Flags = flags;
            MetaFlags = metaFlags;
            IsAligned = (metaFlags & TypeTreeMetaFlags.AlignBytes) != 0;
            RequireNoManagedReferenceFlags(flags);

            var rawChildren = children ?? new List<TypeTreeNode>();
            if (rawChildren.Count == 1 && rawChildren[0].TypeName == ArrayTypeName)
            {
                // Unwrap the implicit Array child.
                IsArrayLike = true;
                Children = rawChildren[0].Children;
                RequireArrayFlag(rawChildren[0].Flags);
            }
            else if (typeName == ArrayTypeName)
            {
                // A bare Array node with no wrapper.
                IsArrayLike = true;
                Children = rawChildren;
                RequireArrayFlag(flags);
            }
            else
            {
                IsArrayLike = false;
                Children = rawChildren;
            }
        }

        /// <summary>Throws if the node looks array-like by name but its IsArray flag is not set.</summary>
        private void RequireArrayFlag(TypeTreeFlags arrayNodeFlags)
        {
            if ((arrayNodeFlags & TypeTreeFlags.IsArray) == 0)
            {
                throw new ArtifactInspectorException(
                    "Type tree node '" + Name + "' (" + TypeName + ") looks array-like by type name but its " +
                    "native IsArray flag is not set. The type tree may be malformed or use an unexpected shape.");
            }
        }

        private const TypeTreeFlags ManagedReferenceFlags =
            TypeTreeFlags.IsManagedReference | TypeTreeFlags.IsManagedReferenceRegistry | TypeTreeFlags.IsArrayOfRefs;

        /// <summary>Throws if the node uses an unsupported managed-reference shape.</summary>
        private void RequireNoManagedReferenceFlags(TypeTreeFlags nodeFlags)
        {
            if ((nodeFlags & ManagedReferenceFlags) != 0)
            {
                throw new ArtifactInspectorException(
                    "Type tree node '" + Name + "' (" + TypeName + ") uses an unsupported managed-reference shape " +
                    "([SerializeReference] polymorphic field) so offsets cannot be computed for this node.");
            }
        }
    }
}