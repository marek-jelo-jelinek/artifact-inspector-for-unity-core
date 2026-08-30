using System;

namespace Tesearis.ArtifactInspectorForUnity.Core.Adapters
{
    /// <summary>Decides whether it recognizes a candidate object and, if so, decodes it into a typed result.</summary>
    public interface IArtifactAdapter
    {
        /// <summary>The type this adapter's Read produces.</summary>
        Type ResultType { get; }

        bool Matches(ArtifactAdapterContext context);

        /// <summary>Decodes the object. Called only after Matches returns true.</summary>
        object Read(ArtifactAdapterContext context);
    }
}