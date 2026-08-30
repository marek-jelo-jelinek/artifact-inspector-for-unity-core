using Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.BinaryFormat
{
    [TestFixture]
    public class StrippedObjectInfoTests
    {
        [Test]
        public void Constructor_ExposesFields()
        {
            var info = new StrippedObjectInfo(pathId: 1001, typeId: 1, byteOffset: 512, byteSize: 64);

            Assert.That(info.PathId, Is.EqualTo(1001L));
            Assert.That(info.TypeId, Is.EqualTo(1));
            Assert.That(info.ByteOffset, Is.EqualTo(512L));
            Assert.That(info.ByteSize, Is.EqualTo(64L));
        }

        [Test]
        public void ClassName_ResolvesKnownTypeIdViaTypeIdRegistry()
        {
            var info = new StrippedObjectInfo(pathId: 1, typeId: 1, byteOffset: 0, byteSize: 0);

            Assert.That(info.ClassName, Is.EqualTo("GameObject"));
        }

        [Test]
        public void ClassName_UnknownTypeId_FallsBackToNumericString()
        {
            var info = new StrippedObjectInfo(pathId: 1, typeId: -12345, byteOffset: 0, byteSize: 0);

            Assert.That(info.ClassName, Is.EqualTo("-12345"));
        }
    }
}
