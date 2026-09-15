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

        [Test]
        public void Equals_TypedAndObject_IdenticalValues_ReturnsTrue()
        {
            var pptr1 = new PPtr(2, 100L);
            var pptr2 = new PPtr(2, 100L);

            Assert.That(pptr1.Equals(pptr2), Is.True);
            Assert.That(pptr1.Equals((object)pptr2), Is.True);
        }

        [Test]
        public void Equals_DifferentFileId_ReturnsFalse()
        {
            var pptr1 = new PPtr(1, 100L);
            var pptr2 = new PPtr(2, 100L);

            Assert.That(pptr1.Equals(pptr2), Is.False);
            Assert.That(pptr1.Equals((object)pptr2), Is.False);
        }

        [Test]
        public void Equals_DifferentPathId_ReturnsFalse()
        {
            var pptr1 = new PPtr(1, 100L);
            var pptr2 = new PPtr(1, 200L);

            Assert.That(pptr1.Equals(pptr2), Is.False);
            Assert.That(pptr1.Equals((object)pptr2), Is.False);
        }

        [Test]
        public void Equals_ObjectOverload_NullOrDifferentType_ReturnsFalse()
        {
            var pptr = new PPtr(1, 100L);

            Assert.That(pptr.Equals(null), Is.False);
            Assert.That(pptr.Equals("not a pptr"), Is.False);
            Assert.That(pptr.Equals(100L), Is.False);
        }

        [Test]
        public void Operators_EqualAndNotEqual_WorkAsExpected()
        {
            var pptr1 = new PPtr(1, 42L);
            var pptr2 = new PPtr(1, 42L);
            var pptr3 = new PPtr(2, 42L);

            Assert.That(pptr1 == pptr2, Is.True);
            Assert.That(pptr1 != pptr2, Is.False);

            Assert.That(pptr1 == pptr3, Is.False);
            Assert.That(pptr1 != pptr3, Is.True);
        }

        [Test]
        public void GetHashCode_EqualInstances_ProduceIdenticalHashCode()
        {
            var pptr1 = new PPtr(5, 9999L);
            var pptr2 = new PPtr(5, 9999L);

            Assert.That(pptr1.GetHashCode(), Is.EqualTo(pptr2.GetHashCode()));
        }

        [Test]
        public void HashSetAndDictionary_UseValueEquality()
        {
            var pptr1 = new PPtr(1, 10L);
            var pptr2 = new PPtr(1, 10L);
            var pptr3 = new PPtr(2, 10L);

            var set = new System.Collections.Generic.HashSet<PPtr> { pptr1 };
            Assert.That(set.Contains(pptr2), Is.True);
            Assert.That(set.Add(pptr2), Is.False);
            Assert.That(set.Add(pptr3), Is.True);

            var dict = new System.Collections.Generic.Dictionary<PPtr, string>
            {
                [pptr1] = "Value1"
            };
            Assert.That(dict[pptr2], Is.EqualTo("Value1"));
        }

        [Test]
        public void ToString_ReturnsExpectedFormat()
        {
            var pptr = new PPtr(3, 456L);

            Assert.That(pptr.ToString(), Is.EqualTo("PPtr(FileId: 3, PathId: 456)"));
        }
    }
}
