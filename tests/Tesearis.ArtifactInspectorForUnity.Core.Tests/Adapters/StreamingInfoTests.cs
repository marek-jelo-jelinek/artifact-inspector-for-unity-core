using Tesearis.ArtifactInspectorForUnity.Core.Adapters;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Adapters
{
    [TestFixture]
    public class StreamingInfoTests
    {
        [Test]
        public void Constructor_ExposesOffsetSizeAndPath()
        {
            var info = new StreamingInfo(1234UL, 5678U, "archive:/CAB-abc.resS");

            Assert.That(info.Offset, Is.EqualTo(1234UL));
            Assert.That(info.Size, Is.EqualTo(5678U));
            Assert.That(info.Path, Is.EqualTo("archive:/CAB-abc.resS"));
        }
    }
}
