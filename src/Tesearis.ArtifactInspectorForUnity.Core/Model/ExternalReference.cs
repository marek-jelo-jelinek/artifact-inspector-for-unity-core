namespace Tesearis.ArtifactInspectorForUnity.Core.Model
{
    /// <summary>
    /// One entry in a SerializedFile's external-reference table.
    /// </summary>
    public readonly struct ExternalReference
    {
        public string Path { get; }
        public string Guid { get; }
        public ExternalReferenceType Type { get; }

        internal ExternalReference(string path, string guid, ExternalReferenceType type)
        {
            Path = path;
            Guid = guid;
            Type = type;
        }
    }
}
