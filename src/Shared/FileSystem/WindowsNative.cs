// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Shared.FileSystem
{
    /// <summary>
    /// Native implementation of file system operations
    /// </summary>
    internal static class WindowsNative
    {
        /// <summary>
        /// Maximum path length.
        /// </summary>
        public const int MaxPath = 260;

        /// <summary>
        /// ERROR_SUCCESS
        /// </summary>
        public const int ErrorSuccess = 0x0;

        /// <summary>
        /// ERROR_FILE_NOT_FOUND
        /// </summary>
        public const int ErrorFileNotFound = 0x2;

        /// <summary>
        /// ERROR_PATH_NOT_FOUND
        /// </summary>
        public const int ErrorPathNotFound = 0x3;

        /// <summary>
        /// ERROR_DIRECTORY
        /// </summary>
        public const int ErrorDirectory = 0x10b;

        /// <summary>
        /// ERROR_ACCESS_DENIED
        /// </summary>
        public const int ErrorAccessDenied = 0x5;

        /// <summary>
        /// ERROR_NO_MORE_FILES
        /// </summary>
        public const uint ErrorNoMoreFiles = 0x12;

        /// <summary>
        /// Modifies the search condition of PathMatchSpecEx
        /// </summary>
        /// <remarks>
        /// <see ref="https://msdn.microsoft.com/en-us/library/windows/desktop/bb773728(v=vs.85).aspx"/>
        /// </remarks>
        public static class DwFlags
        {
            /// <summary>
            /// The pszSpec parameter points to a single file name pattern to be matched.
            /// </summary>
            public const int PmsfNormal = 0x0;

            /// <summary>
            /// The pszSpec parameter points to a semicolon-delimited list of file name patterns to be matched.
            /// </summary>
            public const int PmsfMultiple = 0x1;

            /// <summary>
            /// If PMSF_NORMAL is used, ignore leading spaces in the string pointed to by pszSpec. If PMSF_MULTIPLE is used, 
            /// ignore leading spaces in each file type contained in the string pointed to by pszSpec. This flag can be combined with PMSF_NORMAL and PMSF_MULTIPLE.
            /// </summary>
            public const int PmsfDontStripSpaces = 0x00010000;
        }

        /// <summary>
        /// Status of attempting to enumerate a directory.
        /// </summary>
        public enum EnumerateDirectoryStatus
        {
            /// <summary>
            /// Enumeration of an existent directory succeeded.
            /// </summary>
            Success,

            /// <summary>
            /// One or more path components did not exist, so the search directory could not be opened.
            /// </summary>
            SearchDirectoryNotFound,

            /// <summary>
            /// A path component in the search path refers to a file. Only directories can be enumerated.
            /// </summary>
            CannotEnumerateFile,

            /// <summary>
            /// Directory enumeration could not complete due to denied access to the search directory or a file inside.
            /// </summary>
            AccessDenied,

            /// <summary>
            /// Directory enumeration failed without a well-known status (see <see cref="EnumerateDirectoryResult.NativeErrorCode"/>).
            /// </summary>
            UnknownError,
        }

        /// <summary>
        /// Represents the result of attempting to enumerate a directory.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance",
            "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
        public struct EnumerateDirectoryResult
        {
            /// <summary>
            /// Enumerated directory.
            /// </summary>
            public readonly string Directory;

            /// <summary>
            /// Overall status indication.
            /// </summary>
            public readonly EnumerateDirectoryStatus Status;

            /// <summary>
            /// Native error code. Note that an error code other than <c>ERROR_SUCCESS</c> may be present even on success.
            /// </summary>
            public readonly int NativeErrorCode;

            /// <nodoc />
            public EnumerateDirectoryResult(string directory, EnumerateDirectoryStatus status, int nativeErrorCode)
            {
                Directory = directory;
                Status = status;
                NativeErrorCode = nativeErrorCode;
            }

            /// <summary>
            /// Indicates if enumeration succeeded.
            /// </summary>
            public bool Succeeded
            {
                get { return Status == EnumerateDirectoryStatus.Success; }
            }

            /// <summary>
            /// Throws an exception if the native error code could not be canonicalized (a fairly exceptional circumstance).
            /// This is allowed when <see cref="Status"/> is <see cref="EnumerateDirectoryStatus.UnknownError"/>.
            /// </summary>
            /// <remarks>
            /// This is a good <c>default:</c> case when switching on every possible <see cref="EnumerateDirectoryStatus"/>
            /// </remarks>
            public NativeWin32Exception ThrowForUnknownError()
            {
                Debug.Assert(Status == EnumerateDirectoryStatus.UnknownError);
                throw CreateExceptionForError();
            }

            /// <summary>
            /// Throws an exception if the native error code was corresponds to a known <see cref="EnumerateDirectoryStatus"/>
            /// (and enumeration was not successful).
            /// </summary>
            public NativeWin32Exception ThrowForKnownError()
            {
                Debug.Assert(Status != EnumerateDirectoryStatus.UnknownError &&
                             Status != EnumerateDirectoryStatus.Success);
                throw CreateExceptionForError();
            }

            /// <summary>
            /// Creates (but does not throw) an exception for this result. The result must not be successful.
            /// </summary>
            public NativeWin32Exception CreateExceptionForError()
            {
                Debug.Assert(Status != EnumerateDirectoryStatus.Success);
                if (Status == EnumerateDirectoryStatus.UnknownError)
                {
                    return new NativeWin32Exception(
                        NativeErrorCode,
                        "Enumerating a directory failed");
                }
                else
                {
                    return new NativeWin32Exception(
                        NativeErrorCode,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Enumerating a directory failed: {0:G}", Status));
                }
            }
        }

        /// <summary>
        /// <c>Win32FindData</c>
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
        public struct Win32FindData
        {
            /// <summary>
            /// The file attributes of a file
            /// </summary>
            public FileAttributes DwFileAttributes;

            /// <summary>
            /// Specified when a file or directory was created
            /// </summary>
            public System.Runtime.InteropServices.ComTypes.FILETIME FtCreationTime;

            /// <summary>
            /// Specifies when the file was last read from, written to, or for executable files, run.
            /// </summary>
            public System.Runtime.InteropServices.ComTypes.FILETIME FtLastAccessTime;

            /// <summary>
            /// For a file, the structure specifies when the file was last written to, truncated, or overwritten.
            /// For a directory, the structure specifies when the directory is created.
            /// </summary>
            public System.Runtime.InteropServices.ComTypes.FILETIME FtLastWriteTime;

            /// <summary>
            /// The high-order DWORD value of the file size, in bytes.
            /// </summary>
            public uint NFileSizeHigh;

            /// <summary>
            /// The low-order DWORD value of the file size, in bytes.
            /// </summary>
            public uint NFileSizeLow;

            /// <summary>
            /// If the dwFileAttributes member includes the FILE_ATTRIBUTE_REPARSE_POINT attribute, this member specifies the reparse point tag.
            /// </summary>
            public uint DwReserved0;

            /// <summary>
            /// Reserved for future use.
            /// </summary>
            public uint DwReserved1;

            /// <summary>
            /// The name of the file.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)]
            public string CFileName;

            /// <summary>
            /// An alternative name for the file.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string CAlternate;
        }

        /// <nodoc/>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [SuppressMessage("Microsoft.Interoperability", "CA1401:PInvokesShouldNotBeVisible", Justification = "Needed for custom enumeration.")]
        public static extern SafeFindFileHandle FindFirstFileW(
            string lpFileName,
            out Win32FindData lpFindFileData);

        /// <nodoc/>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [SuppressMessage("Microsoft.Interoperability", "CA1401:PInvokesShouldNotBeVisible", Justification = "Needed for custom enumeration.")]
        public static extern bool FindNextFileW(SafeHandle hFindFile, out Win32FindData lpFindFileData);

        /// <nodoc/>
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        [SuppressMessage("Microsoft.Interoperability", "CA1401:PInvokesShouldNotBeVisible", Justification = "Needed for creating symlinks.")]
        public static extern int PathMatchSpecExW([In] string pszFileParam, [In] string pszSpec, [In] int flags);

        /// <nodoc/>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FindClose(IntPtr findFileHandle);
    }
}
