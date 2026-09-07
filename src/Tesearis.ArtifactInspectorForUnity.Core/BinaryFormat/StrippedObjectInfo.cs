namespace Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat
{
    /// <summary>
    /// One entry from a SerializedFile's object table, parsed directly off bytes with no TypeTree
    /// involved. Deliberately a separate type from <see cref="Model.ObjectRef"/> rather than a
    /// variant of it: <see cref="Model.ObjectRef.GetReader()"/> needs a live native TypeTree, which
    /// a stripped file fundamentally cannot provide, so this type exposes no reader.
    /// </summary>
    public readonly struct StrippedObjectInfo
    {
        public long PathId { get; }
        public int TypeId { get; }
        public long ByteOffset { get; }
        public long ByteSize { get; }

        /// <summary>The class name for <see cref="TypeId"/>, via <see cref="TypeIdRegistry"/>.</summary>
        public string ClassName => TypeIdRegistry.GetTypeName(TypeId);

        /// <summary>
        /// The object's <c>m_Name</c>, read directly off bytes with no TypeTree -- best-effort, and
        /// only attempted for classes where the field's byte position is reliably known without one
        /// (see <see cref="StrippedObjectNameReader"/>). <c>null</c> for every other class, and for
        /// an attempt that didn't look like a valid name.
        /// </summary>
        public string Name { get; }

        internal StrippedObjectInfo(long pathId, int typeId, long byteOffset, long byteSize, string name)
        {
            PathId = pathId;
            TypeId = typeId;
            ByteOffset = byteOffset;
            ByteSize = byteSize;
            Name = name;
        }
    }
}
