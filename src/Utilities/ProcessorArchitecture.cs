// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// Processor architecture utilities
    /// </summary>
    static public class ProcessorArchitecture
    {
        // Known processor architectures
        public const string X86 = "x86";
        public const string IA64 = "IA64";

        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "AMD", Justification = "This is the correct casing for ProcessorArchitecture")]
        public const string AMD64 = "AMD64";

        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "MSIL", Justification = "This is the correct casing for ProcessorArchitecture")]
        public const string MSIL = "MSIL";

        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "ARM", Justification = "This is the correct casing for ProcessorArchitecture")]
        public const string ARM = "ARM";

        static private string s_currentProcessArchitecture = null;
        static private bool s_currentProcessArchitectureInitialized = false;

        /// <summary>
        /// Lazy-initted property for getting the architecture of the currently running process
        /// </summary>
        static public string CurrentProcessArchitecture
        {
            get
            {
                if (s_currentProcessArchitectureInitialized)
                {
                    return s_currentProcessArchitecture;
                }

                s_currentProcessArchitectureInitialized = true;
                s_currentProcessArchitecture = ProcessorArchitecture.GetCurrentProcessArchitecture();

                return s_currentProcessArchitecture;
            }
        }

        // PInvoke delegate for IsWow64Process
        private delegate bool IsWow64ProcessDelegate([In] IntPtr hProcess, [Out] out bool Wow64Process);

        /// <summary>
        /// Gets the processor architecture of the currently running process
        /// </summary>
        /// <returns>null if unknown architecture or error, one of the known architectures otherwise</returns>
        static private string GetCurrentProcessArchitecture()
        {
            string architecture = null;

            IntPtr kernel32Dll = NativeMethodsShared.LoadLibrary("kernel32.dll");
            try
            {
                if (kernel32Dll != NativeMethodsShared.NullIntPtr)
                {
                    // This method gets the current architecture from the currently running msbuild.
                    // If the entry point is missing, we're running on Kernel older than WinXP
                    // http://msdn.microsoft.com/en-us/library/ms684139.aspx
                    IntPtr isWow64ProcessHandle = NativeMethodsShared.GetProcAddress(kernel32Dll, "IsWow64Process");

                    if (isWow64ProcessHandle == NativeMethodsShared.NullIntPtr)
                    {
                        architecture = ProcessorArchitecture.X86;
                    }
                    else
                    {
                        // entry point present, check if running in WOW
                        IsWow64ProcessDelegate isWow64Process = (IsWow64ProcessDelegate)Marshal.GetDelegateForFunctionPointer(isWow64ProcessHandle, typeof(IsWow64ProcessDelegate));
                        bool isWow64 = false;
                        bool success = isWow64Process(Process.GetCurrentProcess().Handle, out isWow64);

                        if (success)
                        {
                            // if it's running on WOW, must be an x86 process
                            if (isWow64)
                            {
                                architecture = ProcessorArchitecture.X86;
                            }
                            else
                            {
                                // it's a native process. Check the system architecture to determine the process architecture.
                                NativeMethodsShared.SYSTEM_INFO systemInfo = new NativeMethodsShared.SYSTEM_INFO();

                                NativeMethodsShared.GetSystemInfo(ref systemInfo);

                                switch (systemInfo.wProcessorArchitecture)
                                {
                                    case NativeMethodsShared.PROCESSOR_ARCHITECTURE_INTEL:
                                        architecture = ProcessorArchitecture.X86;
                                        break;

                                    case NativeMethodsShared.PROCESSOR_ARCHITECTURE_AMD64:
                                        architecture = ProcessorArchitecture.AMD64;
                                        break;

                                    case NativeMethodsShared.PROCESSOR_ARCHITECTURE_IA64:
                                        architecture = ProcessorArchitecture.IA64;
                                        break;

                                    case NativeMethodsShared.PROCESSOR_ARCHITECTURE_ARM:
                                        architecture = ProcessorArchitecture.ARM;
                                        break;

                                    // unknown architecture? return null
                                    default:
                                        architecture = null;
                                        break;
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                if (kernel32Dll != NativeMethodsShared.NullIntPtr)
                {
                    NativeMethodsShared.FreeLibrary(kernel32Dll);
                }
            }

            return architecture;
        }
    }
}
