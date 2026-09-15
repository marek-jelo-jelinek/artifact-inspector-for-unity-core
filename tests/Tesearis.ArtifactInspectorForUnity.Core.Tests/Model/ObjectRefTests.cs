using System.Collections.Generic;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Handles;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Model
{
    [TestFixture]
    public class ObjectRefTests
    {
        private static SerializedFile CreateFile(FakeUnityFileSystemApi api)
        {
            var handle = new SerializedFileHandle(api, api.NextHandle());
            var fileHandle = new FileHandle(api, api.NextHandle());
            return new SerializedFile(handle, fileHandle, new TypeTreeCache());
        }

        [Test]
        public void Equals_TypedAndObject_IdenticalValues_ReturnsTrue()
        {
            var api = new FakeUnityFileSystemApi();
            using var file = CreateFile(api);

            var obj1 = new ObjectRef(file, 12345L, 28, 1024L, 512L);
            var obj2 = new ObjectRef(file, 12345L, 28, 1024L, 512L);

            Assert.That(obj1.Equals(obj2), Is.True);
            Assert.That(obj1.Equals((object)obj2), Is.True);
        }

        [Test]
        public void Equals_DifferentOwner_ReturnsFalse()
        {
            var api = new FakeUnityFileSystemApi();
            using var file1 = CreateFile(api);
            using var file2 = CreateFile(api);

            var obj1 = new ObjectRef(file1, 100L, 1, 10L, 20L);
            var obj2 = new ObjectRef(file2, 100L, 1, 10L, 20L);

            Assert.That(obj1.Equals(obj2), Is.False);
            Assert.That(obj1.Equals((object)obj2), Is.False);
        }

        [Test]
        public void Equals_DifferentPathId_ReturnsFalse()
        {
            var api = new FakeUnityFileSystemApi();
            using var file = CreateFile(api);

            var obj1 = new ObjectRef(file, 100L, 1, 10L, 20L);
            var obj2 = new ObjectRef(file, 200L, 1, 10L, 20L);

            Assert.That(obj1.Equals(obj2), Is.False);
        }

        [Test]
        public void Equals_DifferentTypeId_ReturnsFalse()
        {
            var api = new FakeUnityFileSystemApi();
            using var file = CreateFile(api);

            var obj1 = new ObjectRef(file, 100L, 1, 10L, 20L);
            var obj2 = new ObjectRef(file, 100L, 2, 10L, 20L);

            Assert.That(obj1.Equals(obj2), Is.False);
        }

        [Test]
        public void Equals_DifferentByteOffset_ReturnsFalse()
        {
            var api = new FakeUnityFileSystemApi();
            using var file = CreateFile(api);

            var obj1 = new ObjectRef(file, 100L, 1, 10L, 20L);
            var obj2 = new ObjectRef(file, 100L, 1, 999L, 20L);

            Assert.That(obj1.Equals(obj2), Is.False);
        }

        [Test]
        public void Equals_DifferentByteSize_ReturnsFalse()
        {
            var api = new FakeUnityFileSystemApi();
            using var file = CreateFile(api);

            var obj1 = new ObjectRef(file, 100L, 1, 10L, 20L);
            var obj2 = new ObjectRef(file, 100L, 1, 10L, 999L);

            Assert.That(obj1.Equals(obj2), Is.False);
        }

        [Test]
        public void Equals_ObjectOverload_NullOrDifferentType_ReturnsFalse()
        {
            var api = new FakeUnityFileSystemApi();
            using var file = CreateFile(api);
            var obj = new ObjectRef(file, 100L, 1, 10L, 20L);

            Assert.That(obj.Equals(null), Is.False);
            Assert.That(obj.Equals("not an objectref"), Is.False);
            Assert.That(obj.Equals(100L), Is.False);
        }

        [Test]
        public void Operators_EqualAndNotEqual_WorkAsExpected()
        {
            var api = new FakeUnityFileSystemApi();
            using var file = CreateFile(api);

            var obj1 = new ObjectRef(file, 100L, 1, 10L, 20L);
            var obj2 = new ObjectRef(file, 100L, 1, 10L, 20L);
            var obj3 = new ObjectRef(file, 200L, 1, 10L, 20L);

            Assert.That(obj1 == obj2, Is.True);
            Assert.That(obj1 != obj2, Is.False);

            Assert.That(obj1 == obj3, Is.False);
            Assert.That(obj1 != obj3, Is.True);
        }

        [Test]
        public void GetHashCode_EqualInstances_ProduceIdenticalHashCode()
        {
            var api = new FakeUnityFileSystemApi();
            using var file = CreateFile(api);

            var obj1 = new ObjectRef(file, 100L, 1, 10L, 20L);
            var obj2 = new ObjectRef(file, 100L, 1, 10L, 20L);

            Assert.That(obj1.GetHashCode(), Is.EqualTo(obj2.GetHashCode()));
        }

        [Test]
        public void GetHashCode_NullOwner_DoesNotThrow()
        {
            var obj1 = default(ObjectRef);
            var obj2 = default(ObjectRef);

            Assert.DoesNotThrow(() => _ = obj1.GetHashCode());
            Assert.That(obj1.GetHashCode(), Is.EqualTo(obj2.GetHashCode()));
            Assert.That(obj1 == obj2, Is.True);
        }

        [Test]
        public void HashSetAndDictionary_UseValueEquality()
        {
            var api = new FakeUnityFileSystemApi();
            using var file = CreateFile(api);

            var obj1 = new ObjectRef(file, 100L, 1, 10L, 20L);
            var obj2 = new ObjectRef(file, 100L, 1, 10L, 20L);
            var obj3 = new ObjectRef(file, 200L, 1, 10L, 20L);

            var set = new HashSet<ObjectRef> { obj1 };
            Assert.That(set.Contains(obj2), Is.True);
            Assert.That(set.Add(obj2), Is.False);
            Assert.That(set.Add(obj3), Is.True);

            var dict = new Dictionary<ObjectRef, string>
            {
                [obj1] = "Entity1"
            };
            Assert.That(dict[obj2], Is.EqualTo("Entity1"));
        }

        [Test]
        public void ToString_ReturnsExpectedFormat()
        {
            var api = new FakeUnityFileSystemApi();
            using var file = CreateFile(api);

            var obj = new ObjectRef(file, 100L, 28, 1024L, 512L);

            Assert.That(obj.ToString(), Is.EqualTo("ObjectRef(PathId: 100, TypeId: 28, ByteOffset: 1024, ByteSize: 512)"));
        }

        [Test]
        public void ClassName_DefaultObjectRef_ReturnsEmpty()
        {
            var def = default(ObjectRef);
            Assert.That(def.ClassName, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ClassName_ValidObject_ReturnsTypeName()
        {
            var api = new FakeUnityFileSystemApi();
            using var file = CreateFile(api);

            var obj = new ObjectRef(file, 100L, 1, 10L, 20L);
            Assert.That(obj.ClassName, Is.EqualTo("int"));
        }
    }
}
