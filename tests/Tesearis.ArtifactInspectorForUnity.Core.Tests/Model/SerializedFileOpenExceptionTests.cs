using Tesearis.ArtifactInspectorForUnity.Core.Model;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Model
{
    [TestFixture]
    public class SerializedFileOpenExceptionTests
    {
        [Test]
        public void Constructor_MissingTypeTreesTrue_SetsPropertiesAndDistinctMessage()
        {
            var ex = new SerializedFileOpenException("level0", missingTypeTrees: true);

            Assert.That(ex.EntryName, Is.EqualTo("level0"));
            Assert.That(ex.MissingTypeTrees, Is.True);
            Assert.That(ex.Message, Does.Contain("no TypeTrees"));
            Assert.That(ex.Message, Does.Contain("level0"));
        }

        [Test]
        public void Constructor_MissingTypeTreesFalse_SetsPropertiesAndDistinctMessage()
        {
            var ex = new SerializedFileOpenException("level0", missingTypeTrees: false);

            Assert.That(ex.EntryName, Is.EqualTo("level0"));
            Assert.That(ex.MissingTypeTrees, Is.False);
            Assert.That(ex.Message, Does.Not.Contain("no TypeTrees"));
            Assert.That(ex.Message, Does.Contain("level0"));
        }

        [Test]
        public void SerializedFileOpenException_IsAnArtifactInspectorException()
        {
            var ex = new SerializedFileOpenException("level0", missingTypeTrees: true);

            Assert.That(ex, Is.InstanceOf<ArtifactInspectorException>());
        }
    }
}
