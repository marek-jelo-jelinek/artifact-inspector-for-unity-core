using System;

namespace Tesearis.ArtifactInspectorForUnity.Core.TypeTree
{
    /// <summary>
    /// Controls how <see cref="Model.SerializedFile.MaterializeAll"/> and <see cref="Model.ObjectRef.Snapshot"/>
    /// decide, per field, whether to eagerly decode it into a cached <see cref="Model.ObjectSnapshot"/> or leave
    /// it deferred (a lazy reference, read on demand -- see <see cref="SnapshotField.IsDeferred"/>).
    /// </summary>
    public sealed class MaterializeOptions
    {
        /// <summary>
        /// The default options: a 1 KiB per-field threshold, no type filtering. A reasoned starting point --
        /// comfortably covers fixed structs (Vector3/Quaternion/Color/...) and typical short names/small
        /// reference arrays, while excluding mesh/texture/audio-scale payloads and unusually long strings/
        /// arrays -- not a value measured against real content, worth tuning if it doesn't hold up.
        /// </summary>
        public static MaterializeOptions Default => new();

        /// <summary>
        /// The largest byte size (any length prefix excluded) a single string/byte-blob/array field may have
        /// and still be eagerly decoded. Anything larger is left deferred. Defaults to 1024.
        /// </summary>
        public long MaxInlineFieldSizeBytes { get; set; } = 1024;

        /// <summary>
        /// When set, restricts materialization to objects whose <see cref="Model.ObjectRef.TypeId"/> this
        /// predicate returns true for -- e.g. only GameObject/Transform/MonoBehaviour, so Mesh/Texture2D/
        /// AudioClip objects in the same file are never eagerly decoded. Null (the default) means every object.
        /// </summary>
        public Func<int, bool> TypeIdFilter { get; set; }
    }
}
