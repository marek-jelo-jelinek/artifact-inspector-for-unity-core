namespace Tesearis.ArtifactInspectorForUnity.Core.Model
{
    /// <summary>
    /// Thrown by <see cref="ArtifactArchive.OpenSerializedFile"/> and <see cref="ArtifactInspector.OpenSerializedFile"/>
    /// in place of a <see cref="Native.NativeCallException"/> specifically when the native open failure is positively confirmed (via
    /// <see cref="BinaryFormat.SerializedFileDetector.IsMissingTypeTrees(TypeTree.IRandomAccessByteSource)"/>)
    /// to be caused by the entry lacking TypeTrees.
    /// </summary>
    public sealed class SerializedFileOpenException : ArtifactInspectorException
    {
        public string EntryName { get; }
        public bool MissingTypeTrees { get; }

        public SerializedFileOpenException(string entryName, bool missingTypeTrees)
            : base(missingTypeTrees
                ? "SerializedFile entry has no TypeTrees: '" + entryName + "'."
                : "Failed to open SerializedFile entry: '" + entryName + "'.")
        {
            EntryName = entryName;
            MissingTypeTrees = missingTypeTrees;
        }
    }
}
