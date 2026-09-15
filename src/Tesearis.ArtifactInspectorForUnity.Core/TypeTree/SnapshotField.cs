using System;
using System.Collections.Generic;
using System.Text;
using Tesearis.ArtifactInspectorForUnity.Core.Model;

namespace Tesearis.ArtifactInspectorForUnity.Core.TypeTree
{
    /// <summary>
    /// One field's value from a <see cref="SnapshotBuilder"/> pass: either eagerly decoded (scalar/string/
    /// small array/struct), or a deferred marker over a field whose size exceeded
    /// <see cref="MaterializeOptions.MaxInlineFieldSizeBytes"/>. Mirrors <see cref="TypeTreeReader"/>'s field
    /// access surface so call sites read the same either way -- a deferred field's accessors simply delegate
    /// to a live <see cref="TypeTreeReader"/> built over its offset, and any accessor that doesn't match this
    /// field's eagerly-cached shape (e.g. calling <see cref="AsInt64"/> on a field whose cached bytes are
    /// shorter than 8) falls back the same way, so nothing here is less capable than the raw lazy reader --
    /// only, for the common cases, faster on repeat access.
    /// </summary>
    public sealed class SnapshotField
    {
        private enum Kind
        {
            Scalar,   // fixed-size leaf: raw bytes, reinterpreted per accessor like TypeTreeReader.ReadFixed
            ByteBlob, // array-like node with a fixed 1-byte element template (string, TypelessData, byte[])
            Elements, // array-like node with a non-1-byte element template, decoded element-by-element
            Struct,   // named children, in declaration order
            Deferred  // over threshold (or delegated through another deferred field) -- always lazy
        }

        private readonly Kind _kind;
        private readonly TypeTreeNode _node;
        private readonly IRandomAccessByteSource _byteSource;
        private readonly long _byteOffset;
        private long? _byteSize;

        private readonly byte[] _scalarBytes; // Kind.Scalar
        private readonly byte[] _blob; // Kind.ByteBlob: array/string content, length prefix excluded
        private readonly SnapshotField[] _elements; // Kind.Elements
        private readonly SnapshotField[] _children; // Kind.Struct
        private readonly TypeTreeReader _deferredReader; // Kind.Deferred

        private SnapshotField(Kind kind, TypeTreeNode node, IRandomAccessByteSource byteSource, long byteOffset, long? byteSize,
            byte[] scalarBytes = null, byte[] blob = null, SnapshotField[] elements = null, SnapshotField[] children = null,
            TypeTreeReader deferredReader = null)
        {
            _kind = kind;
            _node = node;
            _byteSource = byteSource;
            _byteOffset = byteOffset;
            _byteSize = byteSize;
            _scalarBytes = scalarBytes;
            _blob = blob;
            _elements = elements;
            _children = children;
            _deferredReader = deferredReader;
        }

        internal static SnapshotField Scalar(TypeTreeNode node, IRandomAccessByteSource byteSource, long offset, byte[] rawBytes)
        {
            return new SnapshotField(Kind.Scalar, node, byteSource, offset, rawBytes.Length, scalarBytes: rawBytes);
        }

        internal static SnapshotField ByteBlob(TypeTreeNode node, IRandomAccessByteSource byteSource, long offset, byte[] blob)
        {
            return new SnapshotField(Kind.ByteBlob, node, byteSource, offset, 4 + blob.Length, blob: blob);
        }

        internal static SnapshotField ElementArray(TypeTreeNode node, IRandomAccessByteSource byteSource, long offset, SnapshotField[] elements, long byteSize)
        {
            return new SnapshotField(Kind.Elements, node, byteSource, offset, byteSize, elements: elements);
        }

        internal static SnapshotField StructNode(TypeTreeNode node, IRandomAccessByteSource byteSource, long offset, SnapshotField[] children, long byteSize)
        {
            return new SnapshotField(Kind.Struct, node, byteSource, offset, byteSize, children: children);
        }

        /// <summary>A field deliberately left un-decoded because it's too large -- byteSize is already known
        /// (whoever's building this already had to compute or read it to make that decision), so it's passed
        /// through rather than recomputed.</summary>
        internal static SnapshotField Deferred(TypeTreeNode node, IRandomAccessByteSource byteSource, long offset, long byteSize)
        {
            return new SnapshotField(Kind.Deferred, node, byteSource, offset, byteSize, deferredReader: new TypeTreeReader(node, byteSource, offset));
        }

        /// <summary>Wraps a reader obtained by delegating through an already-deferred field's Field()/Element() --
        /// everything below a deferred boundary stays lazy too, rather than eagerly re-decoding partway through.</summary>
        private static SnapshotField WrapDeferred(TypeTreeReader reader)
        {
            return new SnapshotField(Kind.Deferred, node: null, byteSource: null, byteOffset: 0, byteSize: null, deferredReader: reader);
        }

        /// <summary>True when this field's value was left as a lazy reference instead of eagerly decoded.</summary>
        public bool IsDeferred => _kind == Kind.Deferred;

        /// <summary>The serialized type name of this field (e.g. "Texture2D", "int", "string").</summary>
        public string TypeName => _kind == Kind.Deferred ? _deferredReader.TypeName : _node.TypeName;

        /// <summary>This field's byte offset into the underlying byte source.</summary>
        public long ByteOffset => _kind == Kind.Deferred ? _deferredReader.ByteOffset : _byteOffset;

        /// <summary>This field's size in bytes.</summary>
        public long ByteSize
        {
            get
            {
                _byteSize ??= _deferredReader.ByteSize;
                return _byteSize.Value;
            }
        }

        /// <summary>An escape hatch back to a live, lazy reader over this field's byte range -- e.g. to stream a huge deferred field without going through the cached accessors below.</summary>
        public TypeTreeReader ToReader()
        {
            return _kind == Kind.Deferred ? _deferredReader : new TypeTreeReader(_node, _byteSource, _byteOffset);
        }

        /// <summary>Whether a child field named <paramref name="fieldName"/> exists on this object/struct.</summary>
        public bool HasField(string fieldName)
        {
            return TryGetField(fieldName, out _);
        }

        /// <summary>Looks up a child field by name, without throwing if it's absent.</summary>
        public bool TryGetField(string fieldName, out SnapshotField field)
        {
            if (fieldName == null) throw new ArgumentNullException(nameof(fieldName));

            if (_kind == Kind.Deferred)
            {
                if (_deferredReader.TryGetField(fieldName, out var reader))
                {
                    field = WrapDeferred(reader);
                    return true;
                }

                field = null;
                return false;
            }

            if (_kind == Kind.Struct)
            {
                for (var i = 0; i < _node.Children.Count; i++)
                {
                    if (!string.Equals(_node.Children[i].Name, fieldName, StringComparison.Ordinal)) continue;
                    field = _children[i];
                    return true;
                }
            }

            field = null;
            return false;
        }

        /// <summary>Looks up a child field by name.</summary>
        /// <exception cref="ArtifactInspectorException">No field named <paramref name="fieldName"/> exists on this object/struct.</exception>
        public SnapshotField Field(string fieldName)
        {
            return !TryGetField(fieldName, out var field)
                ? throw new ArtifactInspectorException($"Field '{fieldName}' was not found on type '{TypeName}'.")
                : field;
        }

        /// <summary>The number of elements in this array/vector/map-shaped field.</summary>
        /// <exception cref="ArtifactInspectorException">This field isn't array-like.</exception>
        public int ArrayLength()
        {
            return _kind switch
            {
                Kind.Deferred => _deferredReader.ArrayLength(),
                Kind.ByteBlob when _node?.IsArrayLike == true => _blob.Length,
                Kind.ByteBlob => throw new ArtifactInspectorException($"Type '{TypeName}' is not an array/vector/map field."),
                Kind.Elements => _elements.Length,
                _ => throw new ArtifactInspectorException($"Type '{TypeName}' is not an array/vector/map field."),
            };
        }

        /// <summary>The element at <paramref name="index"/> of this array/vector/map-shaped field.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative or beyond <see cref="ArrayLength"/>.</exception>
        public SnapshotField Element(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index), index, "Index must not be negative.");

            var length = ArrayLength(); // also validates array-ness
            if (index >= length) throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be less than the array length ({length}).");

            return _kind switch
            {
                Kind.Deferred => WrapDeferred(_deferredReader.Element(index)),
                Kind.ByteBlob when _node?.Children.Count >= 2 => Scalar(_node.Children[1], _byteSource, _byteOffset + 4 + index, new[] { _blob[index] }),
                Kind.ByteBlob => throw new ArtifactInspectorException($"Type '{TypeName}' is not an array/vector/map field."),
                Kind.Elements => _elements[index],
                _ => throw new ArtifactInspectorException($"Type '{TypeName}' is not an array/vector/map field."), // unreachable: ArrayLength() already threw
            };
        }

        /// <summary>Every element of this array/vector/map-shaped field, in order.</summary>
        public IEnumerable<SnapshotField> Elements()
        {
            var count = ArrayLength();
            for (var i = 0; i < count; i++) yield return Element(i);
        }

        /// <summary>Copies this field's raw bytes.</summary>
        public byte[] ReadRawBytes()
        {
            return _kind switch
            {
                Kind.Scalar => (byte[])_scalarBytes.Clone(),
                Kind.ByteBlob => BuildBlobWithLengthPrefix(),
                _ => ToReader().ReadRawBytes(),
            };
        }

        /// <summary>Copies count raw bytes starting relativeOffset bytes into this field's data.</summary>
        public byte[] ReadRawBytes(long relativeOffset, int count)
        {
            // Always via a live reader: the in-memory fast paths above cover the common no-offset case
            // (the one this feature optimizes for); an arbitrary offset/count is a rare escape hatch not
            // worth a second, cached code path to keep in sync with TypeTreeReader's exact semantics.
            return ToReader().ReadRawBytes(relativeOffset, count);
        }

        public int AsInt32() => _kind == Kind.Scalar && _scalarBytes.Length >= 4
            ? BitConverter.ToInt32(_scalarBytes, 0)
            : ToReader().AsInt32();

        public uint AsUInt32() => _kind == Kind.Scalar && _scalarBytes.Length >= 4
            ? BitConverter.ToUInt32(_scalarBytes, 0)
            : ToReader().AsUInt32();

        public long AsInt64() => _kind == Kind.Scalar && _scalarBytes.Length >= 8
            ? BitConverter.ToInt64(_scalarBytes, 0)
            : ToReader().AsInt64();

        public ulong AsUInt64() => _kind == Kind.Scalar && _scalarBytes.Length >= 8
            ? BitConverter.ToUInt64(_scalarBytes, 0)
            : ToReader().AsUInt64();

        public float AsSingle() => _kind == Kind.Scalar && _scalarBytes.Length >= 4
            ? BitConverter.ToSingle(_scalarBytes, 0)
            : ToReader().AsSingle();

        public double AsDouble() => _kind == Kind.Scalar && _scalarBytes.Length >= 8
            ? BitConverter.ToDouble(_scalarBytes, 0)
            : ToReader().AsDouble();

        public short AsInt16() => _kind == Kind.Scalar && _scalarBytes.Length >= 2
            ? BitConverter.ToInt16(_scalarBytes, 0)
            : ToReader().AsInt16();

        public ushort AsUInt16() => _kind == Kind.Scalar && _scalarBytes.Length >= 2
            ? BitConverter.ToUInt16(_scalarBytes, 0)
            : ToReader().AsUInt16();

        public byte AsByte() => _kind == Kind.Scalar && _scalarBytes.Length >= 1
            ? _scalarBytes[0]
            : ToReader().AsByte();

        public sbyte AsSByte() => _kind == Kind.Scalar && _scalarBytes.Length >= 1
            ? (sbyte)_scalarBytes[0]
            : ToReader().AsSByte();

        public bool AsBoolean() => AsByte() != 0;

        /// <summary>Reads this field's value as a UTF-8-decoded string.</summary>
        public string AsString()
        {
            return _kind switch
            {
                Kind.ByteBlob => Encoding.UTF8.GetString(_blob),
                Kind.Deferred => _deferredReader.AsString(),
                _ => ToReader().AsString(),
            };
        }

        /// <summary>Reads this field as a PPtr (`m_FileID` + `m_PathID`).</summary>
        public PPtr AsPPtr()
        {
            return new PPtr(Field("m_FileID").AsInt32(), Field("m_PathID").AsInt64());
        }

        private byte[] BuildBlobWithLengthPrefix()
        {
            var result = new byte[4 + _blob.Length];
            BitConverter.GetBytes(_blob.Length).CopyTo(result, 0);
            _blob.CopyTo(result, 4);
            return result;
        }
    }
}
