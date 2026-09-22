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

        /// <summary>
        /// A cached, mostly-eager decoding of this object's fields (see <see cref="ObjectSnapshot"/>), for an
        /// adapter that needs repeated or cross-object field lookups (e.g. resolving a sibling object's name
        /// several times). Most adapters, which read each object's own fields once via <see cref="Reader"/>,
        /// don't need this.
        /// </summary>
        public ObjectSnapshot Snapshot(MaterializeOptions options = null)
        {
            if (SerializedFile == null)
            {
                throw new InvalidOperationException("Cannot build a snapshot without an owning SerializedFile.");
            }

            return SerializedFile.GetOrBuildSnapshotForRef(ObjectRef, options);
        }
    }
}