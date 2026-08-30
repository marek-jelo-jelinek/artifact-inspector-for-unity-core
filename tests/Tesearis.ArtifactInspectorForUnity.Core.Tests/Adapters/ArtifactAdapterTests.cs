using System;
using Tesearis.ArtifactInspectorForUnity.Core.Adapters;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Adapters
{
    [TestFixture]
    public class ArtifactAdapterTests
    {
        [Test]
        public void Matches_ReaderTypeNameEqualsClassName_ReturnsTrue()
        {
            var adapter = new ClassNameAdapter();
            var context = BuildContext("SomeType");

            Assert.That(adapter.Matches(context), Is.True);
        }

        [Test]
        public void Matches_ReaderTypeNameDiffersFromClassName_ReturnsFalse()
        {
            var adapter = new ClassNameAdapter();
            var context = BuildContext("OtherType");

            Assert.That(adapter.Matches(context), Is.False);
        }

        [Test]
        public void Matches_ClassNameNotOverridden_AlwaysReturnsFalse()
        {
            // The default ClassName is null, opting a subclass out of the default name-based Matches --
            // such a subclass is expected to override Matches directly instead.
            var adapter = new OptOutAdapter();
            var context = BuildContext("AnyType");

            Assert.That(adapter.Matches(context), Is.False);
        }

        [Test]
        public void ResultType_ReflectsTheGenericTypeParameter()
        {
            IArtifactAdapter adapter = new ClassNameAdapter();

            Assert.That(adapter.ResultType, Is.EqualTo(typeof(string)));
        }

        [Test]
        public void Read_StructResult_IsBoxedThroughTheInterfacesObjectReturningRead()
        {
            IArtifactAdapter adapter = new StructAdapter();
            var context = BuildContext("Anything");

            var result = adapter.Read(context);

            Assert.That(result, Is.InstanceOf<StructResult>());
            Assert.That(((StructResult)result).Value, Is.EqualTo(42));
        }

        private static ArtifactAdapterContext BuildContext(string typeName)
        {
            var node = FakeTypeTreeBuilder.Leaf("Base", typeName, 0);
            var reader = new TypeTreeReader(node, new InMemoryByteSource(Array.Empty<byte>()), 0);
            return new ArtifactAdapterContext(default(ObjectRef), reader, null, null);
        }

        private sealed class ClassNameAdapter : ArtifactAdapter<string>
        {
            protected override string ClassName => "SomeType";
            public override string Read(ArtifactAdapterContext context) => "read result";
        }

        private sealed class OptOutAdapter : ArtifactAdapter<string>
        {
            public override string Read(ArtifactAdapterContext context) => "read result";
        }

        private struct StructResult
        {
            public int Value;
        }

        private sealed class StructAdapter : ArtifactAdapter<StructResult>
        {
            public override StructResult Read(ArtifactAdapterContext context) => new StructResult { Value = 42 };
        }
    }
}
