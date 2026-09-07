using System;
using System.IO;
using System.Text;
using Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.BinaryFormat
{
    [TestFixture]
    public class YamlSerializedFileDetectorTests
    {
        [Test]
        public void IsYamlSerializedFile_MagicWithoutBom_ReturnsTrue()
        {
            var buffer = Encoding.ASCII.GetBytes("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");

            Assert.That(YamlSerializedFileDetector.IsYamlSerializedFile(new InMemoryByteSource(buffer)), Is.True);
        }

        [Test]
        public void IsYamlSerializedFile_MagicWithBom_ReturnsTrue()
        {
            var bom = new byte[] { 0xEF, 0xBB, 0xBF };
            var magic = Encoding.ASCII.GetBytes("%YAML 1.1\n");
            var buffer = new byte[bom.Length + magic.Length];
            bom.CopyTo(buffer, 0);
            magic.CopyTo(buffer, bom.Length);

            Assert.That(YamlSerializedFileDetector.IsYamlSerializedFile(new InMemoryByteSource(buffer)), Is.True);
        }

        [Test]
        public void IsYamlSerializedFile_BinaryHeader_ReturnsFalse()
        {
            var buffer = new ByteBufferWriter().WriteUInt32BigEndian(0).WriteUInt32BigEndian(0)
                .WriteUInt32BigEndian(21).WriteUInt32BigEndian(0).WriteByte(0).WriteZeros(3).ToArray();

            Assert.That(YamlSerializedFileDetector.IsYamlSerializedFile(new InMemoryByteSource(buffer)), Is.False);
        }

        [Test]
        public void IsYamlSerializedFile_TooShortBuffer_ReturnsFalse()
        {
            var buffer = new byte[] { (byte)'%', (byte)'Y' };

            Assert.That(YamlSerializedFileDetector.IsYamlSerializedFile(new InMemoryByteSource(buffer)), Is.False);
        }

        [Test]
        public void IsYamlSerializedFile_FilePathOverload_MatchesByteSourceOverload()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "%YAML 1.1\n");

                Assert.That(YamlSerializedFileDetector.IsYamlSerializedFile(path), Is.True);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void IsYamlSerializedFile_NullFilePath_ThrowsArgumentNullException()
        {
            // A bad path is never swallowed by the string-path convenience overload, only
            // format-level detection failures are.
            Assert.Throws<ArgumentNullException>(() => YamlSerializedFileDetector.IsYamlSerializedFile((string)null));
        }

        [Test]
        public void IsYamlSerializedFile_FilePathDoesNotExist_ThrowsInsteadOfReturningFalse()
        {
            var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".missing");

            Assert.Throws<FileNotFoundException>(() => YamlSerializedFileDetector.IsYamlSerializedFile(missingPath));
        }
    }
}
