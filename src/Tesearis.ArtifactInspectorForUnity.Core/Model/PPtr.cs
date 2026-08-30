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
    }
}
