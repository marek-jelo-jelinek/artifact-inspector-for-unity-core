using Tesearis.ArtifactInspectorForUnity.Core.Model;

namespace Tesearis.ArtifactInspectorForUnity.Core.Adapters
{
    /// <summary>An object no registered IArtifactAdapter claimed.</summary>
    public sealed class RawObject
    {
        public ObjectRef ObjectRef { get; }
        public string ClassName { get; }

        public RawObject(ObjectRef objectRef, string className)
        {
            ObjectRef = objectRef;
            ClassName = className;
        }
    }
}
