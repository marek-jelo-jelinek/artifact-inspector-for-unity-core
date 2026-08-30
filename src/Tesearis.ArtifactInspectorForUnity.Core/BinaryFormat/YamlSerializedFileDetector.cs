using System.Text;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat
{
    /// <summary>
    /// Sniffs whether a file is a YAML-format Unity SerializedFile.
    /// </summary>
    public static class YamlSerializedFileDetector
    {
        private const string MagicString = "%YAML 1.1";
        private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

        public static bool IsYamlSerializedFile(IRandomAccessByteSource source)
        {
            if (source == null) return false;

            try
            {
                if (source.Length < MagicString.Length) return false;

                var bufferSize = Utf8Bom.Length + MagicString.Length;
                var buffer = new byte[bufferSize];
                var bytesRead = source.Read(0, buffer, 0, bufferSize);
                if (bytesRead < MagicString.Length) return false;

                var offset = 0;
                if (bytesRead >= Utf8Bom.Length && HasUtf8Bom(buffer))
                {
                    offset = Utf8Bom.Length;
                }

                if (bytesRead - offset < MagicString.Length) return false;

                var candidate = Encoding.ASCII.GetString(buffer, offset, MagicString.Length);
                return candidate == MagicString;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsYamlSerializedFile(string filePath)
        {
            if (filePath == null) return false;

            try
            {
                using var source = new FileStreamByteSource(filePath);
                return IsYamlSerializedFile(source);
            }
            catch
            {
                return false;
            }
        }

        private static bool HasUtf8Bom(byte[] buffer)
        {
            for (var i = 0; i < Utf8Bom.Length; i++)
            {
                if (buffer[i] != Utf8Bom[i]) return false;
            }

            return true;
        }
    }
}
