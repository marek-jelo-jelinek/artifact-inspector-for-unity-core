using System;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Interop;

namespace Tesearis.ArtifactInspectorForUnity.Core.Native.Handles
{
    internal sealed class SerializedFileHandle : UnityNativeHandle
    {
        internal SerializedFileHandle(IUnityFileSystemApi api, IntPtr handle) : base(api, handle)
        {
        }

        protected override void Release(IUnityFileSystemApi api) => api.CloseSerializedFile(handle);
    }
}
