using System.Collections.Generic;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Model
{
    [TestFixture]
    public class ExternalReferenceTests
    {
        [Test]
        public void Equals_TypedAndObject_IdenticalValues_ReturnsTrue()
        {
            var ref1 = new ExternalReference("archive:/cabin.sharedassets", "0123456789abcdef0123456789abcdef", ExternalReferenceType.SerializedAssetType);
            var ref2 = new ExternalReference("archive:/cabin.sharedassets", "0123456789abcdef0123456789abcdef", ExternalReferenceType.SerializedAssetType);

            Assert.That(ref1.Equals(ref2), Is.True);
            Assert.That(ref1.Equals((object)ref2), Is.True);
        }

        [Test]
        public void Equals_DifferentPath_ReturnsFalse()
        {
            var ref1 = new ExternalReference("archive:/cabin1.sharedassets", "0123456789abcdef0123456789abcdef", ExternalReferenceType.SerializedAssetType);
            var ref2 = new ExternalReference("archive:/cabin2.sharedassets", "0123456789abcdef0123456789abcdef", ExternalReferenceType.SerializedAssetType);

            Assert.That(ref1.Equals(ref2), Is.False);
            Assert.That(ref1.Equals((object)ref2), Is.False);
        }

        [Test]
        public void Equals_DifferentGuid_ReturnsFalse()
        {
            var ref1 = new ExternalReference("archive:/cabin.sharedassets", "11111111111111111111111111111111", ExternalReferenceType.SerializedAssetType);
            var ref2 = new ExternalReference("archive:/cabin.sharedassets", "22222222222222222222222222222222", ExternalReferenceType.SerializedAssetType);

            Assert.That(ref1.Equals(ref2), Is.False);
            Assert.That(ref1.Equals((object)ref2), Is.False);
        }

        [Test]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var ref1 = new ExternalReference("archive:/cabin.sharedassets", "0123456789abcdef0123456789abcdef", ExternalReferenceType.SerializedAssetType);
            var ref2 = new ExternalReference("archive:/cabin.sharedassets", "0123456789abcdef0123456789abcdef", ExternalReferenceType.MetaAssetType);

            Assert.That(ref1.Equals(ref2), Is.False);
            Assert.That(ref1.Equals((object)ref2), Is.False);
        }

        [Test]
        public void Equals_ObjectOverload_NullOrDifferentType_ReturnsFalse()
        {
            var reference = new ExternalReference("archive:/cabin.sharedassets", "0123456789abcdef0123456789abcdef", ExternalReferenceType.SerializedAssetType);

            Assert.That(reference.Equals(null), Is.False);
            Assert.That(reference.Equals("not an external reference"), Is.False);
            Assert.That(reference.Equals(42), Is.False);
        }

        [Test]
        public void Equals_WithNullProperties_WorksCorrectly()
        {
            var ref1 = new ExternalReference(null, null, ExternalReferenceType.NonAssetType);
            var ref2 = new ExternalReference(null, null, ExternalReferenceType.NonAssetType);
            var ref3 = new ExternalReference("path", null, ExternalReferenceType.NonAssetType);

            Assert.That(ref1.Equals(ref2), Is.True);
            Assert.That(ref1.Equals(ref3), Is.False);
        }

        [Test]
        public void Operators_EqualAndNotEqual_WorkAsExpected()
        {
            var ref1 = new ExternalReference("archive:/cabin.sharedassets", "guid1", ExternalReferenceType.SerializedAssetType);
            var ref2 = new ExternalReference("archive:/cabin.sharedassets", "guid1", ExternalReferenceType.SerializedAssetType);
            var ref3 = new ExternalReference("archive:/cabin.sharedassets", "guid2", ExternalReferenceType.SerializedAssetType);

            Assert.That(ref1 == ref2, Is.True);
            Assert.That(ref1 != ref2, Is.False);

            Assert.That(ref1 == ref3, Is.False);
            Assert.That(ref1 != ref3, Is.True);
        }

        [Test]
        public void GetHashCode_EqualInstances_ProduceIdenticalHashCode()
        {
            var ref1 = new ExternalReference("resources/unity_builtin_extra", "guid123", ExternalReferenceType.MetaAssetType);
            var ref2 = new ExternalReference("resources/unity_builtin_extra", "guid123", ExternalReferenceType.MetaAssetType);

            Assert.That(ref1.GetHashCode(), Is.EqualTo(ref2.GetHashCode()));
        }

        [Test]
        public void GetHashCode_NullFields_DoesNotThrow()
        {
            var ref1 = default(ExternalReference);
            var ref2 = default(ExternalReference);

            Assert.DoesNotThrow(() => _ = ref1.GetHashCode());
            Assert.That(ref1.GetHashCode(), Is.EqualTo(ref2.GetHashCode()));
            Assert.That(ref1 == ref2, Is.True);
        }

        [Test]
        public void HashSetAndDictionary_UseValueEquality()
        {
            var ref1 = new ExternalReference("path1", "guid1", ExternalReferenceType.SerializedAssetType);
            var ref2 = new ExternalReference("path1", "guid1", ExternalReferenceType.SerializedAssetType);
            var ref3 = new ExternalReference("path2", "guid2", ExternalReferenceType.SerializedAssetType);

            var set = new HashSet<ExternalReference> { ref1 };
            Assert.That(set.Contains(ref2), Is.True);
            Assert.That(set.Add(ref2), Is.False);
            Assert.That(set.Add(ref3), Is.True);

            var dict = new Dictionary<ExternalReference, string>
            {
                [ref1] = "RefEntry"
            };
            Assert.That(dict[ref2], Is.EqualTo("RefEntry"));
        }

        [Test]
        public void ToString_ReturnsExpectedFormat()
        {
            var reference = new ExternalReference("resources/unity default resources", "0000000000000000e000000000000000", ExternalReferenceType.SerializedAssetType);

            Assert.That(reference.ToString(), Is.EqualTo("ExternalReference(Path: 'resources/unity default resources', Guid: 0000000000000000e000000000000000, Type: SerializedAssetType)"));
        }
    }
}
