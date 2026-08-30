using Tesearis.ArtifactInspectorForUnity.Core.Model;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Model
{
    [TestFixture]
    public class PPtrTests
    {
        [Test]
        public void Constructor_RoundTripsFileIdAndPathId()
        {
            var pptr = new PPtr(3, 12345L);

            Assert.That(pptr.FileId, Is.EqualTo(3));
            Assert.That(pptr.PathId, Is.EqualTo(12345L));
        }

        [Test]
        public void IsNull_BothFieldsZero_ReturnsTrue()
        {
            var pptr = new PPtr(0, 0L);

            Assert.That(pptr.IsNull, Is.True);
        }

        [Test]
        public void IsNull_NonZeroFileId_ReturnsFalse()
        {
            var pptr = new PPtr(1, 0L);

            Assert.That(pptr.IsNull, Is.False);
        }

        [Test]
        public void IsNull_NonZeroPathId_ReturnsFalse()
        {
            var pptr = new PPtr(0, 42L);

            Assert.That(pptr.IsNull, Is.False);
        }

        [Test]
        public void IsLocal_ZeroFileId_ReturnsTrueRegardlessOfPathId()
        {
            Assert.That(new PPtr(0, 0L).IsLocal, Is.True);
            Assert.That(new PPtr(0, 42L).IsLocal, Is.True);
        }

        [Test]
        public void IsLocal_NonZeroFileId_ReturnsFalse()
        {
            var pptr = new PPtr(1, 42L);

            Assert.That(pptr.IsLocal, Is.False);
        }
    }
}
