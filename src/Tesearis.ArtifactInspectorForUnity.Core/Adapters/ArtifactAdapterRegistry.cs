using System;
using System.Collections.Generic;
using Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat;
using Tesearis.ArtifactInspectorForUnity.Core.Model;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.Adapters
{
    /// <summary>An ordered set of IArtifactAdapters. Dispatch uses the first match in registration order.</summary>
    public sealed class ArtifactAdapterRegistry
    {
        private readonly List<IArtifactAdapter> _adapters = new();

        /// <summary>Registers adapter, returning this instance for chaining.</summary>
        public ArtifactAdapterRegistry Register(IArtifactAdapter adapter)
        {
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));
            _adapters.Add(adapter);
            return this;
        }

        internal bool TryResolve(ArtifactAdapterContext context, out IArtifactAdapter adapter)
        {
            foreach (var candidate in _adapters)
            {
                if (!candidate.Matches(context)) continue;
                adapter = candidate;
                return true;
            }

            adapter = null;
            return false;
        }

        /// <summary>Dispatches a single object through the registry, falling back to a RawObject for anything unrecognized.</summary>
        public object Adapt(ObjectRef objectRef, SerializedFile serializedFile, ArtifactArchive archive = null)
        {
            if (serializedFile == null) throw new ArgumentNullException(nameof(serializedFile));

            TypeTreeReader reader;
            try
            {
                reader = objectRef.GetReader();
                var context = new ArtifactAdapterContext(objectRef, reader, serializedFile, archive);
                return TryResolve(context, out var adapter) ? adapter.Read(context) : new RawObject(objectRef, reader.TypeName);
            }
            catch (UnsupportedManagedReferenceShapeException)
            {
                // Known, documented limitation (see README's "Known limitations"): [SerializeReference]
                // polymorphic fields aren't decoded, so this object's offsets can't be computed and no
                // adapter can read it -- fall back to a RawObject like any other unrecognized object,
                // same as the numeric-ClassID fallback StrippedObjectInfo.ClassName uses for stripped
                // files with no TypeTree at all.
                return new RawObject(objectRef, TypeIdRegistry.GetTypeName(objectRef.TypeId));
            }
        }

        /// <summary>Dispatches every object in serializedFile, in declaration order or ordered by ByteOffset.</summary>
        public IEnumerable<object> Inspect(SerializedFile serializedFile, bool orderByOffset = false)
        {
            if (serializedFile == null) throw new ArgumentNullException(nameof(serializedFile));

            return InspectObjects(serializedFile, archive: null, orderByOffset);
        }

        /// <summary>Dispatches every object in serializedFile, in declaration order or ordered by ByteOffset.</summary>
        public IEnumerable<object> Inspect(SerializedFile serializedFile, ArtifactArchive archive, bool orderByOffset = false)
        {
            if (serializedFile == null) throw new ArgumentNullException(nameof(serializedFile));
            if (archive == null) throw new ArgumentNullException(nameof(archive));

            return InspectObjects(serializedFile, archive, orderByOffset);
        }

        private IEnumerable<object> InspectObjects(SerializedFile serializedFile, ArtifactArchive archive, bool orderByOffset)
        {
            var objects = serializedFile.Objects;
            if (orderByOffset)
            {
                var objectsSorted = new List<ObjectRef>(objects);
                objectsSorted.Sort((a, b) => a.ByteOffset.CompareTo(b.ByteOffset));
                objects = objectsSorted;
            }

            foreach (var objectRef in objects)
            {
                yield return Adapt(objectRef, serializedFile, archive);
            }
        }

        /// <summary>Dispatches every object in every SerializedFile entry of archive.</summary>
        public IEnumerable<object> Inspect(ArtifactArchive archive, bool orderByOffset = false)
        {
            if (archive == null) throw new ArgumentNullException(nameof(archive));

            return InspectEntries(archive, orderByOffset);
        }

        private IEnumerable<object> InspectEntries(ArtifactArchive archive, bool orderByOffset)
        {
            foreach (var entryName in archive.EntryNames)
            {
                using var serializedFile = archive.OpenSerializedFile(entryName);
                foreach (var obj in Inspect(serializedFile, archive, orderByOffset))
                {
                    yield return obj;
                }
            }
        }
    }
}
