// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32;

#nullable disable

namespace Microsoft.Build.Framework;

internal static class NativeMethods
{
    private const int FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

    /// <summary>
    /// Default buffer size to use when dealing with the Windows API.
    /// </summary>
    private const int MAX_PATH = 260;

    private const string WINDOWS_FILE_SYSTEM_REGISTRY_KEY = @"SYSTEM\CurrentControlSet\Control\FileSystem";
    private const string WINDOWS_LONG_PATHS_ENABLED_VALUE_NAME = "LongPathsEnabled";

    // As defined in winnt.h:
    private const ushort PROCESSOR_ARCHITECTURE_INTEL = 0;
    private const ushort PROCESSOR_ARCHITECTURE_ARM = 5;
    private const ushort PROCESSOR_ARCHITECTURE_IA64 = 6;
    private const ushort PROCESSOR_ARCHITECTURE_AMD64 = 9;
    private const ushort PROCESSOR_ARCHITECTURE_ARM64 = 12;

    private enum LOGICAL_PROCESSOR_RELATIONSHIP
    {
        RelationProcessorCore,
        RelationNumaNode,
        RelationCache,
        RelationProcessorPackage,
        RelationGroup,
        RelationAll = 0xffff
    }

    private struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
    {
        public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;
        public uint Size;
        public PROCESSOR_RELATIONSHIP Processor;

        public SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX()
        {
            Relationship = default;
            Size = default;
            Processor = default;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct PROCESSOR_RELATIONSHIP
    {
        public byte Flags;
        private byte EfficiencyClass;
        private fixed byte Reserved[20];
        public ushort GroupCount;
        public IntPtr GroupInfo;
    }

    /// <summary>
    /// Processor architecture values
    /// </summary>
    public enum ProcessorArchitectures
    {
        // Intel 32 bit
        X86,

        // AMD64 64 bit
        X64,

        // Itanium 64
        IA64,

        // ARM
        ARM,

        // ARM64
        ARM64,

        // WebAssembly
        WASM,

        // S390x
        S390X,

        // LongAarch64
        LOONGARCH64,

        // 32-bit ARMv6
        ARMV6,

        // PowerPC 64-bit (little-endian)
        PPC64LE,

        // Who knows
        Unknown
    }

    /// <summary>
    /// Structure that contain information about the system on which we are running
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_INFO
    {
        // This is a union of a DWORD and a struct containing 2 WORDs.
        internal ushort wProcessorArchitecture;
        internal ushort wReserved;

        internal uint dwPageSize;
        internal IntPtr lpMinimumApplicationAddress;
        internal IntPtr lpMaximumApplicationAddress;
        internal IntPtr dwActiveProcessorMask;
        internal uint dwNumberOfProcessors;
        internal uint dwProcessorType;
        internal uint dwAllocationGranularity;
        internal ushort wProcessorLevel;
        internal ushort wProcessorRevision;
    }

    /// <summary>
    /// Contains information about a file or directory; used by GetFileAttributesEx.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct WIN32_FILE_ATTRIBUTE_DATA
    {
        internal int fileAttributes;
        internal uint ftCreationTimeLow;
        internal uint ftCreationTimeHigh;
        internal uint ftLastAccessTimeLow;
        internal uint ftLastAccessTimeHigh;
        internal uint ftLastWriteTimeLow;
        internal uint ftLastWriteTimeHigh;
        internal uint fileSizeHigh;
        internal uint fileSizeLow;
    }

    private class SystemInformationData
    {
        /// <summary>
        /// Architecture as far as the current process is concerned.
        /// It's x86 in wow64 (native architecture is x64 in that case).
        /// Otherwise it's the same as the native architecture.
        /// </summary>
        public readonly ProcessorArchitectures ProcessorArchitectureType;

        /// <summary>
        /// Convert SYSTEM_INFO architecture values to the internal enum
        /// </summary>
        /// <param name="arch"></param>
        /// <returns></returns>
        private static ProcessorArchitectures ConvertSystemArchitecture(ushort arch)
        {
            return arch switch
            {
                PROCESSOR_ARCHITECTURE_INTEL => ProcessorArchitectures.X86,
                PROCESSOR_ARCHITECTURE_AMD64 => ProcessorArchitectures.X64,
                PROCESSOR_ARCHITECTURE_ARM => ProcessorArchitectures.ARM,
                PROCESSOR_ARCHITECTURE_IA64 => ProcessorArchitectures.IA64,
                PROCESSOR_ARCHITECTURE_ARM64 => ProcessorArchitectures.ARM64,
                _ => ProcessorArchitectures.Unknown,
            };
        }

        /// <summary>
        /// Read system info values
        /// </summary>
        public SystemInformationData()
        {
            var systemInfo = new SYSTEM_INFO();

            GetSystemInfo(ref systemInfo);
            ProcessorArchitectureType = ConvertSystemArchitecture(systemInfo.wProcessorArchitecture);
        }
    }

    public static int GetLogicalCoreCount()
    {
        int numberOfCpus = Environment.ProcessorCount;

        // .NET on Windows returns a core count limited to the current NUMA node
        //     https://github.com/dotnet/runtime/issues/29686
        // so always double-check it.
        var result = GetLogicalCoreCountOnWindows();
        if (result != -1)
        {
            numberOfCpus = result;
        }

        return numberOfCpus;
    }

    /// <summary>
    /// Get the exact physical core count on Windows
    /// Useful for getting the exact core count in 32 bits processes,
    /// as Environment.ProcessorCount has a 32-core limit in that case.
    /// https://github.com/dotnet/runtime/blob/221ad5b728f93489655df290c1ea52956ad8f51c/src/libraries/System.Runtime.Extensions/src/System/Environment.Windows.cs#L171-L210
    /// </summary>
    private static unsafe int GetLogicalCoreCountOnWindows()
    {
        uint len = 0;
        const int ERROR_INSUFFICIENT_BUFFER = 122;

        if (!GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore, IntPtr.Zero, ref len) &&
            Marshal.GetLastWin32Error() == ERROR_INSUFFICIENT_BUFFER)
        {
            // Allocate that much space
            var buffer = new byte[len];
            fixed (byte* bufferPtr = buffer)
            {
                // Call GetLogicalProcessorInformationEx with the allocated buffer
                if (GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore, (IntPtr)bufferPtr, ref len))
                {
                    // Walk each SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX in the buffer, where the Size of each dictates how
                    // much space it's consuming.  For each group relation, count the number of active processors in each of its group infos.
                    int processorCount = 0;
                    byte* ptr = bufferPtr;
                    byte* endPtr = bufferPtr + len;
                    while (ptr < endPtr)
                    {
                        var current = (SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX*)ptr;
                        if (current->Relationship == LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore)
                        {
                            // Flags is 0 if the core has a single logical proc, LTP_PC_SMT if more than one
                            // for now, assume "more than 1" == 2, as it has historically been for hyperthreading
                            processorCount += (current->Processor.Flags == 0) ? 1 : 2;
                        }

                        ptr += current->Size;
                    }

                    return processorCount;
                }
            }
        }

        return -1;
    }

    internal static bool HasMaxPath => MaxPath == MAX_PATH;

    /// <summary>
    /// Gets the max path limit of the current OS.
    /// </summary>
    internal static int MaxPath
    {
        get
        {
            if (!IsMaxPathSet)
            {
                SetMaxPath();
            }

            return _maxPath;
        }
    }

    /// <summary>
    /// Cached value for MaxPath.
    /// </summary>
    private static int _maxPath;

    private static bool IsMaxPathSet { get; set; }

    private static readonly LockType MaxPathLock = new LockType();

    private static void SetMaxPath()
    {
        lock (MaxPathLock)
        {
            if (!IsMaxPathSet)
            {
                bool isMaxPathRestricted = Traits.Instance.EscapeHatches.DisableLongPaths || IsMaxPathLegacyWindows();
                _maxPath = isMaxPathRestricted ? MAX_PATH : int.MaxValue;
                IsMaxPathSet = true;
            }
        }
    }

    private enum LongPathsStatus
    {
        /// <summary>
        ///  The registry key is set to 0 or does not exist.
        /// </summary>
        Disabled,

        /// <summary>
        /// The registry key does not exist.
        /// </summary>
        Missing,

        /// <summary>
        /// The registry key is set to 1.
        /// </summary>
        Enabled,

        /// <summary>
        /// Not on Windows.
        /// </summary>
        NotApplicable,
    }

    private static LongPathsStatus IsLongPathsEnabled()
    {
        try
        {
            return IsLongPathsEnabledRegistry();
        }
        catch
        {
            return LongPathsStatus.Disabled;
        }
    }

    private static bool IsMaxPathLegacyWindows()
    {
        var longPathsStatus = IsLongPathsEnabled();
        return longPathsStatus == LongPathsStatus.Disabled || longPathsStatus == LongPathsStatus.Missing;
    }

    private static LongPathsStatus IsLongPathsEnabledRegistry()
    {
        using (RegistryKey fileSystemKey = Registry.LocalMachine.OpenSubKey(WINDOWS_FILE_SYSTEM_REGISTRY_KEY))
        {
            object longPathsEnabledValue = fileSystemKey?.GetValue(WINDOWS_LONG_PATHS_ENABLED_VALUE_NAME, -1);
            if (fileSystemKey != null && Convert.ToInt32(longPathsEnabledValue) == -1)
            {
                return LongPathsStatus.Missing;
            }
            else if (fileSystemKey != null && Convert.ToInt32(longPathsEnabledValue) == 1)
            {
                return LongPathsStatus.Enabled;
            }
            else
            {
                return LongPathsStatus.Disabled;
            }
        }
    }

    /// <summary>
    /// System information, initialized when required.
    /// </summary>
    /// <remarks>
    /// Initially implemented as <see cref="Lazy{SystemInformationData}"/>, but
    /// that's .NET 4+, and this is used in MSBuildTaskHost.
    /// </remarks>
    private static SystemInformationData SystemInformation
    {
        get
        {
            if (!_systemInformationInitialized)
            {
                lock (SystemInformationLock)
                {
                    if (!_systemInformationInitialized)
                    {
                        _systemInformation = new SystemInformationData();
                        _systemInformationInitialized = true;
                    }
                }
            }
            return _systemInformation;
        }
    }

    private static SystemInformationData _systemInformation;
    private static bool _systemInformationInitialized;
    private static readonly LockType SystemInformationLock = new LockType();

    /// <summary>
    /// Architecture getter
    /// </summary>
    internal static ProcessorArchitectures ProcessorArchitecture => SystemInformation.ProcessorArchitectureType;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void GetSystemInfo(ref SYSTEM_INFO lpSystemInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP RelationshipType, IntPtr Buffer, ref uint ReturnedLength);

    /// <summary>
    /// Given an error code, converts it to an HRESULT and throws the appropriate exception.
    /// </summary>
    /// <param name="errorCode"></param>
    private static void ThrowExceptionForErrorCode(int errorCode)
    {
        // See ndp\clr\src\bcl\system\io\__error.cs for this code as it appears in the CLR.

        // Something really bad went wrong with the call
        // translate the error into an exception

        // Convert the errorcode into an HRESULT (See MakeHRFromErrorCode in Win32Native.cs in
        // ndp\clr\src\bcl\microsoft\win32)
        errorCode = unchecked(((int)0x80070000) | errorCode);

        // Throw an exception as best we can
        Marshal.ThrowExceptionForHR(errorCode);
    }

    /// <summary>
    ///  Internal, optimized GetCurrentDirectory implementation that simply delegates to the native method.
    /// </summary>
    internal static unsafe string GetCurrentDirectory()
    {
        int bufferSize = GetCurrentDirectoryWin32(nBufferLength: 0, lpBuffer: null);

        char* buffer = stackalloc char[bufferSize];
        int length = GetCurrentDirectoryWin32(bufferSize, buffer);

        return new string(buffer, startIndex: 0, length);
    }

    private static unsafe int GetCurrentDirectoryWin32(int nBufferLength, char* lpBuffer)
    {
        int pathLength = GetCurrentDirectory(nBufferLength, lpBuffer);
        VerifyThrowWin32Result(pathLength);
        return pathLength;
    }

    internal static unsafe string GetFullPath(string path)
    {
        char* buffer = stackalloc char[MAX_PATH];
        int fullPathLength = GetFullPathWin32(path, MAX_PATH, buffer, IntPtr.Zero);

        // if user is using long paths we could need to allocate a larger buffer
        if (fullPathLength > MAX_PATH)
        {
            char* newBuffer = stackalloc char[fullPathLength];
            fullPathLength = GetFullPathWin32(path, fullPathLength, newBuffer, IntPtr.Zero);

            buffer = newBuffer;
        }

        // Avoid creating new strings unnecessarily
        return AreStringsEqual(buffer, fullPathLength, path) ? path : new string(buffer, startIndex: 0, length: fullPathLength);
    }

    private static unsafe int GetFullPathWin32(string target, int bufferLength, char* buffer, IntPtr mustBeZero)
    {
        int pathLength = GetFullPathName(target, bufferLength, buffer, mustBeZero);
        VerifyThrowWin32Result(pathLength);
        return pathLength;
    }

    /// <summary>
    /// Compare an unsafe char buffer with a <see cref="System.String"/> to see if their contents are identical.
    /// </summary>
    /// <param name="buffer">The beginning of the char buffer.</param>
    /// <param name="len">The length of the buffer.</param>
    /// <param name="s">The string.</param>
    /// <returns>True only if the contents of <paramref name="s"/> and the first <paramref name="len"/> characters in <paramref name="buffer"/> are identical.</returns>
    private static unsafe bool AreStringsEqual(char* buffer, int len, string s)
    {
        if (len != s.Length)
        {
            return false;
        }

        foreach (char ch in s)
        {
            if (ch != *buffer++)
            {
                return false;
            }
        }

        return true;
    }

    private static void VerifyThrowWin32Result(int result)
    {
        bool isError = result == 0;
        if (isError)
        {
            int code = Marshal.GetLastWin32Error();
            ThrowExceptionForErrorCode(code);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileAttributesEx(String name, int fileInfoLevel, ref WIN32_FILE_ATTRIBUTE_DATA lpFileInformation);

    [SuppressMessage("Microsoft.Usage", "CA2205:UseManagedEquivalentsOfWin32Api", Justification = "Using unmanaged equivalent for performance reasons")]
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern unsafe int GetCurrentDirectory(int nBufferLength, char* lpBuffer);

    [SuppressMessage("Microsoft.Usage", "CA2205:UseManagedEquivalentsOfWin32Api", Justification = "Using unmanaged equivalent for performance reasons")]
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "SetCurrentDirectory")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCurrentDirectoryWindows(string path);

    internal static bool SetCurrentDirectory(string path)
        => SetCurrentDirectoryWindows(path);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern unsafe int GetFullPathName(string target, int bufferLength, char* buffer, IntPtr mustBeZero);

    public static bool DirectoryExists(string fullPath)
    {
        WIN32_FILE_ATTRIBUTE_DATA data = new WIN32_FILE_ATTRIBUTE_DATA();
        bool success = GetFileAttributesEx(fullPath, 0, ref data);
        return success && (data.fileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
    }

    public static bool FileExists(string fullPath)
    {
        WIN32_FILE_ATTRIBUTE_DATA data = new WIN32_FILE_ATTRIBUTE_DATA();
        bool success = GetFileAttributesEx(fullPath, 0, ref data);
        return success && (data.fileAttributes & FILE_ATTRIBUTE_DIRECTORY) == 0;
    }

    public static bool FileOrDirectoryExists(string path)
    {
        WIN32_FILE_ATTRIBUTE_DATA data = new WIN32_FILE_ATTRIBUTE_DATA();
        return GetFileAttributesEx(path, 0, ref data);
    }
}
