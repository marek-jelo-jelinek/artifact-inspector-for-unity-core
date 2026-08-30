using Tesearis.ArtifactInspectorForUnity.Core.Adapters;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Adapters
{
    [TestFixture]
    public class RawObjectTests
    {
        [Test]
        public void Constructor_ExposesObjectRefAndClassName()
        {
            var objectRef = default(ObjectRef);

            var raw = new RawObject(objectRef, "Texture2D");

            Assert.That(raw.ObjectRef, Is.EqualTo(objectRef));
            Assert.That(raw.ClassName, Is.EqualTo("Texture2D"));
        }
    }
}
