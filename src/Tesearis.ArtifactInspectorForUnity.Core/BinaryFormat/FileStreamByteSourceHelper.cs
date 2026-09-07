using System;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat
{
    /// <summary>
    /// Shared "open a bare file path as a FileStreamByteSource, delegate, dispose" plumbing behind the
    /// string-path convenience overloads of <see cref="SerializedFileDetector"/> and
    /// <see cref="YamlSerializedFileDetector"/>. Never swallows: a bad path throws normally (e.g.
    /// <see cref="System.IO.FileNotFoundException"/>), same as opening any other file. Each detector's
    /// own <see cref="IRandomAccessByteSource"/>-based overload still decides for itself which
    /// format-level failures to report as false instead of throwing.
    /// </summary>
    internal static class FileStreamByteSourceHelper
    {
        internal static T WithFileStreamByteSource<T>(string filePath, Func<IRandomAccessByteSource, T> detect)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));

            using var source = new FileStreamByteSource(filePath);
            return detect(source);
        }
    }
}
