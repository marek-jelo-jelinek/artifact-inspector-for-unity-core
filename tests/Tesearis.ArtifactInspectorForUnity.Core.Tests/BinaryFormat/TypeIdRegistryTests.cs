using Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.BinaryFormat
{
    [TestFixture]
    public class TypeIdRegistryTests
    {
        [Test]
        public void GetTypeName_KnownIds_ReturnsClassName()
        {
            Assert.That(TypeIdRegistry.GetTypeName(1), Is.EqualTo("GameObject"));
            Assert.That(TypeIdRegistry.GetTypeName(28), Is.EqualTo("Texture2D"));
            Assert.That(TypeIdRegistry.GetTypeName(114), Is.EqualTo("MonoBehaviour"));
        }

        [Test]
        public void GetTypeName_UnknownId_FallsBackToNumericString()
        {
            Assert.That(TypeIdRegistry.GetTypeName(-999), Is.EqualTo("-999"));
        }

        [Test]
        public void TryGetTypeName_KnownId_ReturnsTrueWithName()
        {
            var found = TypeIdRegistry.TryGetTypeName(1, out var name);

            Assert.That(found, Is.True);
            Assert.That(name, Is.EqualTo("GameObject"));
        }

        [Test]
        public void TryGetTypeName_UnknownId_ReturnsFalseWithNoFallback()
        {
            var found = TypeIdRegistry.TryGetTypeName(-999, out var name);

            Assert.That(found, Is.False);
            Assert.That(name, Is.Null);
        }
    }
}
