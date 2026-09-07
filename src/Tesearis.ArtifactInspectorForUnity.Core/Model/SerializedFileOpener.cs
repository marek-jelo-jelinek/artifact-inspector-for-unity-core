using System;
using Tesearis.ArtifactInspectorForUnity.Core.Native;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Handles;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Interop;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.Model
{
    /// <summary>
    /// Shared "open a SerializedFile handle pair at a path, with missing-TypeTrees triage" logic
    /// behind both <see cref="ArtifactArchive.OpenSerializedFile"/> (an archive-relative virtual
    /// path) and <c>ArtifactInspector.OpenSerializedFile</c> (a real filesystem path to a
    /// loose Player Build output file) -- the native calls involved (<c>UFS_OpenSerializedFile</c>,
    /// <c>UFS_OpenFile</c>) don't care which kind of path they're given, so the open/triage/wrap
    /// sequence is identical either way.
    /// </summary>
    internal static class SerializedFileOpener
    {
        /// <summary>
        /// Opens the SerializedFile at <paramref name="path"/>, wrapping the result in a fresh
        /// <see cref="TypeTreeCache"/>. Falls the failure through
        /// <paramref name="isPositivelyMissingTypeTrees"/> to decide whether a native open failure
        /// should surface as <see cref="SerializedFileOpenException"/> (with <see cref="SerializedFileOpenException.MissingTypeTrees"/>
        /// set) instead of a bare <see cref="NativeCallException"/>.
        /// </summary>
        /// <param name="api">The native API to open through.</param>
        /// <param name="path">The virtual path (archive-relative) or real filesystem path (loose file) to open.</param>
        /// <param name="label">Used only for the exception message/EntryName -- an archive entry name, or the loose file's path.</param>
        /// <param name="isPositivelyMissingTypeTrees">Best-effort check for the missing-TypeTrees triage; see <see cref="SafeInvoke"/>.</param>
        internal static SerializedFile Open(IUnityFileSystemApi api, string path, string label, Func<bool> isPositivelyMissingTypeTrees)
        {
            SerializedFileHandle serializedFileHandle;
            try
            {
                var rawSerializedHandle = api.OpenSerializedFile(path);
                serializedFileHandle = new SerializedFileHandle(api, rawSerializedHandle);
            }
            catch (NativeCallException) when (SafeInvoke(isPositivelyMissingTypeTrees))
            {
                // UFS_OpenSerializedFile refuses to open files with no TypeTrees at all, and
                // doesn't reliably report one consistent native error for that case.
                throw new SerializedFileOpenException(label, missingTypeTrees: true);
            }

            FileHandle fileHandle = null;
            try
            {
                var rawFileHandle = api.OpenFile(path);
                fileHandle = new FileHandle(api, rawFileHandle);
                return new SerializedFile(serializedFileHandle, fileHandle, new TypeTreeCache());
            }
            catch
            {
                // Construction failed, so dispose here before rethrowing.
                fileHandle?.Dispose();
                serializedFileHandle.Dispose();
                throw;
            }
        }

        /// <summary>Runs predicate, treating any exception it throws as an inconclusive "no" -- this is a best-effort check.</summary>
        internal static bool SafeInvoke(Func<bool> predicate)
        {
            try
            {
                return predicate();
            }
            catch
            {
                return false;
            }
        }
    }
}
