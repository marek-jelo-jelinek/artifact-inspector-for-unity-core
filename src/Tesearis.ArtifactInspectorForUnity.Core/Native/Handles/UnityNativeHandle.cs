using System;
using System.Runtime.InteropServices;
using Tesearis.ArtifactInspectorForUnity.Core.Native.Interop;

namespace Tesearis.ArtifactInspectorForUnity.Core.Native.Handles
{
    /// <summary>Base SafeHandle for one handle obtained through <see cref="IUnityFileSystemApi"/>.</summary>
    internal abstract class UnityNativeHandle : SafeHandle
    {
        protected UnityNativeHandle(IUnityFileSystemApi api, IntPtr handle) : base(IntPtr.Zero, ownsHandle: true)
        {
            Api = api ?? throw new ArgumentNullException(nameof(api));
            SetHandle(handle);
        }

        internal IUnityFileSystemApi Api { get; }

        public override bool IsInvalid => handle == IntPtr.Zero;
        protected abstract void Release(IUnityFileSystemApi api);

        internal TResult UseHandle<TResult>(Func<IUnityFileSystemApi, IntPtr, TResult> call)
        {
            var acquired = false;
            try
            {
                DangerousAddRef(ref acquired);
                return call(Api, handle);
            }
            finally
            {
                if (acquired) DangerousRelease();
            }
        }

        /// <summary>Like <see cref="UseHandle{TResult}"/>, for calls with no return value.</summary>
        internal void UseHandle(Action<IUnityFileSystemApi, IntPtr> call)
        {
            var acquired = false;
            try
            {
                DangerousAddRef(ref acquired);
                call(Api, handle);
            }
            finally
            {
                if (acquired) DangerousRelease();
            }
        }

        protected sealed override bool ReleaseHandle()
        {
            try
            {
                Release(Api);
            }
            catch
            {
                // SafeHandle.ReleaseHandle must never throw.
            }

            return true;
        }
    }
}
