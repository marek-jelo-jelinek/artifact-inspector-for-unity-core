using System;
using System.Collections.Generic;

namespace Tesearis.ArtifactInspectorForUnity.Core.Model
{
    /// <summary>Result of <see cref="SerializedFile.MaterializeAll"/>: how many objects were snapshotted, and which ones failed.</summary>
    public sealed class MaterializeResult
    {
        /// <summary>Objects successfully snapshotted (including ones that were already cached from an earlier call).</summary>
        public int SucceededCount { get; }

        /// <summary>Objects that threw while being snapshotted (e.g. an unsupported [SerializeReference] shape) -- one
        /// object's failure doesn't abort the rest of the pass.</summary>
        public IReadOnlyList<(long PathId, Exception Error)> Failures { get; }

        internal MaterializeResult(int succeededCount, IReadOnlyList<(long PathId, Exception Error)> failures)
        {
            SucceededCount = succeededCount;
            Failures = failures;
        }
    }
}
