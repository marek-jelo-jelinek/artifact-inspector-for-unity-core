using System.Collections.Generic;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.Model
{
    /// <summary>
    /// A cached, mostly-eager decoding of one object's fields, built once by <see cref="SerializedFile.TryGetSnapshot"/>/
    /// <see cref="ObjectRef.Snapshot"/> and reused for every later lookup of the same object -- the fix for
    /// repeatedly re-walking the same object's type tree from scratch (see <see cref="ObjectRef.GetReader"/>'s
    /// doc comment). Structurally parallel to <see cref="ObjectRef"/>+<see cref="TypeTreeReader"/>: the same
    /// field-access calls read the same either way, e.g. <c>objectRef.Snapshot().Field("m_Name").AsString()</c>.
    /// </summary>
    public readonly struct ObjectSnapshot
    {
        private readonly SnapshotField _root;

        public long PathId { get; }
        public int TypeId { get; }
        public long ByteOffset { get; }
        public long ByteSize { get; }

        internal ObjectSnapshot(long pathId, int typeId, long byteOffset, long byteSize, SnapshotField root)
        {
            PathId = pathId;
            TypeId = typeId;
            ByteOffset = byteOffset;
            ByteSize = byteSize;
            _root = root;
        }

        public string ClassName => _root.TypeName;

        public bool HasField(string fieldName) => _root.HasField(fieldName);

        public bool TryGetField(string fieldName, out SnapshotField field) => _root.TryGetField(fieldName, out field);

        public SnapshotField Field(string fieldName) => _root.Field(fieldName);

        public int ArrayLength() => _root.ArrayLength();

        public SnapshotField Element(int index) => _root.Element(index);

        public IEnumerable<SnapshotField> Elements() => _root.Elements();

        public byte[] ReadRawBytes() => _root.ReadRawBytes();

        public byte[] ReadRawBytes(long relativeOffset, int count) => _root.ReadRawBytes(relativeOffset, count);
    }
}
