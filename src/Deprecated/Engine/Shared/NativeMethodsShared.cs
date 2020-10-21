// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.Runtime.InteropServices;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// Interop methods.
    /// </summary>
    /// <owner>LukaszG</owner>
    internal static class NativeMethods
    {
        #region Constants

        internal const uint ERROR_INSUFFICIENT_BUFFER = 0x8007007A;
        internal const uint STARTUP_LOADER_SAFEMODE = 0x10;
        internal const uint S_OK = 0x0;
        internal const uint RUNTIME_INFO_DONT_SHOW_ERROR_DIALOG = 0x40;
        internal const uint FILE_TYPE_CHAR = 0x0002;
        internal const Int32 STD_OUTPUT_HANDLE = -11;

        private const string kernel32Dll = "kernel32.dll";
        private const string mscoreeDLL = "mscoree.dll";

        internal static HandleRef NullHandleRef = new HandleRef(null, IntPtr.Zero);

        internal static IntPtr NullIntPtr = new IntPtr(0);
        #endregion

        #region Member data

        /// <summary>
        /// Default buffer size to use when dealing with the Windows API.
        /// </summary>
        /// <remarks>
        /// This member is intentionally not a constant because we want to allow
        /// unit tests to change it.
        /// </remarks>
        internal static int MAX_PATH = 260;

        #endregion

        #region Wrapper methods

        /// <summary>
        /// Looks for the given file in the system path i.e. all locations in
        /// the %PATH% environment variable.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>The location of the file, or null if file not found.</returns>
        internal static string FindOnPath(string filename)
        {
            StringBuilder pathBuilder = new StringBuilder(MAX_PATH + 1);
            string pathToFile = null;

            // we may need to make two attempts because there's a small chance
            // the buffer may not be sized correctly the first time
            for (int i = 0; i < 2; i++)
            {
                uint result = SearchPath
                                (
                                    null /* search the system path */,
                                    filename /* look for this file */,
                                    null /* don't add an extra extension to the filename when searching */,
                                    pathBuilder.Capacity /* size of buffer */,
                                    pathBuilder /* buffer to write path into */,
                                    null /* don't want pointer to filename in the return path */
                                );

                // if the buffer is not big enough
                if (result > pathBuilder.Capacity)
                {
                    ErrorUtilities.VerifyThrow(i == 0, "We should not have to resize the buffer twice.");

                    // resize the buffer and try again
                    pathBuilder.Capacity = (int)result;
                }
                else if (result > 0)
                {
                    // file was found, so don't make another attempt
                    pathToFile = pathBuilder.ToString();
                    break;
                }
                else
                {
                    // file was not found, so quit
                    break;
                }
            }

            return pathToFile;
        }

        #endregion

        #region PInvoke

        /// <summary>
        /// Gets the current OEM code page which is used by console apps 
        /// (as opposed to the Windows/ANSI code page)
        /// Basically for each ANSI code page (set in Regional settings) there's a corresponding OEM code page 
        /// that needs to be used for instance when writing to batch files
        /// </summary>
        /// <owner>LukaszG</owner>
        [DllImport(kernel32Dll)]
        internal static extern int GetOEMCP();


        [DllImport(kernel32Dll, SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint SearchPath
        (
            string path,
            string fileName,
            string extension,
            int numBufferChars,
            StringBuilder buffer,
            int[] filePart
        );


        [DllImport("kernel32.dll", PreserveSig = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary([In] IntPtr module);

        [DllImport("kernel32.dll", PreserveSig = true, BestFitMapping = false, ThrowOnUnmappableChar = true, CharSet = CharSet.Ansi, SetLastError = true)]
        internal static extern IntPtr GetProcAddress(IntPtr module, string procName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
        internal static extern IntPtr LoadLibrary(string fileName);

        [DllImport(mscoreeDLL, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern uint GetRequestedRuntimeInfo(String pExe,
                                                String pwszVersion,
                                                String pConfigurationFile,
                                                uint startupFlags,
                                                uint runtimeInfoFlags,
                                                StringBuilder pDirectory,
                                                int dwDirectory,
                                                out uint dwDirectoryLength,
                                                StringBuilder pVersion,
                                                int cchBuffer,
                                                out uint dwlength);

        /// <summary>
        /// Gets the fully qualified filename of the currently executing .exe
        /// </summary>
        [DllImport(kernel32Dll, SetLastError=true, CharSet = CharSet.Unicode)]
        internal static extern int GetModuleFileName(HandleRef hModule, StringBuilder buffer, int length);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        internal static extern uint GetFileType(IntPtr hFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int GetCurrentDirectory(int nBufferLength, StringBuilder lpBuffer);

        #endregion
    }
}
