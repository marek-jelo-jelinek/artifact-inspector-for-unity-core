using System;

namespace Tesearis.ArtifactInspectorForUnity.Core.Adapters
{
    /// <summary>
    /// Base class for IArtifactAdapter implementations.
    /// </summary>
    public abstract class ArtifactAdapter<T> : IArtifactAdapter
    {
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