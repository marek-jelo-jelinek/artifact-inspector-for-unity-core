using System.Collections.Generic;
using System.Linq;
using Tesearis.ArtifactInspectorForUnity.Core.Adapters;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Handles;
using Tesearis.ArtifactInspectorForUnity.Core.Tests.TestSupport;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;
using NUnit.Framework;

namespace Tesearis.ArtifactInspectorForUnity.Core.Tests.Adapters
{
    [TestFixture]
    public class ArtifactAdapterRegistryTests
    {
        [Test]
        public void Register_ReturnsSameInstance_ForFluentChaining()
        {
            var registry = new ArtifactAdapterRegistry();
            var a = new StubArtifactAdapter();
            var b = new StubArtifactAdapter();

            var result = registry.Register(a).Register(b);

            Assert.That(result, Is.SameAs(registry));
        }

        [Test]
        public void TryResolve_MultipleMatchingAdapters_FirstRegisteredWins()
        {
            var first = new StubArtifactAdapter { MatchesFunc = _ => true };
            var second = new StubArtifactAdapter { MatchesFunc = _ => true };
            var registry = new ArtifactAdapterRegistry().Register(first).Register(second);

            var resolved = registry.TryResolve(BuildContext("AnyType"), out var adapter);

            Assert.That(resolved, Is.True);
            Assert.That(adapter, Is.SameAs(first));
        }

        [Test]
        public void TryResolve_SkipsNonMatchingBeforeMatching_ReturnsFirstMatchingNotFirstElement()
        {
            var nonMatching = new StubArtifactAdapter { MatchesFunc = _ => false };
            var matching = new StubArtifactAdapter { MatchesFunc = _ => true };
            var registry = new ArtifactAdapterRegistry().Register(nonMatching).Register(matching);

            var resolved = registry.TryResolve(BuildContext("AnyType"), out var adapter);

            Assert.That(resolved, Is.True);
            Assert.That(adapter, Is.SameAs(matching));
        }

        [Test]
        public void TryResolve_NoAdapterMatches_ReturnsFalse()
        {
            var registry = new ArtifactAdapterRegistry().Register(new StubArtifactAdapter { MatchesFunc = _ => false });

            var resolved = registry.TryResolve(BuildContext("AnyType"), out var adapter);

            Assert.That(resolved, Is.False);
            Assert.That(adapter, Is.Null);
        }

        [Test]
        public void TryResolve_EmptyRegistry_ReturnsFalse()
        {
            var registry = new ArtifactAdapterRegistry();

            var resolved = registry.TryResolve(BuildContext("AnyType"), out var adapter);

            Assert.That(resolved, Is.False);
        }

        private static ArtifactAdapterContext BuildContext(string typeName)
        {
            var node = FakeTypeTreeBuilder.Struct("Base", typeName, new List<TypeTreeNode>().ToArray());
            var reader = new TypeTreeReader(node, new InMemoryByteSource([]), 0);
            return new ArtifactAdapterContext(default(ObjectRef), reader, null, null);
        }

        [Test]
        public void Adapt_MatchingAdapterRegistered_ReturnsItsReadResult()
        {
            var (serializedFile, archive, objectRef) = CreateSerializedFileWithOneObject();
            var expected = new object();
            var adapter = new StubArtifactAdapter { MatchesFunc = _ => true, ReadFunc = _ => expected };
            var registry = new ArtifactAdapterRegistry().Register(adapter);

            var result = registry.Adapt(objectRef, serializedFile, archive);

            Assert.That(result, Is.SameAs(expected));
        }

        [Test]
        public void Adapt_NoAdapterMatches_ReturnsRawObjectFallback()
        {
            var (serializedFile, archive, objectRef) = CreateSerializedFileWithOneObject();
            var registry = new ArtifactAdapterRegistry().Register(new StubArtifactAdapter { MatchesFunc = _ => false });

            var result = registry.Adapt(objectRef, serializedFile, archive);

            Assert.That(result, Is.InstanceOf<RawObject>());
            var raw = (RawObject)result;
            Assert.That(raw.ObjectRef.PathId, Is.EqualTo(objectRef.PathId));
            Assert.That(raw.ClassName, Is.EqualTo(objectRef.ClassName));
        }

        [Test]
        public void Adapt_EmptyRegistry_ReturnsRawObjectFallback()
        {
            var (serializedFile, archive, objectRef) = CreateSerializedFileWithOneObject();
            var registry = new ArtifactAdapterRegistry();

            var result = registry.Adapt(objectRef, serializedFile, archive);

            Assert.That(result, Is.InstanceOf<RawObject>());
        }

        [Test]
        public void Inspect_SerializedFile_DispatchesEveryObjectThroughTheRegistry()
        {
            var (serializedFile, archive, _) = CreateSerializedFileWithOneObject();
            var registry = new ArtifactAdapterRegistry();

            var results = registry.Inspect(serializedFile, archive).ToList();

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0], Is.InstanceOf<RawObject>());
        }

        private static (SerializedFile SerializedFile, ArtifactArchive Archive, ObjectRef ObjectRef) CreateSerializedFileWithOneObject()
        {
            var api = new FakeUnityFileSystemApi();
            api.Objects.Add(new ObjectInfo { Id = 1, Offset = 0, Size = 4, TypeId = 1 });

            var archiveHandle = new ArchiveHandle(api, api.NextHandle());
            var archive = new ArtifactArchive(api, archiveHandle, ArchiveMountPoint.NewMountPoint());

            var serializedFileHandle = new SerializedFileHandle(api, api.NextHandle());
            var fileHandle = new FileHandle(api, api.NextHandle());
            var serializedFile = new SerializedFile(serializedFileHandle, fileHandle, new TypeTreeCache());

            serializedFile.TryGetObject(1, out var objectRef);
            return (serializedFile, archive, objectRef);
        }
    }
}
