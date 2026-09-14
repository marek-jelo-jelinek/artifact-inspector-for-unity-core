using System;

namespace Tesearis.ArtifactInspectorForUnity.Core.TypeTree
{
    /// <summary>
    /// Builds a <see cref="SnapshotField"/> tree with one forward walk of a type tree's data, deciding per
    /// field whether to eagerly decode it or leave it deferred based on
    /// <see cref="MaterializeOptions.MaxInlineFieldSizeBytes"/>. The offset/alignment arithmetic mirrors
    /// <see cref="TypeTreeOffsetWalker.ComputeSize"/> exactly (branch order and all), reusing its validated
    /// helpers rather than duplicating them, so this stays a zero-diff addition to that already-tested file.
    /// </summary>
    internal static class SnapshotBuilder
    {
        internal static SnapshotField Build(TypeTreeNode node, long offset, IRandomAccessByteSource byteSource, MaterializeOptions options, int depth = 0)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (depth > TypeTreeOffsetWalker.MaxRecursionDepth)
            {
                throw new ArtifactInspectorException(
                    $"Type tree node '{node.Name}' ({node.TypeName}) nests more than {TypeTreeOffsetWalker.MaxRecursionDepth} levels deep. The type tree is likely malformed or self-referential.");
            }

            if (node.HasUnsupportedManagedReferenceShape)
            {
                throw new UnsupportedManagedReferenceShapeException(node.Name, node.TypeName);
            }

            if (node.IsArrayLike) return BuildArray(node, offset, byteSource, options, depth);
            if (node.TypeName == "string") return BuildUnwrappedString(node, offset, byteSource, options);
            if (node.Children.Count == 0) return BuildScalar(node, offset, byteSource);

            return BuildStruct(node, offset, byteSource, options, depth);
        }

        private static SnapshotField BuildScalar(TypeTreeNode node, long offset, IRandomAccessByteSource byteSource)
        {
            if (node.HasVariableByteSize)
            {
                throw new ArtifactInspectorException(
                    $"Type tree node '{node.Name}' ({node.TypeName}) has no children and no fixed byte size. Its size cannot be computed.");
            }

            var buffer = new byte[node.ByteSize];
            var read = byteSource.Read(offset, buffer, 0, node.ByteSize);
            if (read != node.ByteSize)
            {
                throw new ArtifactInspectorException($"Unexpected end of data while reading a {node.ByteSize}-byte value at offset {offset}.");
            }

            return SnapshotField.Scalar(node, byteSource, offset, buffer);
        }

        private static SnapshotField BuildStruct(TypeTreeNode node, long offset, IRandomAccessByteSource byteSource, MaterializeOptions options, int depth)
        {
            var children = new SnapshotField[node.Children.Count];
            var currentOffset = offset;
            for (var i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                var built = Build(child, currentOffset, byteSource, options, depth + 1);
                children[i] = built;
                currentOffset += built.ByteSize;
                if (child.IsAligned) currentOffset = TypeTreeOffsetWalker.ApplyAlignment(child, currentOffset);
            }

            return SnapshotField.StructNode(node, byteSource, offset, children, currentOffset - offset);
        }

        /// <summary>The rare, non-array-wrapped "string" node shape (see <see cref="TypeTreeOffsetWalker.ComputeSize"/>'s
        /// matching unconditional TypeName=="string" branch) -- real string fields are unwrapped into IsArrayLike
        /// by <see cref="TypeTreeNode"/>'s constructor and handled by <see cref="BuildArray"/> instead.</summary>
        private static SnapshotField BuildUnwrappedString(TypeTreeNode node, long offset, IRandomAccessByteSource byteSource, MaterializeOptions options)
        {
            var length = TypeTreeOffsetWalker.ReadValidatedLengthPrefix(byteSource, offset, "string");
            return BuildByteLikePayload(node, offset, byteSource, options, length);
        }

        private static SnapshotField BuildArray(TypeTreeNode node, long offset, IRandomAccessByteSource byteSource, MaterializeOptions options, int depth)
        {
            if (node.Children.Count < 2)
            {
                throw new ArtifactInspectorException(
                    $"Array-like type tree node '{node.Name}' ({node.TypeName}) has {node.Children.Count} children; expected 2 (size and element template).");
            }

            var elementTemplate = node.Children[1];
            var count = TypeTreeOffsetWalker.ReadValidatedLengthPrefix(byteSource, offset, "array");

            // IsAligned excluded deliberately: ComputeArraySize/OffsetCursor round up to a 4-byte boundary
            // after *every* element when the element template requests it, even a fixed-size scalar one.
            // The bulk-read fast paths below pack elements back-to-back with no such gap, so they'd silently
            // diverge from ComputeSize/TypeTreeReader for that (unusual but real, data-dependent) shape --
            // fall through to the general per-element path instead, which already replicates that behavior.
            var isFixedLeafElement = !elementTemplate.IsArrayLike
                && !elementTemplate.HasVariableByteSize
                && elementTemplate.Children.Count == 0
                && !elementTemplate.HasUnsupportedManagedReferenceShape
                && !elementTemplate.IsAligned;

            if (isFixedLeafElement && elementTemplate.ByteSize == 1)
            {
                // Unity encodes string/TypelessData/byte[] alike as arrays of single-byte elements. One
                // compact byte[] instead of one SnapshotField per byte -- both a correctness requirement
                // (avoid N allocations for an N-character name) and an O(1) size (4 + count) instead of the
                // O(n) element-by-element walk ComputeArraySize would otherwise do.
                return BuildByteLikePayload(node, offset, byteSource, options, count);
            }

            if (isFixedLeafElement)
            {
                // Any other fixed-size scalar element (int[], float[], ...): still O(1) to size, and
                // decodable with one bulk read instead of one read per element.
                var totalSize = 4L + (long)count * elementTemplate.ByteSize;
                if (totalSize > options.MaxInlineFieldSizeBytes)
                {
                    return SnapshotField.Deferred(node, byteSource, offset, totalSize);
                }

                var bulk = new byte[count * elementTemplate.ByteSize];
                if (bulk.Length > 0)
                {
                    var read = byteSource.Read(offset + 4, bulk, 0, bulk.Length);
                    if (read != bulk.Length)
                    {
                        throw new ArtifactInspectorException($"Unexpected end of data while reading a {bulk.Length}-byte array at offset {offset + 4}.");
                    }
                }

                var elements = new SnapshotField[count];
                for (var i = 0; i < count; i++)
                {
                    var elementBytes = new byte[elementTemplate.ByteSize];
                    Buffer.BlockCopy(bulk, i * elementTemplate.ByteSize, elementBytes, 0, elementTemplate.ByteSize);
                    elements[i] = SnapshotField.Scalar(elementTemplate, byteSource, offset + 4 + (long)i * elementTemplate.ByteSize, elementBytes);
                }

                return SnapshotField.ElementArray(node, byteSource, offset, elements, totalSize);
            }

            // Variable/struct-shaped elements (PPtr<T>[], nested arrays, ...): decode while accumulating
            // size, bailing out to a deferred field the moment the threshold is crossed. This keeps the
            // common (ends up under threshold) case a single walk instead of sizing with ComputeSize and
            // then decoding with a second, redundant walk.
            var variableElements = new SnapshotField[count];
            var currentOffset = offset + 4;
            for (var i = 0; i < count; i++)
            {
                var built = Build(elementTemplate, currentOffset, byteSource, options, depth + 1);
                currentOffset += built.ByteSize;
                if (elementTemplate.IsAligned) currentOffset = TypeTreeOffsetWalker.ApplyAlignment(elementTemplate, currentOffset);

                if (currentOffset - offset > options.MaxInlineFieldSizeBytes)
                {
                    var wholeArraySize = TypeTreeOffsetWalker.ComputeSize(node, offset, byteSource, depth);
                    return SnapshotField.Deferred(node, byteSource, offset, wholeArraySize);
                }

                variableElements[i] = built;
            }

            return SnapshotField.ElementArray(node, byteSource, offset, variableElements, currentOffset - offset);
        }

        private static SnapshotField BuildByteLikePayload(TypeTreeNode node, long offset, IRandomAccessByteSource byteSource, MaterializeOptions options, int contentLength)
        {
            if (contentLength > options.MaxInlineFieldSizeBytes)
            {
                return SnapshotField.Deferred(node, byteSource, offset, 4 + (long)contentLength);
            }

            if (contentLength == 0) return SnapshotField.ByteBlob(node, byteSource, offset, Array.Empty<byte>());

            var blob = new byte[contentLength];
            var dataOffset = offset + 4;
            var read = byteSource.Read(dataOffset, blob, 0, contentLength);
            if (read != contentLength)
            {
                throw new ArtifactInspectorException($"Unexpected end of data while reading a {contentLength}-byte value at offset {dataOffset}.");
            }

            return SnapshotField.ByteBlob(node, byteSource, offset, blob);
        }
    }
}
