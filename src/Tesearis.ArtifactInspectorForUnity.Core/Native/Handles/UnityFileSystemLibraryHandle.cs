using System;
using System.Runtime.InteropServices;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Interop;

namespace Tesearis.ArtifactInspectorForUnity.Core.Native.Handles
{
    internal sealed class UnityFileSystemLibraryHandle : SafeHandle
    {
        private readonly UnityFileSystemApi _api;

        private UnityFileSystemLibraryHandle() : base(IntPtr.Zero, ownsHandle: true)
        {
            _api = new UnityFileSystemApi();
            SetHandle(_api.LibraryHandle);
        }

        /// <summary>
        /// Exposed as the interface, not the concrete <see cref="UnityFileSystemApi"/>, so archive/file/
        /// SerializedFile handles and the Model/TypeTree layers depend only on the call surface they
        /// actually use.
        /// </summary>
        internal IUnityFileSystemApi Api => _api;

        public override bool IsInvalid => handle == IntPtr.Zero;

        internal static UnityFileSystemLibraryHandle LoadAndInit()
        {
            var handle = new UnityFileSystemLibraryHandle();
            handle._api.Init();
            return handle;
        }

        protected override bool ReleaseHandle()
        {
            try
            {
                _api.Cleanup();
            }
            catch
            {
                // SafeHandle.ReleaseHandle must never throw.
            }
            finally
            {
                NativeLibraryLoader.Free(handle);
            }

            return true;
        }
    }
}