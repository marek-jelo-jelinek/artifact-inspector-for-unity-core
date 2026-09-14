using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.Model
{
    /// <summary>
    /// A reference to one object inside a SerializedFile.
    /// </summary>
    public readonly struct ObjectRef
    {
        private readonly SerializedFile _owner;

        public long PathId { get; }
        public int TypeId { get; }
        public long ByteOffset { get; }
        public long ByteSize { get; }

        internal ObjectRef(SerializedFile owner, long pathId, int typeId, long byteOffset, long byteSize)
        {
            _owner = owner;
            PathId = pathId;
            TypeId = typeId;
            ByteOffset = byteOffset;
            ByteSize = byteSize;
        }

        public string ClassName => GetReader().TypeName;

        public TypeTreeReader GetReader()
        {
            return _owner.CreateReader(PathId, ByteOffset);
        }

        /// <summary>
        /// A cached, mostly-eager decoding of this object's fields (see <see cref="ObjectSnapshot"/>). The
        /// first call does one forward walk; every later call for this object -- from any caller -- is a
        /// pure dictionary lookup, unlike <see cref="GetReader"/>, which builds a fresh reader (and throws
        /// away its offset memoization) every time.
        /// </summary>
        public ObjectSnapshot Snapshot(MaterializeOptions options = null)
        {
            return _owner.GetOrBuildSnapshotForRef(this, options);
        }
    }
}