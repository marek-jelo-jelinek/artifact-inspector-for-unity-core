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

        // Keyed by object id, not the native type-tree handle.
        private readonly Dictionary<long, TypeTreeNode> _cacheByObjectId = new();

        // Keyed by type-tree index (SerializedFile.TypeTrees / GetTypeTreeByIndex).
        private readonly Dictionary<int, TypeTreeNode> _cacheByTypeTreeIndex = new();

        private readonly object _lock = new();

        /// <summary>Thread-safe.</summary>
        internal TypeTreeNode GetOrBuild(SerializedFileHandle file, long objectId)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            lock (_lock)
            {
                if (_cacheByObjectId.TryGetValue(objectId, out var cached)) return cached;

                // The whole recursive tree walk happens inside one UseHandle scope, rather than one
                // per native call, so a concurrent Dispose() can't free the underlying SerializedFile
                // (and, transitively, the native typeTreeHandle this walk keeps dereferencing) partway
                // through.
                var root = file.UseHandle((api, h) =>
                {
                    var typeTreeHandle = api.GetTypeTree(h, objectId);
                    var rootInfo = api.GetTypeTreeNodeInfo(typeTreeHandle, 0);
                    return BuildNode(api, typeTreeHandle, rootInfo);
                });
                _cacheByObjectId[objectId] = root;
                return root;
            }
        }

        /// <summary>Thread-safe.</summary>
        internal TypeTreeNode GetOrBuildByIndex(SerializedFileHandle file, int index)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            lock (_lock)
            {
                if (_cacheByTypeTreeIndex.TryGetValue(index, out var cached)) return cached;

                var root = file.UseHandle((api, h) =>
                {
                    var typeTreeHandle = api.GetTypeTreeByIndex(h, index);
                    var rootInfo = api.GetTypeTreeNodeInfo(typeTreeHandle, 0);
                    return BuildNode(api, typeTreeHandle, rootInfo);
                });
                _cacheByTypeTreeIndex[index] = root;
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