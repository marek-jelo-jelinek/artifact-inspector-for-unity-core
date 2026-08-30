using System;

namespace Tesearis.ArtifactInspectorForUnity.Core
{
    /// <summary>
    /// Base type for exceptions raised by Tesearis.ArtifactInspectorForUnity.Core itself.
    /// </summary>
    public class ArtifactInspectorException : Exception
    {
        public ArtifactInspectorException()
        {
        }

        public ArtifactInspectorException(string message) : base(message)
        {
        }

        public ArtifactInspectorException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
