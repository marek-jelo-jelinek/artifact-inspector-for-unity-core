using System;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.Model
{
    /// <summary>
    /// Unity's on-disk object reference (`PPtr&lt;T&gt;`): an index into the containing file's
    /// external-reference table (<see cref="FileId"/>; 0 means "this file") plus the target
    /// object's path ID within that file (<see cref="PathId"/>).
    /// </summary>
    public readonly struct PPtr
    {
        public int FileId { get; }
        public long PathId { get; }

        public PPtr(int fileId, long pathId)
        {
            FileId = fileId;
            PathId = pathId;
        }

        /// <summary>True when both fields are zero - Unity's "no reference" encoding.</summary>
        public bool IsNull => FileId == 0 && PathId == 0;

        /// <summary>True when this reference targets an object in the same file (resolve via <see cref="SerializedFile.TryGetObject"/>).</summary>
        public bool IsLocal => FileId == 0;

        /// <summary>
        /// Resolves this reference to a cached <see cref="ObjectSnapshot"/> in <paramref name="owningFile"/>,
        /// when it's <see cref="IsLocal"/> and the target PathId exists. Once the target is warmed (cached),
        /// chasing this reference again -- e.g. several sibling components resolving their owning
        /// GameObject -- costs zero further byte-source reads; see <see cref="SerializedFile.TryGetSnapshot"/>.
        /// Cross-file references (<see cref="IsLocal"/> false) aren't resolved by this library; always returns false for those.
        /// </summary>
        public bool TryResolveSnapshot(SerializedFile owningFile, out ObjectSnapshot snapshot, MaterializeOptions options = null)
        {
            if (owningFile == null) throw new ArgumentNullException(nameof(owningFile));

            if (!IsLocal)
            {
                snapshot = default;
                return false;
            }

            return owningFile.TryGetSnapshot(PathId, out snapshot, options);
        }
    }
}
