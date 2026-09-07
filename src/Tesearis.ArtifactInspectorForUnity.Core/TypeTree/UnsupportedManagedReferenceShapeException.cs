namespace Tesearis.ArtifactInspectorForUnity.Core.TypeTree
{
    /// <summary>
    /// Thrown by <see cref="TypeTreeNode"/>'s constructor when a node uses an unsupported
    /// managed-reference shape ([SerializeReference] polymorphic field), so its offsets cannot
    /// be computed. See README's "Known limitations".
    /// </summary>
    public sealed class UnsupportedManagedReferenceShapeException : ArtifactInspectorException
    {
        public string Name { get; }
        public string TypeName { get; }

        internal UnsupportedManagedReferenceShapeException(string name, string typeName) : base(
            $"Type tree node '{name}' ({typeName}) uses an unsupported managed-reference shape ([SerializeReference] polymorphic field) so offsets cannot be computed for this node.")
        {
            Name = name;
            TypeName = typeName;
        }
    }
}