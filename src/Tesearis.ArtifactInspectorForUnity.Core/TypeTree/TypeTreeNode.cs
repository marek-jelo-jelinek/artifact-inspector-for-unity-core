using System;
using System.Collections.Generic;
using System.Threading;
using Tesearis.ArtifactInspectorForUnity.Core.Native;

namespace Tesearis.ArtifactInspectorForUnity.Core.TypeTree
{
    /// <summary>
    /// An immutable node in a Unity SerializedFile type tree, describing the shape of one field.
    /// </summary>
    public sealed class TypeTreeNode
    {
        private const string ArrayTypeName = "Array";
        private const string TypelessDataTypeName = "TypelessData";

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

        /// <summary>
        /// True when this node uses an unsupported managed-reference shape ([SerializeReference]
        /// polymorphic field). Construction still succeeds; <see cref="TypeTreeOffsetWalker.ComputeSize"/>
        /// throws <see cref="UnsupportedManagedReferenceShapeException"/> if this node's size is ever
        /// actually needed.
        /// </summary>
        public bool HasUnsupportedManagedReferenceShape { get; }

        public IReadOnlyList<TypeTreeNode> Children { get; }

        // Sentinels for the lazy constant-byte-size cache (see TryGetConstantByteSize).
        // Valid constant sizes are always >= 0, so these negative sentinels are unambiguous.
        private const long CacheSentinelNotYetComputed = long.MinValue;
        private const long CacheSentinelNotConstant = long.MinValue + 1;

        /// <summary>
        /// Lazily-computed, cached result of <see cref="TypeTreeOffsetWalker.TryGetConstantElementSize"/>
        /// for this node. Written at most once per node (idempotently) via Interlocked, so the same
        /// TypeTreeNode instance can safely be read by many concurrent threads (as happens when many
        /// objects of the same type are inspected simultaneously — all share the same type-tree nodes
        /// from <see cref="TypeTreeCache"/>).
        /// </summary>
        private long _cachedConstantByteSize = CacheSentinelNotYetComputed;

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
            HasUnsupportedManagedReferenceShape = (flags & ManagedReferenceFlags) != 0;

            var rawChildren = children ?? new List<TypeTreeNode>();
            if (rawChildren.Count == 1 && rawChildren[0].TypeName == ArrayTypeName)
            {
                // Unwrap the implicit Array child.
                IsArrayLike = true;
                Children = rawChildren[0].Children;
                IsAligned = (rawChildren[0].MetaFlags & TypeTreeMetaFlags.AlignBytes) != 0;
                RequireArrayFlag(rawChildren[0].Flags);
            }
            else if (typeName == ArrayTypeName)
            {
                // A bare Array node with no wrapper.
                IsArrayLike = true;
                Children = rawChildren;
                IsAligned = (metaFlags & TypeTreeMetaFlags.AlignBytes) != 0;
                RequireArrayFlag(flags);
            }
            else if (typeName == TypelessDataTypeName)
            {
                if (rawChildren.Count != 2)
                {
                    throw new ArtifactInspectorException(
                        "Type tree node '" + name + "' (TypelessData) has " + rawChildren.Count +
                        " children; expected 2 (size and data).");
                }

                IsArrayLike = true;
                Children = rawChildren;
                IsAligned = (metaFlags & TypeTreeMetaFlags.AlignBytes) != 0;
            }
            else
            {
                IsArrayLike = false;
                Children = rawChildren;
                IsAligned = (metaFlags & TypeTreeMetaFlags.AlignBytes) != 0;
            }
        }

        /// <summary>
        /// Returns <c>true</c> and sets <paramref name="size"/> to this node's constant serialized byte
        /// size when that size is statically known without reading any byte-source data. Returns
        /// <c>false</c> for variable-size nodes (arrays, strings, structs containing such fields).
        /// <para>
        /// The result is computed on the first call and cached on the node instance, so repeated calls
        /// from any number of threads pay only a single volatile read after the first computation. Since
        /// <see cref="TypeTreeCache"/> shares the same <see cref="TypeTreeNode"/> instances across all
        /// objects of the same type, this cache is effectively process-wide per type, not per object —
        /// the first of N same-type objects warms it, and the remaining N-1 get an immediate O(1) answer.
        /// </para>
        /// </summary>
        internal bool TryGetConstantByteSize(out long size)
        {
            var cached = Interlocked.Read(ref _cachedConstantByteSize);
            if (cached != CacheSentinelNotYetComputed)
            {
                if (cached == CacheSentinelNotConstant) { size = 0; return false; }
                size = cached;
                return true;
            }

            // First call: compute via the existing (stateless) logic, then cache the result.
            // If two threads race, both compute the same value — the CAS ensures exactly one write
            // wins, and the re-read gives every thread the canonical stored value.
            var hasConstant = TypeTreeOffsetWalker.TryGetConstantElementSize(this, out var computed);
            var toStore = hasConstant ? computed : CacheSentinelNotConstant;
            Interlocked.CompareExchange(ref _cachedConstantByteSize, toStore, CacheSentinelNotYetComputed);

            cached = Interlocked.Read(ref _cachedConstantByteSize);
            if (cached == CacheSentinelNotConstant) { size = 0; return false; }
            size = cached;
            return true;
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
    }
}