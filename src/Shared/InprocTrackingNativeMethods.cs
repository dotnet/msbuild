// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_FILE_TRACKER

using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Microsoft.Build.Shared.FileSystem;
using Windows.Win32;
using Windows.Win32.Foundation;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Managed shim over <c>FileTracker.dll</c> — the native Detours-based file-access tracker
    /// shipped alongside MSBuild on .NET Framework.
    /// </summary>
    /// <remarks>
    /// FileTracker.dll is not on PATH (it lives in the MSBuild tools directory) and there is
    /// no <c>DllImportSearchPath</c> that explicitly points to it, so the module is loaded by
    /// absolute path via <c>LoadLibrary</c> and each export is resolved through
    /// <c>GetProcAddress</c> into a typed <c>delegate* unmanaged[Stdcall]</c> function pointer.
    /// Slot table from <c>FileTracker.def</c>: <c>StartTrackingContext</c> @2,
    /// <c>StartTrackingContextWithRoot</c> @3, <c>EndTrackingContext</c> @4,
    /// <c>StopTrackingAndCleanup</c> @5, <c>SuspendTracking</c> @6, <c>ResumeTracking</c> @7,
    /// <c>WriteAllTLogs</c> @8, <c>WriteContextTLogs</c> @9, <c>SetThreadCount</c> @10.
    /// All exports are <c>HRESULT WINAPI Name(...)</c> with Unicode <c>LPCTSTR</c> arguments
    /// (FileTracker.dll is built <c>UNICODE</c>), so the function-pointer signatures take
    /// <see cref="PCWSTR"/> and return <see cref="HRESULT"/>.
    /// </remarks>
    internal static unsafe class InprocTrackingNativeMethods
    {
        #region Public API

        /// <summary>
        /// Begin a new tracking context on the current thread. File accesses on this thread
        /// (and any worker threads pre-allocated via <see cref="SetThreadCount"/>) will
        /// accumulate into per-context read/write/delete TLog lists keyed by
        /// <paramref name="taskName"/> until <see cref="EndTrackingContext"/> is called.
        /// </summary>
        /// <param name="intermediateDirectory">Directory in which TLog files for this context will be written when <see cref="WriteContextTLogs"/> / <see cref="WriteAllTLogs"/> is called.</param>
        /// <param name="taskName">Logical task name; used as the TLog file-name prefix and as the key in the context stack.</param>
        internal static void StartTrackingContext(string intermediateDirectory, string taskName)
        {
            fixed (char* pDir = intermediateDirectory)
            {
                fixed (char* pTask = taskName)
                {
                    FileTrackerDllStub.StartTrackingContext(new PCWSTR(pDir), new PCWSTR(pTask)).ThrowOnFailure();
                }
            }
        }

        /// <summary>
        /// Same as <see cref="StartTrackingContext"/> but also supplies a response file from
        /// which the rooting marker (the canonical "this is the build root" identifier
        /// embedded in TLog headers) is read.
        /// </summary>
        /// <param name="intermediateDirectory">Output directory for TLogs.</param>
        /// <param name="taskName">Logical task name (TLog file-name prefix).</param>
        /// <param name="rootMarker">Path to a response file containing the rooting-marker string.</param>
        internal static void StartTrackingContextWithRoot(string intermediateDirectory, string taskName, string rootMarker)
        {
            fixed (char* pDir = intermediateDirectory)
            {
                fixed (char* pTask = taskName)
                {
                    fixed (char* pRoot = rootMarker)
                    {
                        FileTrackerDllStub.StartTrackingContextWithRoot(new PCWSTR(pDir), new PCWSTR(pTask), new PCWSTR(pRoot)).ThrowOnFailure();
                    }
                }
            }
        }

        /// <summary>
        /// Pop the top-most tracking context off the current thread's context stack. After
        /// this returns, file accesses on the thread fall through to the next context on the
        /// stack (if any) or stop being tracked.
        /// </summary>
        internal static void EndTrackingContext()
        {
            FileTrackerDllStub.EndTrackingContext().ThrowOnFailure();
        }

        /// <summary>
        /// Tear down every tracking context on every tracked thread and release the
        /// process-wide tracking data structures. Tracking is fully disabled after this call
        /// until a fresh <see cref="StartTrackingContext"/> reactivates it. The FileTracker
        /// module itself remains loaded (it must — see remarks on the class).
        /// </summary>
        internal static void StopTrackingAndCleanup()
        {
            FileTrackerDllStub.StopTrackingAndCleanup().ThrowOnFailure();
        }

        /// <summary>
        /// Temporarily pause recording into the current thread's active tracking context.
        /// File accesses between this call and <see cref="ResumeTracking"/> are not added to
        /// the context's TLog lists.
        /// </summary>
        internal static void SuspendTracking()
        {
            FileTrackerDllStub.SuspendTracking().ThrowOnFailure();
        }

        /// <summary>
        /// Resume recording into the current thread's active tracking context after a prior
        /// <see cref="SuspendTracking"/>.
        /// </summary>
        internal static void ResumeTracking()
        {
            FileTrackerDllStub.ResumeTracking().ThrowOnFailure();
        }

        /// <summary>
        /// Flush the accumulated read/write/delete TLogs for every tracking context across
        /// every tracked thread to disk under <paramref name="intermediateDirectory"/>, using
        /// <paramref name="tlogRootName"/> as the file-name prefix. The contexts remain live
        /// and continue to accumulate entries after this call.
        /// </summary>
        /// <param name="intermediateDirectory">Output directory for the TLogs.</param>
        /// <param name="tlogRootName">File-name prefix for the produced <c>*.read.tlog</c> / <c>*.write.tlog</c> / <c>*.delete.tlog</c> files.</param>
        internal static void WriteAllTLogs(string intermediateDirectory, string tlogRootName)
        {
            fixed (char* pDir = intermediateDirectory)
            {
                fixed (char* pRoot = tlogRootName)
                {
                    FileTrackerDllStub.WriteAllTLogs(new PCWSTR(pDir), new PCWSTR(pRoot)).ThrowOnFailure();
                }
            }
        }

        /// <summary>
        /// Flush the read/write/delete TLogs for just the current thread's active tracking
        /// context to disk and clear those in-memory lists (so subsequent activity in the same
        /// context is recorded into freshly empty lists).
        /// </summary>
        /// <param name="intermediateDirectory">Output directory for the TLogs.</param>
        /// <param name="tlogRootName">File-name prefix for the produced TLog files.</param>
        internal static void WriteContextTLogs(string intermediateDirectory, string tlogRootName)
        {
            fixed (char* pDir = intermediateDirectory)
            {
                fixed (char* pRoot = tlogRootName)
                {
                    FileTrackerDllStub.WriteContextTLogs(new PCWSTR(pDir), new PCWSTR(pRoot)).ThrowOnFailure();
                }
            }
        }

        /// <summary>
        /// Declare to FileTracker how many worker threads the host intends to use. FileTracker
        /// pre-allocates per-thread TLS slots / counters from this value so that worker
        /// threads can attach their own tracking contexts without taking a global lock at
        /// runtime.
        /// </summary>
        /// <param name="threadCount">Maximum concurrent worker-thread count.</param>
        internal static void SetThreadCount(int threadCount)
        {
            FileTrackerDllStub.SetThreadCount(threadCount).ThrowOnFailure();
        }

        #endregion // Public API

        private static class FileTrackerDllStub
        {
            // Architecture-specific FileTracker DLL name. The native module is built per
            // architecture (x86 / x64 / arm64); the host process must load the variant whose
            // bitness and ISA match its own so that Detours can hook the right import table.
            private static readonly Lazy<string> s_fileTrackerDllName = new(() =>
                RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                    ? "FileTrackerA4.dll"
                    : IntPtr.Size == sizeof(int)
                        ? "FileTracker32.dll"
                        : "FileTracker64.dll");

            // Handle for FileTracker.dll itself. The HMODULE is intentionally never freed:
            // FileTracker detours ExitProcess and forcibly unloading it would corrupt CLR
            // shutdown (matching the historical SafeLibraryHandle.ReleaseHandle no-op).
            [SecurityCritical]
            private static HMODULE s_fileTrackerDllHandle;

            // Function-pointer slots, populated by InitDelegates() from the LoadLibrary'd
            // module via GetProcAddress. Signatures track the native exports verbatim:
            // every export is __stdcall HRESULT-returning and takes Unicode LPCWSTR arguments
            // (FileTracker.dll is built UNICODE).
            internal static delegate* unmanaged[Stdcall]<PCWSTR, PCWSTR, HRESULT> StartTrackingContext;
            internal static delegate* unmanaged[Stdcall]<PCWSTR, PCWSTR, PCWSTR, HRESULT> StartTrackingContextWithRoot;
            internal static delegate* unmanaged[Stdcall]<HRESULT> EndTrackingContext;
            internal static delegate* unmanaged[Stdcall]<HRESULT> StopTrackingAndCleanup;
            internal static delegate* unmanaged[Stdcall]<HRESULT> SuspendTracking;
            internal static delegate* unmanaged[Stdcall]<HRESULT> ResumeTracking;
            internal static delegate* unmanaged[Stdcall]<PCWSTR, PCWSTR, HRESULT> WriteAllTLogs;
            internal static delegate* unmanaged[Stdcall]<PCWSTR, PCWSTR, HRESULT> WriteContextTLogs;
            internal static delegate* unmanaged[Stdcall]<int, HRESULT> SetThreadCount;

            /// <summary>
            /// Loads FileTracker.dll from the MSBuild tools directory into
            /// <see cref="s_fileTrackerDllHandle"/>.
            /// </summary>
            private static void LoadFileTrackerDll()
            {
                string buildToolsPath = FrameworkLocationHelper.GeneratePathToBuildToolsForToolsVersion(MSBuildConstants.CurrentToolsVersion, DotNetFrameworkArchitecture.Current);
                string fileTrackerPath = Path.Combine(buildToolsPath, s_fileTrackerDllName.Value);

                if (!FileSystems.Default.FileExists(fileTrackerPath))
                {
                    throw new DllNotFoundException(s_fileTrackerDllName.Value);
                }

                HMODULE handle;
                fixed (char* pPath = fileTrackerPath)
                {
                    handle = PInvoke.LoadLibrary(new PCWSTR(pPath));
                }

                if (handle.IsNull)
                {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error(), new IntPtr(-1));

                    // If Marshal.ThrowExceptionForHR did not throw, force a throw here.
                    throw new InvalidOperationException();
                }

                s_fileTrackerDllHandle = handle;
            }

            /// <summary>
            /// Resolve <paramref name="entryPointName"/> from the loaded FileTracker module
            /// and return its address as an unmanaged function pointer.
            /// </summary>
            [SecurityCritical]
            private static void* GetExport(string entryPointName)
            {
                // GetProcAddress takes a system-default-code-page (ANSI) string. Encode through
                // the real encoder so DBCS / multi-byte characters in an unusual export name
                // are handled correctly. Rent from ArrayPool to avoid a per-call allocation.
                //
                // Encoding.Default returns the system ANSI code page on .NET Framework
                // (correct for GetProcAddress) but UTF-8 on .NET (Core). FEATURE_FILE_TRACKER
                // is only defined for net472 so this branch is correct as-is; if the #if
                // guard ever broadens to .NET (Core), switch to a CodePagesEncodingProvider
                // ANSI encoding instead.
                Encoding ansi = Encoding.Default;
                int maxByteCount = ansi.GetMaxByteCount(entryPointName.Length) + 1;
                byte[] buffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
                try
                {
                    int written = ansi.GetBytes(entryPointName, 0, entryPointName.Length, buffer, 0);
                    buffer[written] = 0;

                    FARPROC entryPoint;
                    fixed (byte* pName = buffer)
                    {
                        entryPoint = PInvoke.GetProcAddress(s_fileTrackerDllHandle, new PCSTR(pName));
                    }

                    if (entryPoint.IsNull)
                    {
                        throw new EntryPointNotFoundException(s_fileTrackerDllName.Value + "!" + entryPointName);
                    }

                    return (void*)entryPoint.Value;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            /// <summary>
            /// Resolve every export FileTracker.dll publishes that the managed shim cares
            /// about, populating the function-pointer fields.
            /// </summary>
            private static void InitDelegates()
            {
                Assumed.False(s_fileTrackerDllHandle.IsNull, "Handle for FileTracker.dll should not be null");

                StartTrackingContext         = (delegate* unmanaged[Stdcall]<PCWSTR, PCWSTR, HRESULT>)GetExport(nameof(StartTrackingContext));
                StartTrackingContextWithRoot = (delegate* unmanaged[Stdcall]<PCWSTR, PCWSTR, PCWSTR, HRESULT>)GetExport(nameof(StartTrackingContextWithRoot));
                EndTrackingContext           = (delegate* unmanaged[Stdcall]<HRESULT>)GetExport(nameof(EndTrackingContext));
                StopTrackingAndCleanup       = (delegate* unmanaged[Stdcall]<HRESULT>)GetExport(nameof(StopTrackingAndCleanup));
                SuspendTracking              = (delegate* unmanaged[Stdcall]<HRESULT>)GetExport(nameof(SuspendTracking));
                ResumeTracking               = (delegate* unmanaged[Stdcall]<HRESULT>)GetExport(nameof(ResumeTracking));
                WriteAllTLogs                = (delegate* unmanaged[Stdcall]<PCWSTR, PCWSTR, HRESULT>)GetExport(nameof(WriteAllTLogs));
                WriteContextTLogs            = (delegate* unmanaged[Stdcall]<PCWSTR, PCWSTR, HRESULT>)GetExport(nameof(WriteContextTLogs));
                SetThreadCount               = (delegate* unmanaged[Stdcall]<int, HRESULT>)GetExport(nameof(SetThreadCount));
            }

            /// <summary>
            /// Type initializer: loads the native module and resolves every export once
            /// before any of the function-pointer fields are read.
            /// </summary>
            [SecuritySafeCritical]
            static FileTrackerDllStub()
            {
                LoadFileTrackerDll();
                InitDelegates();
            }
        }
    }
}
#endif
