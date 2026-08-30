using System;
using System.Runtime.InteropServices;

namespace Tesearis.ArtifactInspectorForUnity.Core.Native.Interop
{
    /// <summary>
    /// Manually resolves and loads a native shared library by absolute path.
    /// </summary>
    internal static class NativeLibraryLoader
    {
        internal static IntPtr Load(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Native library path must not be null or empty.", nameof(path));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var handle = Windows.LoadLibrary(path);
                if (handle != IntPtr.Zero) return handle;
                var win32Error = Marshal.GetLastWin32Error();
                throw new ArtifactInspectorException("Failed to load native library at '" + path + "' (Win32 error " + win32Error + ").");
            }
            else
            {
                var handle = Unix.dlopen(path, Unix.RTLD_NOW);
                return handle == IntPtr.Zero
                    ? throw new ArtifactInspectorException("Failed to load native library at '" + path + "'" + FormatDlError(Unix.dlerror()))
                    : handle;
            }
        }

        internal static IntPtr GetExport(IntPtr libraryHandle, string symbolName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var address = Windows.GetProcAddress(libraryHandle, symbolName);
                if (address != IntPtr.Zero) return address;
                var win32Error = Marshal.GetLastWin32Error();
                throw new ArtifactInspectorException("Native symbol '" + symbolName + "' was not found (Win32 error " + win32Error + ").");
            }
            else
            {
                var address = Unix.dlsym(libraryHandle, symbolName);
                return address == IntPtr.Zero
                    ? throw new ArtifactInspectorException("Native symbol '" + symbolName + "' was not found" + FormatDlError(Unix.dlerror()))
                    : address;
            }
        }

        internal static void Free(IntPtr libraryHandle)
        {
            if (libraryHandle == IntPtr.Zero) return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Windows.FreeLibrary(libraryHandle);
            }
            else
            {
                Unix.dlclose(libraryHandle);
            }
        }

        private static string FormatDlError(string dlError)
        {
            return string.IsNullOrEmpty(dlError) ? "." : ": " + dlError;
        }

        private static class Windows
        {
            [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern IntPtr LoadLibrary(string path);

            [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true, BestFitMapping = false)]
            internal static extern IntPtr GetProcAddress(IntPtr module, string procName);

            [DllImport("kernel32", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool FreeLibrary(IntPtr module);
        }

        private static class Unix
        {
            internal const int RTLD_NOW = 2;

            [DllImport("libdl", EntryPoint = "dlopen")]
            private static extern IntPtr dlopen_mac(string path, int flags);

            [DllImport("libdl", EntryPoint = "dlsym")]
            private static extern IntPtr dlsym_mac(IntPtr handle, string symbol);

            [DllImport("libdl", EntryPoint = "dlclose")]
            private static extern int dlclose_mac(IntPtr handle);

            [DllImport("libdl", EntryPoint = "dlerror")]
            private static extern IntPtr dlerror_mac();

            [DllImport("libdl.so.2", EntryPoint = "dlopen")]
            private static extern IntPtr dlopen_linux(string path, int flags);

            [DllImport("libdl.so.2", EntryPoint = "dlsym")]
            private static extern IntPtr dlsym_linux(IntPtr handle, string symbol);

            [DllImport("libdl.so.2", EntryPoint = "dlclose")]
            private static extern int dlclose_linux(IntPtr handle);

            [DllImport("libdl.so.2", EntryPoint = "dlerror")]
            private static extern IntPtr dlerror_linux();

            internal static IntPtr dlopen(string path, int flags)
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? dlopen_mac(path, flags) : dlopen_linux(path, flags);
            }

            internal static IntPtr dlsym(IntPtr handle, string symbol)
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? dlsym_mac(handle, symbol) : dlsym_linux(handle, symbol);
            }

            internal static void dlclose(IntPtr handle)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    dlclose_mac(handle);
                }
                else
                {
                    dlclose_linux(handle);
                }
            }

            internal static string dlerror()
            {
                var message = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? dlerror_mac() : dlerror_linux();
                return message == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(message);
            }
        }
    }
}