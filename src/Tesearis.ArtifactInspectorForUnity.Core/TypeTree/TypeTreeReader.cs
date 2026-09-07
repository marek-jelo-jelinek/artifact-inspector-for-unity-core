using System;
using System.Collections.Generic;
using System.Text;
using Tesearis.ArtifactInspectorForUnity.Core.Model;

namespace Tesearis.ArtifactInspectorForUnity.Core.TypeTree
{
    /// <summary>A lazy, random-access reader over one type-tree node's data at a given byte offset.</summary>
    public sealed class TypeTreeReader
    {
        private readonly TypeTreeNode _node;
        private readonly IRandomAccessByteSource _byteSource;

        private long? _size;

        // Struct-field resolution state.
        private readonly OffsetCursor _childCursor;

        // Array-element resolution state, initialized lazily on first use (needs ByteOffset + 4,
        // past the length prefix, which isn't known to be valid until an array access is made).
        private OffsetCursor _elementCursor;

        internal TypeTreeReader(TypeTreeNode node, IRandomAccessByteSource byteSource, long offset)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _byteSource = byteSource ?? throw new ArgumentNullException(nameof(byteSource));
            ByteOffset = offset;
            _childCursor = new OffsetCursor(offset);
        }

        /// <summary>This field's byte offset into the underlying byte source.</summary>
        public long ByteOffset { get; }

        /// <summary>The serialized type name of this field/object (e.g. "Texture2D", "int", "string").</summary>
        public string TypeName => _node.TypeName;

        /// <summary>This field's size in bytes, computed (and cached) by walking its type tree.</summary>
        public long ByteSize
        {
            get
            {
                _size ??= TypeTreeOffsetWalker.ComputeSize(_node, ByteOffset, _byteSource);
                return _size.Value;
            }
        }

        /// <summary>Whether a child field named <paramref name="fieldName"/> exists on this object/struct.</summary>
        public bool HasField(string fieldName)
        {
            return TryGetField(fieldName, out _);
        }

        /// <summary>Looks up a child field by name, without throwing if it's absent.</summary>
        public bool TryGetField(string fieldName, out TypeTreeReader field)
        {
            if (fieldName == null) throw new ArgumentNullException(nameof(fieldName));

            for (var i = 0; i < _node.Children.Count; i++)
            {
                if (!string.Equals(_node.Children[i].Name, fieldName, StringComparison.Ordinal)) continue;
                field = GetChildByIndex(i);
                return true;
            }

            field = null;
            return false;
        }

        /// <summary>Looks up a child field by name.</summary>
        /// <exception cref="ArtifactInspectorException">No field named <paramref name="fieldName"/> exists on this object/struct.</exception>
        public TypeTreeReader Field(string fieldName)
        {
            return !TryGetField(fieldName, out var field)
                ? throw new ArtifactInspectorException($"Field '{fieldName}' was not found on type '{_node.TypeName}'.")
                : field;
        }

        /// <summary>The number of elements in this array/vector/map-shaped field.</summary>
        /// <exception cref="ArtifactInspectorException">This field isn't array-like, or its length prefix is negative or truncated.</exception>
        public int ArrayLength()
        {
            RequireArray();
            return TypeTreeOffsetWalker.ReadValidatedLengthPrefix(_byteSource, ByteOffset, "array");
        }

        /// <summary>The element at <paramref name="index"/> of this array/vector/map-shaped field.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative or beyond <see cref="ArrayLength"/>.</exception>
        public TypeTreeReader Element(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index), index, "Index must not be negative.");

            var length = ArrayLength();
            return index >= length
                ? throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be less than the array length ({length}).")
                : GetElementByIndex(index);
        }

        /// <summary>Every element of this array/vector/map-shaped field, in order.</summary>
        public IEnumerable<TypeTreeReader> Elements()
        {
            var count = ArrayLength();
            for (var i = 0; i < count; i++) yield return Element(i);
        }

        /// <summary>Copies this field's raw bytes.</summary>
        /// <exception cref="ArtifactInspectorException">The field is too large to copy into a single <see cref="byte"/>[], or the underlying data is truncated.</exception>
        public byte[] ReadRawBytes()
        {
            var size = ByteSize;
            return size > int.MaxValue
                ? throw new ArtifactInspectorException($"Field is too large to read into a single byte[] ({size} bytes).")
                : ReadRawBytes(0, (int)size);
        }

        /// <summary>Copies count raw bytes starting relativeOffset bytes into this field's data.</summary>
        /// <exception cref="ArtifactInspectorException">The underlying data has fewer than <paramref name="count"/> bytes remaining at <paramref name="relativeOffset"/>.</exception>
        public byte[] ReadRawBytes(long relativeOffset, int count)
        {
            if (count < 0) throw new ArtifactInspectorException($"count must not be negative ({count}).");
            if (count == 0) return Array.Empty<byte>();

            TypeTreeOffsetWalker.RequireCountFitsRemainingBytes(count, ByteOffset + relativeOffset, _byteSource);

            var buffer = new byte[count];
            var read = _byteSource.Read(ByteOffset + relativeOffset, buffer, 0, count);
            if (read != count)
            {
                throw new ArtifactInspectorException(
                    $"Unexpected end of data while reading {count} raw bytes at offset {ByteOffset + relativeOffset}.");
            }

            return buffer;
        }

        /// <summary>Reads this field's value as an <see cref="int"/>.</summary>
        /// <exception cref="ArtifactInspectorException">The underlying data is truncated.</exception>
        public int AsInt32()
        {
            return ReadFixed(4, BitConverter.ToInt32);
        }

        /// <summary>Reads this field's value as a <see cref="uint"/>.</summary>
        /// <exception cref="ArtifactInspectorException">The underlying data is truncated.</exception>
        public uint AsUInt32()
        {
            return ReadFixed(4, BitConverter.ToUInt32);
        }

        /// <summary>Reads this field's value as a <see cref="long"/>.</summary>
        /// <exception cref="ArtifactInspectorException">The underlying data is truncated.</exception>
        public long AsInt64()
        {
            return ReadFixed(8, BitConverter.ToInt64);
        }

        /// <summary>Reads this field's value as a <see cref="ulong"/>.</summary>
        /// <exception cref="ArtifactInspectorException">The underlying data is truncated.</exception>
        public ulong AsUInt64()
        {
            return ReadFixed(8, BitConverter.ToUInt64);
        }

        /// <summary>Reads this field's value as a <see cref="float"/>.</summary>
        /// <exception cref="ArtifactInspectorException">The underlying data is truncated.</exception>
        public float AsSingle()
        {
            return ReadFixed(4, BitConverter.ToSingle);
        }

        /// <summary>Reads this field's value as a <see cref="short"/>.</summary>
        /// <exception cref="ArtifactInspectorException">The underlying data is truncated.</exception>
        public short AsInt16()
        {
            return ReadFixed(2, BitConverter.ToInt16);
        }

        /// <summary>Reads this field's value as a <see cref="ushort"/>.</summary>
        /// <exception cref="ArtifactInspectorException">The underlying data is truncated.</exception>
        public ushort AsUInt16()
        {
            return ReadFixed(2, BitConverter.ToUInt16);
        }

        /// <summary>Reads this field's value as an <see cref="sbyte"/>.</summary>
        /// <exception cref="ArtifactInspectorException">The underlying data is truncated.</exception>
        public sbyte AsSByte()
        {
            return ReadFixed(1, (buffer, i) => (sbyte)buffer[i]);
        }

        /// <summary>Reads this field's value as a <see cref="double"/>.</summary>
        /// <exception cref="ArtifactInspectorException">The underlying data is truncated.</exception>
        public double AsDouble()
        {
            return ReadFixed(8, BitConverter.ToDouble);
        }

        /// <summary>Reads this field's value as a <see cref="byte"/>.</summary>
        /// <exception cref="ArtifactInspectorException">The underlying data is truncated.</exception>
        public byte AsByte()
        {
            return ReadFixed(1, (buffer, i) => buffer[i]);
        }

        /// <summary>Reads this field's value as a <see cref="bool"/> (a nonzero byte).</summary>
        /// <exception cref="ArtifactInspectorException">The underlying data is truncated.</exception>
        public bool AsBoolean()
        {
            return AsByte() != 0;
        }

        /// <summary>Reads this field's value as a UTF-8-decoded, length-prefixed <see cref="string"/>.</summary>
        /// <exception cref="ArtifactInspectorException">The length prefix is negative, or the underlying data is truncated.</exception>
        public string AsString()
        {
            var length = TypeTreeOffsetWalker.ReadValidatedLengthPrefix(_byteSource, ByteOffset, "string");
            if (length == 0) return string.Empty;

            var buffer = new byte[length];
            var read = _byteSource.Read(ByteOffset + 4, buffer, 0, length);
            return read != length
                ? throw new ArtifactInspectorException($"Unexpected end of data while reading a {length}-byte string at offset {ByteOffset + 4}.")
                : Encoding.UTF8.GetString(buffer);
        }

        /// <summary>Reads this field as a PPtr (`m_FileID` + `m_PathID`).</summary>
        /// <exception cref="ArtifactInspectorException">This field has no `m_FileID`/`m_PathID` children, or the underlying data is truncated.</exception>
        public PPtr AsPPtr()
        {
            return new PPtr(Field("m_FileID").AsInt32(), Field("m_PathID").AsInt64());
        }

        private T ReadFixed<T>(int byteCount, Func<byte[], int, T> convert)
        {
            var buffer = new byte[byteCount];
            var read = _byteSource.Read(ByteOffset, buffer, 0, byteCount);
            return read != byteCount
                ? throw new ArtifactInspectorException($"Unexpected end of data while reading a {byteCount}-byte value at offset {ByteOffset}.")
                : convert(buffer, 0);
        }

        private void RequireArray()
        {
            if (!_node.IsArrayLike) throw new ArtifactInspectorException($"Type '{_node.TypeName}' is not an array/vector/map field.");
        }

        private TypeTreeReader GetChildByIndex(int index)
        {
            var offset = _childCursor.ResolveOffset(index, i => _node.Children[i], _byteSource);
            return new TypeTreeReader(_node.Children[index], _byteSource, offset);
        }

        private TypeTreeReader GetElementByIndex(int index)
        {
            if (_node.Children.Count < 2)
            {
                throw new ArtifactInspectorException(
                    $"Array-like type tree node '{_node.Name}' ({_node.TypeName}) has {_node.Children.Count} children; expected 2 (size and element template).");
            }

            var elementTemplate = _node.Children[1];
            _elementCursor ??= new OffsetCursor(ByteOffset + 4); // past the length prefix

            var offset = _elementCursor.ResolveOffset(index, _ => elementTemplate, _byteSource);
            return new TypeTreeReader(elementTemplate, _byteSource, offset);
        }

        /// <summary>
        /// Resolves the byte offset of the node at a given index within a sequence (struct fields, or
        /// array elements) that starts at a fixed offset, walking forward through -- and caching -- every
        /// lower index first: each node's size must be computed to know where the next one starts, so
        /// resolving index N inevitably resolves every index below it too. Shared by
        /// <see cref="GetChildByIndex"/> (a distinct node per index) and <see cref="GetElementByIndex"/>
        /// (the same element-template node repeated).
        /// </summary>
        private sealed class OffsetCursor
        {
            private readonly Dictionary<int, long> _resolvedOffsets = new();
            private int _nextIndex;
            private long _nextOffset;

            public OffsetCursor(long startOffset)
            {
                _nextOffset = startOffset;
            }

            public long ResolveOffset(int index, Func<int, TypeTreeNode> nodeAt, IRandomAccessByteSource byteSource)
            {
                if (_resolvedOffsets.TryGetValue(index, out var cachedOffset)) return cachedOffset;

                while (_nextIndex <= index)
                {
                    var node = nodeAt(_nextIndex);
                    var offset = _nextOffset;
                    var size = TypeTreeOffsetWalker.ComputeSize(node, offset, byteSource);

                    _resolvedOffsets[_nextIndex] = offset;

                    _nextOffset = offset + size;
                    if (node.IsAligned) _nextOffset = TypeTreeOffsetWalker.ApplyAlignment(node, _nextOffset);

                    _nextIndex++;
                }

                return _resolvedOffsets[index];
            }
        }
    }
}