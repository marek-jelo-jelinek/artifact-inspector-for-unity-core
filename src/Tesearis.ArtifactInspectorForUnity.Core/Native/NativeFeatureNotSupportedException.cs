namespace Tesearis.ArtifactInspectorForUnity.Core.Native
{
    /// <summary>
    /// Thrown instead of a <see cref="NativeCallException"/> when a call requires a native
    /// UnityFileSystemApi entry point that isn't present at all in the currently loaded native
    /// library.
    /// </summary>
    public sealed class NativeFeatureNotSupportedException : ArtifactInspectorException
    {
        public string SymbolName { get; }

        private NativeFeatureNotSupportedException(string message, string symbolName) : base(message)
        {
            SymbolName = symbolName;
        }

        internal static NativeFeatureNotSupportedException ForSymbol(string symbolName)
        {
            return new NativeFeatureNotSupportedException(
                "Native symbol '" + symbolName + "' is not present in the loaded UnityFileSystemApi library " +
                "(this entry point requires a newer native library, Unity 6.5+).",
                symbolName);
        }
    }
}
