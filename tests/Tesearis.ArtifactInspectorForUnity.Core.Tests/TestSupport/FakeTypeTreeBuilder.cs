using System.Collections.Generic;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport
{
    /// <summary>
    /// Small helpers for building synthetic <see cref="TypeTreeNode"/> trees in
    /// tests, without needing a real native type-tree handle.
    /// </summary>
    internal static class FakeTypeTreeBuilder
    {
        internal static TypeTreeNode Leaf(string name, string typeName, int byteSize, bool aligned = false)
        {
            return new TypeTreeNode(
                name, typeName, byteSize, TypeTreeFlags.None,
                aligned ? TypeTreeMetaFlags.AlignBytes : TypeTreeMetaFlags.None,
                new List<TypeTreeNode>());
        }

        /// <summary>
        /// A leaf node carrying only <see cref="TypeTreeMetaFlags.AnyChildUsesAlignBytes"/>, never
        /// <see cref="TypeTreeMetaFlags.AlignBytes"/> -- lets a test prove that flag alone must not pad this
        /// node's own trailing data; only <see cref="TypeTreeMetaFlags.AlignBytes"/> should.
        /// </summary>
        internal static TypeTreeNode LeafWithAnyChildUsesAlignBytesOnly(string name, string typeName, int byteSize)
        {
            return new TypeTreeNode(
                name, typeName, byteSize, TypeTreeFlags.None, TypeTreeMetaFlags.AnyChildUsesAlignBytes,
                new List<TypeTreeNode>());
        }

        internal static TypeTreeNode Int32(string name, bool aligned = false)
        {
            return Leaf(name, "int", 4, aligned);
        }

        internal static TypeTreeNode Byte(string name, bool aligned = false)
        {
            return Leaf(name, "UInt8", 1, aligned);
        }

        internal static TypeTreeNode Bool(string name, bool aligned = false)
        {
            return Leaf(name, "bool", 1, aligned);
        }

        internal static TypeTreeNode String(string name, bool aligned = false)
        {
            var arrayNode = Array(Byte("data"), aligned);
            return new TypeTreeNode(
                name, "string", -1, TypeTreeFlags.None, TypeTreeMetaFlags.None,
                new List<TypeTreeNode> { arrayNode });
        }

        internal static TypeTreeNode Struct(string name, string typeName, params TypeTreeNode[] children)
        {
            return new TypeTreeNode(
                name, typeName, -1, TypeTreeFlags.None, TypeTreeMetaFlags.None,
                new List<TypeTreeNode>(children));
        }

        /// <summary>A bare native "Array" node: [size:int, data:elementTemplate].</summary>
        internal static TypeTreeNode Array(TypeTreeNode elementTemplate, bool aligned = false)
        {
            return new TypeTreeNode(
                "Array", "Array", -1, TypeTreeFlags.IsArray,
                aligned ? TypeTreeMetaFlags.AlignBytes : TypeTreeMetaFlags.None,
                new List<TypeTreeNode> { Int32("size"), elementTemplate });
        }

        /// <summary>
        /// A "vector"-wrapped array field, matching how array fields actually
        /// appear as struct members in real Unity type trees:
        /// vector { Array { size, data } }.
        /// </summary>
        internal static TypeTreeNode Vector(string name, TypeTreeNode elementTemplate, bool aligned = false)
        {
            var arrayNode = Array(elementTemplate, aligned);
            return new TypeTreeNode(
                name, "vector", -1, TypeTreeFlags.None, TypeTreeMetaFlags.None,
                new List<TypeTreeNode> { arrayNode });
        }

        internal static TypeTreeNode TypelessData(string name, bool aligned = false)
        {
            return new TypeTreeNode(
                name, "TypelessData", -1, TypeTreeFlags.None,
                aligned ? TypeTreeMetaFlags.AlignBytes : TypeTreeMetaFlags.None,
                new List<TypeTreeNode> { Int32("size"), Byte("data") });
        }

        /// <summary>
        /// A leaf carrying <see cref="TypeTreeFlags.IsManagedReference"/>, simulating a
        /// [SerializeReference] polymorphic field -- <see cref="TypeTreeNode.HasUnsupportedManagedReferenceShape"/>
        /// is set, but construction itself never throws.
        /// </summary>
        internal static TypeTreeNode ManagedReference(string name)
        {
            return new TypeTreeNode(
                name, "managedReference", -1, TypeTreeFlags.IsManagedReference, TypeTreeMetaFlags.None,
                new List<TypeTreeNode>());
        }
    }
}
