using System;

namespace Tesearis.ArtifactInspectorForUnity.Core.Adapters
{
    /// <summary>
    /// Base class for IArtifactAdapter implementations.
    /// </summary>
    public abstract class ArtifactAdapter<T> : IArtifactAdapter
    {
        /// <summary>
        /// The Unity class name this adapter matches by default. Leave unoverridden (null) and override
        /// <see cref="Matches"/> instead for anything beyond a straight type-name check; an adapter that
        /// overrides neither never matches anything.
        /// </summary>
        protected virtual string ClassName => null;

        public virtual bool Matches(ArtifactAdapterContext context)
        {
            return ClassName != null && string.Equals(context.Reader.TypeName, ClassName, StringComparison.Ordinal);
        }

        public abstract T Read(ArtifactAdapterContext context);

        Type IArtifactAdapter.ResultType => typeof(T);

        object IArtifactAdapter.Read(ArtifactAdapterContext context) => Read(context);
    }
}