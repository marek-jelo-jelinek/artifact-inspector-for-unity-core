using System;
using System.IO;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport
{
    /// <summary>Creates a temp file with the given content, runs <paramref name="body"/> against its path, then deletes it.</summary>
    internal static class TempFile
    {
        internal static void WithContent(byte[] bytes, Action<string> body)
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(path, bytes);
                body(path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        internal static void WithContent(string text, Action<string> body)
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, text);
                body(path);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
