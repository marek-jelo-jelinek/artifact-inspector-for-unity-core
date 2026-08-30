using System;

namespace Tesearis.ArtifactInspectorForUnity.Core.Native
{
    internal static class ArchiveMountPoint
    {
        private const string SchemePrefix = "archive://";

        internal static string NewMountPoint() => SchemePrefix + Guid.NewGuid().ToString("N") + "/";
    }
}