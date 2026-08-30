namespace Tesearis.ArtifactInspectorForUnity.Core.Native
{
    /// <summary>One type-tree node, as returned by UFS_GetTypeTreeNodeInfo.</summary>
    internal readonly struct TypeTreeNodeInfo
    {
        public string TypeName { get; }
        public string FieldName { get; }
        public int Offset { get; }
        public int Size { get; }
        public TypeTreeFlags Flags { get; }
        public TypeTreeMetaFlags MetaFlags { get; }
        public int FirstChildNode { get; }
        public int NextNode { get; }

        public TypeTreeNodeInfo(
            string typeName,
            string fieldName,
            int offset,
            int size,
            TypeTreeFlags flags,
            TypeTreeMetaFlags metaFlags,
            int firstChildNode,
            int nextNode)
        {
            TypeName = typeName;
            FieldName = fieldName;
            Offset = offset;
            Size = size;
            Flags = flags;
            MetaFlags = metaFlags;
            FirstChildNode = firstChildNode;
            NextNode = nextNode;
        }
    }
}