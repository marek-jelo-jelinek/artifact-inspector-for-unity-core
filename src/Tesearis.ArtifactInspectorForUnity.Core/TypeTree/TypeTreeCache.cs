using System;
using System.Collections.Generic;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Handles;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Interop;

namespace Tesearis.ArtifactInspectorForUnity.Core.TypeTree
{
    /// <summary>
    /// Builds a TypeTreeNode tree once per distinct object id or type-tree index and caches it,
    /// scoped to one SerializedFile. Object ids and type-tree indices are separate identifier
    /// spaces (an object id and a small index could numerically coincide), so they're cached in
    /// separate dictionaries rather than sharing one keyed by a single native handle.
    /// </summary>
    internal sealed class TypeTreeCache
    {
        private const int MaxRecursionDepth = 64;

        // Unity's on-disk ClassID for MonoBehaviour. A MonoBehaviour's type tree embeds that
        // specific script's own serialized public fields, so two MonoBehaviour instances sharing
        // this ClassID can have genuinely different schemas -- unlike every other ClassID, whose
        // schema is fixed per file. This is the only confirmed per-instance-schema ClassID
        // (checked TypeIdRegistry and the [SerializeReference]/RefType handling in SnapshotBuilder/
        // TypeTreeOffsetWalker: those vary individual field shapes, not a whole ClassID's top-level
        // node tree). MonoBehaviour objects must keep going through the per-object cache below.
        private const int MonoBehaviourTypeId = 114;

        // Keyed by object id, not the native type-tree handle. Used only for classes whose schema
        // can vary per instance (see MonoBehaviourTypeId) -- the safe-but-slower fallback.
        private readonly Dictionary<long, TypeTreeNode> _cacheByObjectId = new();

        // Keyed by ClassID (ObjectRef.TypeId), for classes whose on-disk type tree is provably
        // identical across every instance within one SerializedFile (i.e. everything except
        // MonoBehaviour). Distinct identifier space from _cacheByObjectId and _cacheByTypeTreeIndex.
        private readonly Dictionary<int, TypeTreeNode> _cacheByTypeId = new();

        // Keyed by type-tree index (SerializedFile.TypeTrees / GetTypeTreeByIndex).
        private readonly Dictionary<int, TypeTreeNode> _cacheByTypeTreeIndex = new();

        private readonly object _lock = new();

        private static bool CanShareAcrossInstances(int typeId) => typeId != MonoBehaviourTypeId;

        /// <summary>Thread-safe.</summary>
        internal TypeTreeNode GetOrBuild(SerializedFileHandle file, long objectId, int typeId)
        {
            return CanShareAcrossInstances(typeId)
                ? GetOrBuild(file, _cacheByTypeId, typeId, (api, h) => api.GetTypeTree(h, objectId))
                : GetOrBuild(file, _cacheByObjectId, objectId, (api, h) => api.GetTypeTree(h, objectId));
        }

        /// <summary>Thread-safe.</summary>
        internal TypeTreeNode GetOrBuildByIndex(SerializedFileHandle file, int index)
        {
            return GetOrBuild(file, _cacheByTypeTreeIndex, index, (api, h) => api.GetTypeTreeByIndex(h, index));
        }

        private TypeTreeNode GetOrBuild<TKey>(SerializedFileHandle file, Dictionary<TKey, TypeTreeNode> cache, TKey key,
            Func<IUnityFileSystemApi, IntPtr, IntPtr> resolveTypeTreeHandle)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            lock (_lock)
            {
                if (cache.TryGetValue(key, out var cached)) return cached;

                // The whole recursive tree walk happens inside one UseHandle scope, rather than one
                // per native call, so a concurrent Dispose() can't free the underlying SerializedFile
                // (and, transitively, the native typeTreeHandle this walk keeps dereferencing) partway
                // through.
                var root = file.UseHandle((api, h) =>
                {
                    var typeTreeHandle = resolveTypeTreeHandle(api, h);
                    var rootInfo = api.GetTypeTreeNodeInfo(typeTreeHandle, 0);
                    return BuildNode(api, typeTreeHandle, rootInfo);
                });
                cache[key] = root;
                return root;
            }
        }

        private static TypeTreeNode BuildNode(IUnityFileSystemApi api, IntPtr typeTreeHandle, TypeTreeNodeInfo info, int depth = 0)
        {
            if (depth > MaxRecursionDepth)
            {
                throw new ArtifactInspectorException(
                    $"Type tree node '{info.FieldName}' ({info.TypeName}) nests more than {MaxRecursionDepth} levels deep. The type tree is likely malformed or self-referential.");
            }

            var children = BuildChildren(api, typeTreeHandle, info.FirstChildNode, depth + 1);
            return new TypeTreeNode(info.FieldName, info.TypeName, info.Size, info.Flags, info.MetaFlags, children);
        }

        private static List<TypeTreeNode> BuildChildren(IUnityFileSystemApi api, IntPtr typeTreeHandle, int firstChildIndex, int depth)
        {
            var children = new List<TypeTreeNode>();
            var index = firstChildIndex;
            while (index > 0)
            {
                var info = api.GetTypeTreeNodeInfo(typeTreeHandle, index);
                children.Add(BuildNode(api, typeTreeHandle, info, depth));
                index = info.NextNode;
            }

            return children;
        }
    }
}