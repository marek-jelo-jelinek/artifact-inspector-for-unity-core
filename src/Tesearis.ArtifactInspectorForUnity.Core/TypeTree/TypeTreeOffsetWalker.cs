using System;

namespace Tesearis.ArtifactInspectorForUnity.Core.TypeTree
{
    /// <summary>Pure offset/size arithmetic over a TypeTreeNode shape and an IRandomAccessByteSource.</summary>
    internal static class TypeTreeOffsetWalker
    {
        internal const int MaxRecursionDepth = 64;

        /// <summary>The number of bytes node's data occupies starting at offset, excluding trailing alignment.</summary>
        internal static long ComputeSize(TypeTreeNode node, long offset, IRandomAccessByteSource byteSource, int depth = 0)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            RequireDepthWithinLimit(depth, node);
            if (node.HasUnsupportedManagedReferenceShape) throw new UnsupportedManagedReferenceShapeException(node.Name, node.TypeName);

            // Fast path: if this node's serialized size is statically known (no arrays, strings, or
            // managed references anywhere in its subtree), skip all recursion and byte-source reads.
            // TypeTreeNode caches this result so the first call warms it for every subsequent call on
            // the same node — including the same node used by many objects of the same type
            // (TypeTreeCache shares node instances across all objects of a given type).
            if (node.TryGetConstantByteSize(out var knownSize)) return knownSize;

            if (node.IsArrayLike) return ComputeArraySize(node, offset, byteSource, depth);
            if (node.TypeName == "string") return 4 + ReadValidatedLengthPrefix(byteSource, offset, "string");
            if (node.Children.Count == 0)
            {
                if (node.HasVariableByteSize)
                {
                    throw new ArtifactInspectorException(
                        "Type tree node '" + node.Name + "' (" + node.TypeName +
                        ") has no children and no fixed byte size. Its size cannot be computed.");
                }

                return node.ByteSize;
            }

            var currentOffset = offset;
            foreach (var child in node.Children)
            {
                var childSize = ComputeSize(child, currentOffset, byteSource, depth + 1);
                currentOffset += childSize;
                if (child.IsAligned) currentOffset = ApplyAlignment(child, currentOffset);
            }

            return currentOffset - offset;
        }

        private static long ComputeArraySize(TypeTreeNode arrayNode, long offset, IRandomAccessByteSource byteSource, int depth)
        {
            if (arrayNode.Children.Count < 2)
            {
                throw new ArtifactInspectorException(
                    $"Array-like type tree node '{arrayNode.Name}' ({arrayNode.TypeName}) has {arrayNode.Children.Count} children; expected 2 (size and element template).");
            }

            var elementTemplate = arrayNode.Children[1];
            var count = ReadValidatedLengthPrefix(byteSource, offset, "array");

            // Fast path: if every element has a statically known constant size we can skip the per-element
            // loop entirely and compute the total array size with a single multiplication.  This is the
            // common case for primitive buffers (UInt8[], UInt16[], float[]) and fixed structs (Vector3[],
            // etc.) such as mesh vertex/index buffers, which can be hundreds of thousands of elements long.
            if (TryGetConstantElementSize(elementTemplate, out var constantElementSize))
            {
                // Account for per-element alignment padding (rare for primitive element types, but correct).
                var stride = elementTemplate.IsAligned ? (constantElementSize + 3) & ~3L : constantElementSize;
                var totalDataBytes = (long)count * stride;
                RequireCountFitsRemainingBytes(totalDataBytes, offset + 4, byteSource);
                return 4 + totalDataBytes;
            }

            // Slow path: variable-size elements — walk element by element.
            var currentOffset = offset + 4;
            for (var i = 0; i < count; i++)
            {
                var elementSize = ComputeSize(elementTemplate, currentOffset, byteSource, depth + 1);
                currentOffset += elementSize;
                if (elementTemplate.IsAligned) currentOffset = ApplyAlignment(elementTemplate, currentOffset);
            }

            return currentOffset - offset;
        }

        /// <summary>
        /// Returns true and sets <paramref name="elementSize"/> to the constant serialized byte size of
        /// <paramref name="node"/> when that size is statically known without reading any stream data.
        /// Returns false for arrays, strings, managed references, any struct containing such members,
        /// and any struct nested deeper than <see cref="MaxRecursionDepth"/> levels (which would be
        /// caught and thrown by <see cref="ComputeSize"/> anyway — returning false here lets that path
        /// handle the error rather than silently caching the result of a too-deep tree).
        /// </summary>
        internal static bool TryGetConstantElementSize(TypeTreeNode node, out long elementSize, int depth = 0)
        {
            elementSize = 0;

            // Arrays/strings/managed-references have variable size — cannot shortcut.
            if (node.IsArrayLike) return false;
            if (node.TypeName == "string") return false;
            if (node.HasUnsupportedManagedReferenceShape) return false;

            // Leaf node with a fixed native byte size reported directly by Unity.
            if (node.Children.Count == 0)
            {
                if (node.HasVariableByteSize) return false;
                elementSize = node.ByteSize;
                return true;
            }

            // Guard against pathologically deep struct chains: ComputeSize throws for depth > MaxRecursionDepth,
            // so returning false here lets that existing check fire rather than caching a misleading result.
            if (depth > MaxRecursionDepth) return false;

            // Struct: sum all children recursively; bail out on the first variable child.
            long total = 0;
            foreach (var child in node.Children)
            {
                if (!TryGetConstantElementSize(child, out var childSize, depth + 1)) return false;
                total += childSize;
                if (child.IsAligned) total = (total + 3) & ~3L;
            }

            elementSize = total;
            return true;
        }

        /// <summary>Throws if depth exceeds <see cref="MaxRecursionDepth"/>, instead of recursing into a corrupt/self-referential shape until the stack overflows.</summary>
        private static void RequireDepthWithinLimit(int depth, TypeTreeNode node)
        {
            if (depth <= MaxRecursionDepth) return;

            throw new ArtifactInspectorException(
                $"Type tree node '{node.Name}' ({node.TypeName}) nests more than {MaxRecursionDepth} levels deep. The type tree is likely malformed or self-referential.");
        }

        /// <summary>Rounds endOffset up to the next 4-byte boundary if node requires it.</summary>
        internal static long ApplyAlignment(TypeTreeNode node, long endOffset)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (!node.IsAligned) return endOffset;
            return (endOffset + 3) & ~3L;
        }

        internal static int ReadInt32LittleEndian(IRandomAccessByteSource byteSource, long offset)
        {
            if (byteSource == null) throw new ArgumentNullException(nameof(byteSource));
            var buffer = new byte[4];
            var read = byteSource.Read(offset, buffer, 0, 4);
            if (read != 4)
                throw new ArtifactInspectorException("Unexpected end of data while reading a 4-byte length prefix at offset " + offset + ".");
            return BitConverter.ToInt32(buffer, 0);
        }

        /// <summary>Throws if count exceeds the bytes remaining in byteSource.</summary>
        internal static void RequireCountFitsRemainingBytes(long count, long dataStartOffset, IRandomAccessByteSource byteSource)
        {
            var remaining = byteSource.Length - dataStartOffset;
            if (count > remaining)
            {
                throw new ArtifactInspectorException(
                    $"Length prefix {count} at offset {dataStartOffset} exceeds the {remaining} bytes remaining in the underlying data. The file is likely truncated or corrupt.");
            }
        }

        /// <summary>
        /// Reads a 4-byte little-endian length prefix at offset, validating it is not negative and that
        /// the data it claims fits in the bytes remaining in byteSource. kind names what's being read
        /// (e.g. "array", "string") for the negative-length exception message.
        /// </summary>
        internal static int ReadValidatedLengthPrefix(IRandomAccessByteSource byteSource, long offset, string kind)
        {
            var length = ReadInt32LittleEndian(byteSource, offset);
            if (length < 0) throw new ArtifactInspectorException($"Negative {kind} length at offset {offset}.");
            RequireCountFitsRemainingBytes(length, offset + 4, byteSource);
            return length;
        }
    }
}