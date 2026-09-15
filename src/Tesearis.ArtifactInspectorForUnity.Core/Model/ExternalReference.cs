using System;

namespace Tesearis.ArtifactInspectorForUnity.Core.Model
{
    /// <summary>
    /// One entry in a SerializedFile's external-reference table.
    /// </summary>
    public readonly struct ExternalReference : IEquatable<ExternalReference>
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

        public bool Equals(ExternalReference other)
        {
            return string.Equals(Path, other.Path, StringComparison.Ordinal) &&
                   string.Equals(Guid, other.Guid, StringComparison.Ordinal) &&
                   Type == other.Type;
        }

        public override bool Equals(object obj)
        {
            return obj is ExternalReference other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Path != null ? StringComparer.Ordinal.GetHashCode(Path) : 0;
                hash = (hash * 397) ^ (Guid != null ? StringComparer.Ordinal.GetHashCode(Guid) : 0);
                hash = (hash * 397) ^ (int)Type;
                return hash;
            }
        }

        public static bool operator ==(ExternalReference left, ExternalReference right) => left.Equals(right);

        public static bool operator !=(ExternalReference left, ExternalReference right) => !left.Equals(right);

        public override string ToString() => $"ExternalReference(Path: '{Path}', Guid: {Guid}, Type: {Type})";
    }
}
