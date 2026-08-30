using System;
using Tesearis.ArtifactInspectorForUnity.Core.Adapters;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport
{
    /// <summary>A configurable <see cref="IArtifactAdapter"/> stand-in for registry/dispatch tests.</summary>
    internal sealed class StubArtifactAdapter : IArtifactAdapter
    {
        internal Func<ArtifactAdapterContext, bool> MatchesFunc { get; set; } = _ => false;
        internal Func<ArtifactAdapterContext, object> ReadFunc { get; set; } = _ => null;

        public Type ResultType => typeof(object);
        public bool Matches(ArtifactAdapterContext context) => MatchesFunc(context);
        public object Read(ArtifactAdapterContext context) => ReadFunc(context);
    }
}
