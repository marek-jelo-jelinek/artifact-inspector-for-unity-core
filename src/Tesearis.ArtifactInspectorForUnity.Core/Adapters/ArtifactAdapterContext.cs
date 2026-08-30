using System;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.Adapters
{
    /// <summary>
    /// The data an IArtifactAdapter uses to inspect and decode one object.
    /// </summary>
    public readonly struct ArtifactAdapterContext
    {
        public ObjectRef ObjectRef { get; }
        public TypeTreeReader Reader { get; }

        public SerializedFile SerializedFile { get; }
        public ArtifactArchive Archive { get; }

        public ArtifactAdapterContext(ObjectRef objectRef, TypeTreeReader reader, SerializedFile serializedFile, ArtifactArchive archive)
        {
            ObjectRef = objectRef;
            Reader = reader ?? throw new ArgumentNullException(nameof(reader));
            SerializedFile = serializedFile;
            Archive = archive;
        }
    }
}