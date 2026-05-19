// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Build.Framework.Logging;
using Microsoft.Build.Shared;
using Microsoft.Win32;
#if FEATURE_WINDOWSINTEROP
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Build.Utilities;
using Microsoft.Win32.SafeHandles;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Console;
using Windows.Win32.System.Diagnostics.Debug;
using Windows.Win32.System.SystemInformation;
using Windows.Win32.System.Threading;
using Wdk = Windows.Wdk;
using WdkThreading = Windows.Wdk.System.Threading;
#endif

#nullable disable

namespace Microsoft.Build.Framework;

internal static class NativeMethods
{
    /// <summary>
    /// Default buffer size to use when dealing with the Windows API.
    /// </summary>
    internal const int MAX_PATH = 260;

    private const string WINDOWS_FILE_SYSTEM_REGISTRY_KEY = @"SYSTEM\CurrentControlSet\Control\FileSystem";
    private const string WINDOWS_LONG_PATHS_ENABLED_VALUE_NAME = "LongPathsEnabled";

    private const string WINDOWS_SAC_REGISTRY_KEY = @"SYSTEM\CurrentControlSet\Control\CI\Policy";
    private const string WINDOWS_SAC_VALUE_NAME = "VerifiedAndReputablePolicyState";

    internal static DateTime MinFileDate { get; } = DateTime.FromFileTimeUtc(0);

    /// <summary>
    /// Processor architecture values
    /// </summary>
    internal enum ProcessorArchitectures
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
    /// Wrap the intptr returned by OpenProcess in a safe handle.
    /// </summary>
#if FEATURE_WINDOWSINTEROP
    internal class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeProcessHandle(IntPtr handle) : base(true)
        {
            SetHandle(handle);
        }

        private SafeProcessHandle() : base(true)
        {
        }

        [SupportedOSPlatform("windows6.1")]
        protected override bool ReleaseHandle()
        {
            return PInvoke.CloseHandle((HANDLE)handle);
        }
    }
#endif

    private class SystemInformationData
    {
        /// <summary>
        /// Architecture as far as the current process is concerned.
        /// It's x86 in wow64 (native architecture is x64 in that case).
        /// Otherwise it's the same as the native architecture.
        /// </summary>
        public readonly ProcessorArchitectures ProcessorArchitectureType;

        /// <summary>
        /// Actual architecture of the system.
        /// </summary>
        public readonly ProcessorArchitectures ProcessorArchitectureTypeNative;

#if FEATURE_WINDOWSINTEROP
        /// <summary>
        /// Convert SYSTEM_INFO architecture values to the internal enum
        /// </summary>
        /// <param name="arch"></param>
        /// <returns></returns>
        private static ProcessorArchitectures ConvertSystemArchitecture(PROCESSOR_ARCHITECTURE arch)
        {
            return arch switch
            {
                PROCESSOR_ARCHITECTURE.PROCESSOR_ARCHITECTURE_INTEL => ProcessorArchitectures.X86,
                PROCESSOR_ARCHITECTURE.PROCESSOR_ARCHITECTURE_AMD64 => ProcessorArchitectures.X64,
                PROCESSOR_ARCHITECTURE.PROCESSOR_ARCHITECTURE_ARM => ProcessorArchitectures.ARM,
                PROCESSOR_ARCHITECTURE.PROCESSOR_ARCHITECTURE_IA64 => ProcessorArchitectures.IA64,
                PROCESSOR_ARCHITECTURE.PROCESSOR_ARCHITECTURE_ARM64 => ProcessorArchitectures.ARM64,
                _ => ProcessorArchitectures.Unknown,
            };
        }
#endif

        /// <summary>
        /// Read system info values
        /// </summary>
        public SystemInformationData()
        {
            ProcessorArchitectureType = ProcessorArchitectures.Unknown;
            ProcessorArchitectureTypeNative = ProcessorArchitectures.Unknown;

#if FEATURE_WINDOWSINTEROP
            if (IsWindows)
            {
                SYSTEM_INFO systemInfo;
                PInvoke.GetSystemInfo(out systemInfo);
                ProcessorArchitectureType = ConvertSystemArchitecture(systemInfo.Anonymous.Anonymous.wProcessorArchitecture);

                PInvoke.GetNativeSystemInfo(out systemInfo);
                ProcessorArchitectureTypeNative = ConvertSystemArchitecture(systemInfo.Anonymous.Anonymous.wProcessorArchitecture);
            }
            else
#endif
            {
                ProcessorArchitectures processorArchitecture = ProcessorArchitectures.Unknown;

#if NET || NETSTANDARD1_1_OR_GREATER
                // Get the architecture from the runtime.
                processorArchitecture = RuntimeInformation.OSArchitecture switch
                {
                    Architecture.Arm => ProcessorArchitectures.ARM,
                    Architecture.Arm64 => ProcessorArchitectures.ARM64,
                    Architecture.X64 => ProcessorArchitectures.X64,
                    Architecture.X86 => ProcessorArchitectures.X86,
#if NET
                    Architecture.Wasm => ProcessorArchitectures.WASM,
                    Architecture.S390x => ProcessorArchitectures.S390X,
                    Architecture.LoongArch64 => ProcessorArchitectures.LOONGARCH64,
                    Architecture.Armv6 => ProcessorArchitectures.ARMV6,
                    Architecture.Ppc64le => ProcessorArchitectures.PPC64LE,
#endif
                    _ => ProcessorArchitectures.Unknown,
                };

#endif

                ProcessorArchitectureTypeNative = ProcessorArchitectureType = processorArchitecture;
            }
        }
    }

    public static int GetLogicalCoreCount()
    {
        int numberOfCpus = Environment.ProcessorCount;
        // .NET on Windows returns a core count limited to the current NUMA node
        //     https://github.com/dotnet/runtime/issues/29686
        // so always double-check it.
#if FEATURE_WINDOWSINTEROP
        if (IsWindows)
        {
            var result = GetLogicalCoreCountOnWindows();
            if (result != -1)
            {
                numberOfCpus = result;
            }
        }
#endif

        return numberOfCpus;
    }

#if FEATURE_WINDOWSINTEROP
    /// <summary>
    /// Get the exact physical core count on Windows
    /// Useful for getting the exact core count in 32 bits processes,
    /// as Environment.ProcessorCount has a 32-core limit in that case.
    /// https://github.com/dotnet/runtime/blob/221ad5b728f93489655df290c1ea52956ad8f51c/src/libraries/System.Runtime.Extensions/src/System/Environment.Windows.cs#L171-L210
    /// </summary>
    [SupportedOSPlatform("windows6.1")]
    private static unsafe int GetLogicalCoreCountOnWindows()
    {
        uint len = 0;
        const int ERROR_INSUFFICIENT_BUFFER = 122;

        if (!PInvoke.GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore, null, &len) &&
            Marshal.GetLastWin32Error() == ERROR_INSUFFICIENT_BUFFER)
        {
            using BufferScope<byte> buffer = new((int)len);
            fixed (byte* bufferPtr = buffer)
            {
                if (PInvoke.GetLogicalProcessorInformationEx(
                    LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore,
                    (SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX*)bufferPtr,
                    &len))
                {
                    int processorCount = 0;
                    byte* ptr = bufferPtr;
                    byte* endPtr = bufferPtr + len;
                    while (ptr < endPtr)
                    {
                        var current = (SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX*)ptr;
                        if (current->Relationship == LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore)
                        {
                            processorCount += (current->Anonymous.Processor.Flags == 0) ? 1 : 2;
                        }
                        ptr += current->Size;
                    }
                    return processorCount;
                }
            }
        }

        return -1;
    }
#endif

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

    internal enum LongPathsStatus
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

    internal static LongPathsStatus IsLongPathsEnabled()
    {
        if (!IsWindows)
        {
            return LongPathsStatus.NotApplicable;
        }

        try
        {
            return IsLongPathsEnabledRegistry();
        }
        catch
        {
            return LongPathsStatus.Disabled;
        }
    }

    internal static bool IsMaxPathLegacyWindows()
    {
        var longPathsStatus = IsLongPathsEnabled();
        return longPathsStatus == LongPathsStatus.Disabled || longPathsStatus == LongPathsStatus.Missing;
    }

    [SupportedOSPlatform("windows6.1")]
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

    private static SAC_State? s_sacState;

    /// <summary>
    /// Get from registry state of the Smart App Control (SAC) on the system.
    /// </summary>
    /// <returns>State of SAC</returns>
    internal static SAC_State GetSACState()
    {
        s_sacState ??= GetSACStateInternal();

        return s_sacState.Value;
    }

    internal static SAC_State GetSACStateInternal()
    {
        if (IsWindows)
        {
            try
            {
                return GetSACStateRegistry();
            }
            catch
            {
                return SAC_State.Missing;
            }
        }

        return SAC_State.NotApplicable;
    }

    [SupportedOSPlatform("windows6.1")]
    private static SAC_State GetSACStateRegistry()
    {
        SAC_State SACState = SAC_State.Missing;

        using (RegistryKey policyKey = Registry.LocalMachine.OpenSubKey(WINDOWS_SAC_REGISTRY_KEY))
        {
            if (policyKey != null)
            {
                object sacValue = policyKey.GetValue(WINDOWS_SAC_VALUE_NAME, -1);
                SACState = Convert.ToInt32(sacValue) switch
                {
                    0 => SAC_State.Off,
                    1 => SAC_State.Enforcement,
                    2 => SAC_State.Evaluation,
                    _ => SAC_State.Missing,
                };
            }
        }

        return SACState;
    }

    /// <summary>
    /// State of Smart App Control (SAC) on the system.
    /// </summary>
    internal enum SAC_State
    {
        /// <summary>
        /// 1: SAC is on and enforcing.
        /// </summary>
        Enforcement,
        /// <summary>
        /// 2: SAC is on and in evaluation mode.
        /// </summary>
        Evaluation,
        /// <summary>
        /// 0: SAC is off.
        /// </summary>
        Off,
        /// <summary>
        /// The registry key is missing.
        /// </summary>
        Missing,
        /// <summary>
        /// Not on Windows.
        /// </summary>
        NotApplicable
    }

    /// <summary>
    /// Cached value for IsUnixLike (this method is called frequently during evaluation).
    /// </summary>
    private static readonly bool s_isUnixLike = IsLinux || IsOSX || IsBSD || IsHaiku;

    /// <summary>
    /// Gets a flag indicating if we are running under a Unix-like system (Mac, Linux, etc.)
    /// </summary>
    [UnsupportedOSPlatformGuard("windows")]
    internal static bool IsUnixLike
    {
        get { return s_isUnixLike; }
    }

    /// <summary>
    /// Gets a flag indicating if we are running under Linux
    /// </summary>
    [SupportedOSPlatformGuard("linux")]
    internal static bool IsLinux
    {
        get { return RuntimeInformation.IsOSPlatform(OSPlatform.Linux); }
    }

    /// <summary>
    /// Gets a flag indicating if we are running under flavor of BSD (NetBSD, OpenBSD, FreeBSD)
    /// </summary>
    [SupportedOSPlatformGuard("freebsd")]
    internal static bool IsBSD
    {
        get
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")) ||
                   RuntimeInformation.IsOSPlatform(OSPlatform.Create("NETBSD")) ||
                   RuntimeInformation.IsOSPlatform(OSPlatform.Create("OPENBSD"));
        }
    }

    /// <summary>
    /// Gets a flag indicating if we are running under Haiku
    /// </summary>
    [SupportedOSPlatformGuard("haiku")]
    internal static bool IsHaiku
    {
        get
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Create("HAIKU"));
        }
    }

    private static bool? _isWindows;

    /// <summary>
    /// Gets a flag indicating if we are running under some version of Windows
    /// </summary>
    [SupportedOSPlatformGuard("windows6.1")]
    internal static bool IsWindows
    {
        get
        {
            _isWindows ??= RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            return _isWindows.Value;
        }
    }

    private static bool? _isOSX;

    /// <summary>
    /// Gets a flag indicating if we are running under Mac OSX
    /// </summary>
    [SupportedOSPlatformGuard("macos")]
    internal static bool IsOSX
    {
        get
        {
            _isOSX ??= RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            return _isOSX.Value;
        }
    }

    /// <summary>
    /// Gets a string for the current OS. This matches the OS env variable
    /// for Windows (Windows_NT).
    /// </summary>
    internal static string OSName
    {
        get { return IsWindows ? "Windows_NT" : "Unix"; }
    }

    /// <summary>
    /// Framework named as presented to users (for example in version info).
    /// </summary>
    internal static string FrameworkName
    {
        get
        {
#if RUNTIME_TYPE_NETCORE
            const string frameworkName = ".NET";
#else
            const string frameworkName = ".NET Framework";
#endif
            return frameworkName;
        }
    }

    /// <summary>
    /// OS name that can be used for the msbuildExtensionsPathSearchPaths element
    /// for a toolset
    /// </summary>
    internal static string GetOSNameForExtensionsPath()
    {
        return IsOSX ? "osx" : IsUnixLike ? "unix" : "windows";
    }

    internal static bool OSUsesCaseSensitivePaths
    {
        get { return IsLinux; }
    }

    /// <summary>
    /// Determines whether the file system is case sensitive by creating a test file.
    /// Copied from FileUtilities.GetIsFileSystemCaseSensitive() in Shared.
    /// FIXME: shared code should be consolidated to Framework https://github.com/dotnet/msbuild/issues/6984
    /// </summary>
    private static readonly Lazy<bool> s_isFileSystemCaseSensitive = new(() =>
    {
        try
        {
            string pathWithUpperCase = Path.Combine(Path.GetTempPath(), $"INTCASESENSITIVETEST{Guid.NewGuid():N}");
            using (new FileStream(pathWithUpperCase, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 0x1000, FileOptions.DeleteOnClose))
            {
                return !File.Exists(pathWithUpperCase.ToLowerInvariant());
            }
        }
        catch
        {
            return OSUsesCaseSensitivePaths;
        }
    });

    internal static bool IsFileSystemCaseSensitive => s_isFileSystemCaseSensitive.Value;

    /// <summary>
    /// The base directory for all framework paths in Mono
    /// </summary>
    private static string s_frameworkBasePath;

    /// <summary>
    /// The directory of the current framework
    /// </summary>
    private static string s_frameworkCurrentPath;

    /// <summary>
    /// Gets the currently running framework path
    /// </summary>
    internal static string FrameworkCurrentPath
    {
        get
        {
            if (s_frameworkCurrentPath == null)
            {
                var baseTypeLocation = AssemblyUtilities.GetAssemblyLocation(typeof(string).Assembly);

                s_frameworkCurrentPath =
                    Path.GetDirectoryName(baseTypeLocation)
                    ?? string.Empty;
            }

            return s_frameworkCurrentPath;
        }
    }

    /// <summary>
    /// Gets the base directory of all Mono frameworks
    /// </summary>
    internal static string FrameworkBasePath
    {
        get
        {
            if (s_frameworkBasePath == null)
            {
                var dir = FrameworkCurrentPath;
                if (dir != string.Empty)
                {
                    dir = Path.GetDirectoryName(dir);
                }

                s_frameworkBasePath = dir ?? string.Empty;
            }

            return s_frameworkBasePath;
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

    /// <summary>
    /// Native architecture getter
    /// </summary>
    internal static ProcessorArchitectures ProcessorArchitectureNative => SystemInformation.ProcessorArchitectureTypeNative;

    /// <summary>
    /// Get the last write time of the fullpath to a directory. If the pointed path is not a directory, or
    /// if the directory does not exist, then false is returned and fileModifiedTimeUtc is set DateTime.MinValue.
    /// </summary>
    /// <param name="fullPath">Full path to the file in the filesystem</param>
    /// <param name="fileModifiedTimeUtc">The UTC last write time for the directory</param>
    internal static bool GetLastWriteDirectoryUtcTime(string fullPath, out DateTime fileModifiedTimeUtc)
    {
#if FEATURE_WINDOWSINTEROP
        if (IsWindows)
        {
            if (PInvoke.GetFileAttributesEx(fullPath, out WIN32_FILE_ATTRIBUTE_DATA data)
                && ((FILE_FLAGS_AND_ATTRIBUTES)data.dwFileAttributes & FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_DIRECTORY) != 0)
            {
                fileModifiedTimeUtc = DateTime.FromFileTimeUtc(data.ftLastWriteTime.ToLong());
                return true;
            }

            fileModifiedTimeUtc = DateTime.MinValue;
            return false;
        }
#endif

        if (Directory.Exists(fullPath))
        {
            fileModifiedTimeUtc = Directory.GetLastWriteTimeUtc(fullPath);
            return true;
        }
        else
        {
            fileModifiedTimeUtc = DateTime.MinValue;
            return false;
        }
    }

    /// <summary>
    /// Takes the path and returns the short path
    /// </summary>
    internal static string GetShortFilePath(string path)
    {
        if (!IsWindows)
        {
            return path;
        }

#if FEATURE_WINDOWSINTEROP
        if (path != null)
        {
            using BufferScope<char> buffer = new(stackalloc char[(int)PInvoke.MAX_PATH]);
            int length = (int)PInvoke.GetShortPathName(path, buffer.AsSpan());
            WIN32_ERROR errorCode = (WIN32_ERROR)Marshal.GetLastWin32Error();

            if (length > buffer.Length)
            {
                buffer.EnsureCapacity(length);
                length = (int)PInvoke.GetShortPathName(path, buffer.AsSpan());
                errorCode = (WIN32_ERROR)Marshal.GetLastWin32Error();
            }

            if (length > 0)
            {
                path = buffer.Slice(0, length).ToString();
            }

            if (length == 0 && errorCode != WIN32_ERROR.ERROR_SUCCESS)
            {
                ((HRESULT)errorCode).ThrowOnFailure();
            }
        }
#endif

        return path;
    }

    /// <summary>
    /// Takes the path and returns a full path
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    [SupportedOSPlatform("windows6.1")]
    internal static string GetLongFilePath(string path)
    {
        if (IsUnixLike)
        {
            return path;
        }

#if FEATURE_WINDOWSINTEROP
        if (path != null)
        {
            using BufferScope<char> buffer = new(stackalloc char[(int)PInvoke.MAX_PATH]);
            int length = (int)PInvoke.GetLongPathName(path, buffer.AsSpan());
            WIN32_ERROR errorCode = (WIN32_ERROR)Marshal.GetLastWin32Error();

            if (length > buffer.Length)
            {
                buffer.EnsureCapacity(length);
                length = (int)PInvoke.GetLongPathName(path, buffer.AsSpan());
                errorCode = (WIN32_ERROR)Marshal.GetLastWin32Error();
            }

            if (length > 0)
            {
                path = buffer.Slice(0, length).ToString();
            }

            if (length == 0 && errorCode != WIN32_ERROR.ERROR_SUCCESS)
            {
                ((HRESULT)errorCode).ThrowOnFailure();
            }
        }
#endif

        return path;
    }

#if FEATURE_WINDOWSINTEROP
    /// <summary>
    /// Retrieves the current global memory status.
    /// </summary>
    internal static unsafe bool TryGetMemoryStatus(out MEMORYSTATUSEX memoryStatus)
    {
        memoryStatus = default;

        if (IsWindows)
        {
            memoryStatus.dwLength = (uint)sizeof(MEMORYSTATUSEX);
            return PInvoke.GlobalMemoryStatusEx(ref memoryStatus);
        }

        return false;
    }
#endif

    internal static bool MakeSymbolicLink(string newFileName, string existingFileName, ref string errorMessage)
    {
        bool symbolicLinkCreated;
#if FEATURE_WINDOWSINTEROP
        if (IsWindows)
        {
            Version osVersion = Environment.OSVersion.Version;
            SYMBOLIC_LINK_FLAGS flags = 0; // File = 0 (no named constant)
            if (osVersion.Major >= 11 || (osVersion.Major == 10 && osVersion.Build >= 14972))
            {
                flags |= SYMBOLIC_LINK_FLAGS.SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE;
            }

            symbolicLinkCreated = PInvoke.CreateSymbolicLink(newFileName, existingFileName, flags);
            errorMessage = symbolicLinkCreated ? null : Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()).Message;
        }
        else
#endif
        {
            symbolicLinkCreated = symlink(existingFileName, newFileName) == 0;
            errorMessage = symbolicLinkCreated ? null : Marshal.GetLastWin32Error().ToString();
        }

        return symbolicLinkCreated;
    }

    /// <summary>
    /// Get the last write time of the fullpath to the file.
    /// </summary>
    /// <param name="fullPath">Full path to the file in the filesystem</param>
    /// <returns>The last write time of the file, or DateTime.MinValue if the file does not exist.</returns>
    /// <remarks>
    /// This method should be accurate for regular files and symlinks, but can report incorrect data
    /// if the file's content was modified by writing to it through a different link, unless
    /// MSBUILDALWAYSCHECKCONTENTTIMESTAMP=1.
    /// </remarks>
    internal static DateTime GetLastWriteFileUtcTime(string fullPath)
    {
        if (Traits.Instance.EscapeHatches.AlwaysDoImmutableFilesUpToDateCheck)
        {
            return LastWriteFileUtcTime(fullPath);
        }

        bool isNonModifiable = FileClassifier.Shared.IsNonModifiable(fullPath);
        if (isNonModifiable)
        {
            if (ImmutableFilesTimestampCache.Shared.TryGetValue(fullPath, out DateTime modifiedAt))
            {
                return modifiedAt;
            }
        }

        DateTime modifiedTime = LastWriteFileUtcTime(fullPath);

        if (isNonModifiable && modifiedTime != DateTime.MinValue)
        {
            ImmutableFilesTimestampCache.Shared.TryAdd(fullPath, modifiedTime);
        }

        return modifiedTime;

        DateTime LastWriteFileUtcTime(string path)
        {
            DateTime fileModifiedTime = DateTime.MinValue;

#if FEATURE_WINDOWSINTEROP
            if (IsWindows)
            {
                if (Traits.Instance.EscapeHatches.AlwaysUseContentTimestamp)
                {
                    return GetContentLastWriteFileUtcTime(path);
                }

                bool success = PInvoke.GetFileAttributesEx(path, out WIN32_FILE_ATTRIBUTE_DATA data);

                if (success && ((FILE_FLAGS_AND_ATTRIBUTES)data.dwFileAttributes & FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_DIRECTORY) == 0)
                {
                    fileModifiedTime = DateTime.FromFileTimeUtc(data.ftLastWriteTime.ToLong());

                    // If file is a symlink _and_ we're not instructed to do the wrong thing, get a more accurate timestamp.
                    if (((FILE_FLAGS_AND_ATTRIBUTES)data.dwFileAttributes & FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_REPARSE_POINT) != 0
                        && !Traits.Instance.EscapeHatches.UseSymlinkTimeInsteadOfTargetTime)
                    {
                        fileModifiedTime = GetContentLastWriteFileUtcTime(path);
                    }
                }

                return fileModifiedTime;
            }
#endif

            return File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : DateTime.MinValue;
        }
    }

#if FEATURE_WINDOWSINTEROP
    /// <summary>
    /// Get the SafeFileHandle for a file, while skipping reparse points (going directly to target file).
    /// </summary>
    /// <param name="fullPath">Full path to the file in the filesystem</param>
    /// <returns>the SafeFileHandle for a file (target file in case of symlinks)</returns>
    [SupportedOSPlatform("windows6.1")]
    private static unsafe SafeFileHandle OpenFileThroughSymlinks(string fullPath)
    {
        HANDLE h = PInvoke.CreateFile(
            fullPath,
            (uint)FILE_ACCESS_RIGHTS.FILE_GENERIC_READ,
            FILE_SHARE_MODE.FILE_SHARE_READ,
            null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL, /* No FILE_FLAG_OPEN_REPARSE_POINT; read through to content */
            HANDLE.Null);
        return new SafeFileHandle((IntPtr)h.Value, ownsHandle: true);
    }

    /// <summary>
    /// Get the last write time of the content pointed to by a file path.
    /// </summary>
    /// <param name="fullPath">Full path to the file in the filesystem</param>
    /// <returns>The last write time of the file, or DateTime.MinValue if the file does not exist.</returns>
    /// <remarks>
    /// This is the most accurate timestamp-extraction mechanism, but it is too slow to use all the time.
    /// See https://github.com/dotnet/msbuild/issues/2052.
    /// </remarks>
    [SupportedOSPlatform("windows6.1")]
    private static unsafe DateTime GetContentLastWriteFileUtcTime(string fullPath)
    {
        DateTime fileModifiedTime = DateTime.MinValue;

        using (SafeFileHandle handle = OpenFileThroughSymlinks(fullPath))
        {
            if (!handle.IsInvalid)
            {
                FILETIME ftCreationTime, ftLastAccessTime, ftLastWriteTime;
                if (PInvoke.GetFileTime((HANDLE)handle.DangerousGetHandle(), &ftCreationTime, &ftLastAccessTime, &ftLastWriteTime))
                {
                    long fileTime = ((long)(uint)ftLastWriteTime.dwHighDateTime) << 32 |
                                    (long)(uint)ftLastWriteTime.dwLowDateTime;
                    fileModifiedTime =
                        DateTime.FromFileTimeUtc(fileTime);
                }
            }
        }

        return fileModifiedTime;
    }
#endif

    /// <summary>
    /// Given an error code, converts it to an HRESULT and throws the appropriate exception.
    /// </summary>
    /// <param name="errorCode"></param>
    public static void ThrowExceptionForErrorCode(int errorCode)
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

#if FEATURE_WINDOWSINTEROP
    /// <summary>
    /// Kills the specified process by id and all of its children recursively.
    /// </summary>
    [SupportedOSPlatform("windows6.1")]
    internal static void KillTree(int processIdToKill)
    {
        // Note that GetProcessById does *NOT* internally hold on to the process handle.
        // Only when you create the process using the Process object
        // does the Process object retain the original handle.

        Process thisProcess;
        try
        {
            thisProcess = Process.GetProcessById(processIdToKill);
        }
        catch (ArgumentException)
        {
            // The process has already died for some reason.  So shrug and assume that any child processes
            // have all also either died or are in the process of doing so.
            return;
        }

        try
        {
            DateTime myStartTime = thisProcess.StartTime;

            // Grab the process handle.  We want to keep this open for the duration of the function so that
            // it cannot be reused while we are running.
            using (SafeProcessHandle hProcess = OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_INFORMATION, false, processIdToKill))
            {
                if (hProcess.IsInvalid)
                {
                    return;
                }

                try
                {
                    // Kill this process, so that no further children can be created.
                    thisProcess.Kill();
                }
                catch (Win32Exception e) when (e.NativeErrorCode == (int)WIN32_ERROR.ERROR_ACCESS_DENIED)
                {
                    // Access denied is potentially expected -- it happens when the process that
                    // we're attempting to kill is already dead.  So just ignore in that case.
                }

                // Now enumerate our children.  Children of this process are any process which has this process id as its parent
                // and which also started after this process did.
                List<KeyValuePair<int, SafeProcessHandle>> children = GetChildProcessIds(processIdToKill, myStartTime);

                try
                {
                    foreach (KeyValuePair<int, SafeProcessHandle> childProcessInfo in children)
                    {
                        KillTree(childProcessInfo.Key);
                    }
                }
                finally
                {
                    foreach (KeyValuePair<int, SafeProcessHandle> childProcessInfo in children)
                    {
                        childProcessInfo.Value.Dispose();
                    }
                }
            }
        }
        finally
        {
            thisProcess.Dispose();
        }
    }

    /// <summary>
    /// Returns the parent process id for the specified process.
    /// Returns zero if it cannot be gotten for some reason.
    /// </summary>
    [SupportedOSPlatform("windows6.1")]
    internal static int GetParentProcessId(int processId)
    {
        int ParentID = 0;
        if (IsUnixLike)
        {
            string line = null;

            try
            {
                // /proc/<processID>/stat returns a bunch of space separated fields. Get that string

                // TODO: this was
                // using (var r = FileUtilities.OpenRead("/proc/" + processId + "/stat"))
                // and could be again when FileUtilities moves to Framework

                using var fileStream = new FileStream($"/proc/{processId}/stat", FileMode.Open, FileAccess.Read);
                using StreamReader r = new(fileStream);

                line = r.ReadLine();
            }
            catch // Ignore errors since the process may have terminated
            {
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                // One of the fields is the process name. It may contain any characters, but since it's
                // in parenthesis, we can finds its end by looking for the last parenthesis. After that,
                // there comes a space, then the second fields separated by a space is the parent id.
                string[] statFields = line.Substring(line.LastIndexOf(')')).Split(MSBuildConstants.SpaceChar, 4);
                if (statFields.Length >= 3)
                {
                    ParentID = Int32.Parse(statFields[2]);
                }
            }
        }
        else
        {
            using SafeProcessHandle hProcess = OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_INFORMATION, false, processId);
            {
                if (!hProcess.IsInvalid)
                {
                    // UNDONE: NtQueryInformationProcess will fail if we are not elevated and other process is. Advice is to change to use ToolHelp32 API's
                    // For now just return zero and worst case we will not kill some children.
                    var pbi = default(PROCESS_BASIC_INFORMATION);
                    int pSize = 0;

                    if (0 == NtQueryInformationProcess(hProcess, ref pbi, ref pSize))
                    {
                        ParentID = (int)pbi.InheritedFromUniqueProcessId;
                    }
                }
            }
        }

        return ParentID;
    }

    /// <summary>
    /// Returns an array of all the immediate child processes by id.
    /// NOTE: The IntPtr in the tuple is the handle of the child process.  CloseHandle MUST be called on this.
    /// </summary>
    [SupportedOSPlatform("windows6.1")]
    internal static List<KeyValuePair<int, SafeProcessHandle>> GetChildProcessIds(int parentProcessId, DateTime parentStartTime)
    {
        List<KeyValuePair<int, SafeProcessHandle>> myChildren = new List<KeyValuePair<int, SafeProcessHandle>>();

        foreach (Process possibleChildProcess in Process.GetProcesses())
        {
            using (possibleChildProcess)
            {
                // Hold the child process handle open so that children cannot die and restart with a different parent after we've started looking at it.
                // This way, any handle we pass back is guaranteed to be one of our actual children.
#pragma warning disable CA2000 // Dispose objects before losing scope - caller must dispose returned handles
                SafeProcessHandle childHandle = OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_INFORMATION, false, possibleChildProcess.Id);
#pragma warning restore CA2000 // Dispose objects before losing scope
                {
                    if (childHandle.IsInvalid)
                    {
                        continue;
                    }

                    bool keepHandle = false;
                    try
                    {
                        if (possibleChildProcess.StartTime > parentStartTime)
                        {
                            int childParentProcessId = GetParentProcessId(possibleChildProcess.Id);
                            if (childParentProcessId != 0)
                            {
                                if (parentProcessId == childParentProcessId)
                                {
                                    // Add this one
                                    myChildren.Add(new KeyValuePair<int, SafeProcessHandle>(possibleChildProcess.Id, childHandle));
                                    keepHandle = true;
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (!keepHandle)
                        {
                            childHandle.Dispose();
                        }
                    }
                }
            }
        }

        return myChildren;
    }
#endif

    /// <summary>
    /// Internal, optimized GetCurrentDirectory implementation that simply delegates to the native method
    /// </summary>
    internal static string GetCurrentDirectory()
    {
#if FEATURE_LEGACY_GETCURRENTDIRECTORY
        if (IsWindows)
        {
            using BufferScope<char> buffer = new(stackalloc char[(int)PInvoke.MAX_PATH]);
            int pathLength = (int)PInvoke.GetCurrentDirectory(buffer);

            if (pathLength > buffer.Length)
            {
                buffer.EnsureCapacity(pathLength);
                pathLength = (int)PInvoke.GetCurrentDirectory(buffer);
            }

            if (pathLength != 0)
            {
                return buffer.Slice(0, pathLength).ToString();
            }

            HRESULT.FromLastError().ThrowOnFailure();
        }
#endif
        return Directory.GetCurrentDirectory();
    }

    internal static bool SetCurrentDirectory(string path)
    {
#if FEATURE_WINDOWSINTEROP
        if (IsWindows)
        {
            return PInvoke.SetCurrentDirectory(path);
        }
#endif

        // Make sure this does not throw
        try
        {
            Directory.SetCurrentDirectory(path);
        }
        catch
        {
        }

        return true;
    }

#if FEATURE_WINDOWSINTEROP
    [SupportedOSPlatform("windows6.1")]
    internal static unsafe string GetFullPath(string path)
    {
        using BufferScope<char> buffer = new(stackalloc char[(int)PInvoke.MAX_PATH]);
        int fullPathLength = (int)PInvoke.GetFullPathName(path, buffer, out _);

        // If user is using long paths we could need to allocate a larger buffer
        if (fullPathLength > buffer.Length)
        {
            buffer.EnsureCapacity(fullPathLength);
            fullPathLength = (int)PInvoke.GetFullPathName(path, buffer, out _);
        }

        if (fullPathLength == 0)
        {
            HRESULT.FromLastError().ThrowOnFailure();
        }

        // Avoid creating new strings unnecessarily
        ReadOnlySpan<char> result = buffer.AsSpan().Slice(0, fullPathLength);
        return result.SequenceEqual(path.AsSpan()) ? path : result.ToString();
    }

#endif

    internal static (bool acceptAnsiColorCodes, bool outputIsScreen, uint? originalConsoleMode) QueryIsScreenAndTryEnableAnsiColorCodes(bool useStandardError = false)
    {
        if (Console.IsOutputRedirected)
        {
            // There's no ANSI terminal support if console output is redirected.
            return (acceptAnsiColorCodes: false, outputIsScreen: false, originalConsoleMode: null);
        }

        if (Console.BufferHeight == 0 || Console.BufferWidth == 0)
        {
            // The current console doesn't have a valid buffer size, which means it is not a real console. let's default to not using TL
            // in those scenarios.
            return (acceptAnsiColorCodes: false, outputIsScreen: false, originalConsoleMode: null);
        }

        bool acceptAnsiColorCodes = false;
        bool outputIsScreen = false;
        uint? originalConsoleMode = null;
#if FEATURE_WINDOWSINTEROP
        if (IsWindows)
        {
            try
            {
                HANDLE outputStream = PInvoke.GetStdHandle(useStandardError ? STD_HANDLE.STD_ERROR_HANDLE : STD_HANDLE.STD_OUTPUT_HANDLE);
                if (PInvoke.GetConsoleMode(outputStream, out CONSOLE_MODE consoleMode))
                {
                    if (consoleMode.HasFlag(CONSOLE_MODE.ENABLE_VIRTUAL_TERMINAL_PROCESSING))
                    {
                        acceptAnsiColorCodes = true;
                    }
                    else
                    {
                        originalConsoleMode = (uint)consoleMode;
                        consoleMode |= CONSOLE_MODE.ENABLE_VIRTUAL_TERMINAL_PROCESSING;
                        if (PInvoke.SetConsoleMode(outputStream, consoleMode) && PInvoke.GetConsoleMode(outputStream, out consoleMode))
                        {
                            acceptAnsiColorCodes = consoleMode.HasFlag(CONSOLE_MODE.ENABLE_VIRTUAL_TERMINAL_PROCESSING);
                        }
                    }

                    outputIsScreen = PInvoke.GetFileType(outputStream) == FILE_TYPE.FILE_TYPE_CHAR;
                    acceptAnsiColorCodes &= outputIsScreen;
                }
            }
            catch
            {
                // In the unlikely case that the above fails we just ignore and continue.
            }
        }
        else
#endif
        {
            // On posix OSes detect whether the terminal supports VT100 from the value of the TERM environment variable.
            acceptAnsiColorCodes = AnsiDetector.IsAnsiSupported(Environment.GetEnvironmentVariable("TERM"));
            // It wasn't redirected as tested above so we assume output is screen/console
            outputIsScreen = true;
        }
        return (acceptAnsiColorCodes, outputIsScreen, originalConsoleMode);
    }

    internal static void RestoreConsoleMode(uint? originalConsoleMode, bool useStandardError = false)
    {
#if FEATURE_WINDOWSINTEROP
        if (IsWindows && originalConsoleMode is not null)
        {
            HANDLE stdOut = PInvoke.GetStdHandle(useStandardError ? STD_HANDLE.STD_ERROR_HANDLE : STD_HANDLE.STD_OUTPUT_HANDLE);
            _ = PInvoke.SetConsoleMode(stdOut, (CONSOLE_MODE)originalConsoleMode.Value);
        }
#endif
    }

    [SupportedOSPlatform("linux")]
    [DllImport("libc", SetLastError = true)]
    internal static extern int chmod(string pathname, int mode);

    [SupportedOSPlatform("linux")]
    [DllImport("libc", SetLastError = true)]
    internal static extern int mkdir(string path, int mode);

    [DllImport("libc", SetLastError = true)]
    internal static extern int symlink(string oldpath, string newpath);

#if FEATURE_WINDOWSINTEROP
    [SupportedOSPlatform("windows6.1")]
    internal static unsafe bool SetThreadErrorMode(int newMode, out int oldMode)
    {
        THREAD_ERROR_MODE oldModeU;
        bool result = PInvoke.SetThreadErrorMode((THREAD_ERROR_MODE)newMode, &oldModeU);
        oldMode = (int)oldModeU;
        return result;
    }

    [SupportedOSPlatform("windows6.1")]
    private static unsafe SafeProcessHandle OpenProcess(PROCESS_ACCESS_RIGHTS dwDesiredAccess, bool bInheritHandle, int dwProcessId)
    {
        HANDLE h = PInvoke.OpenProcess(dwDesiredAccess, bInheritHandle, (uint)dwProcessId);
        return new SafeProcessHandle((IntPtr)h.Value);
    }

    [SupportedOSPlatform("windows6.1")]
    private static unsafe int NtQueryInformationProcess(
        SafeProcessHandle hProcess,
        ref PROCESS_BASIC_INFORMATION pbi,
        ref int pSize)
    {
        fixed (PROCESS_BASIC_INFORMATION* pbiPtr = &pbi)
        {
            uint returnLength = 0;
            NTSTATUS status = Wdk.PInvoke.NtQueryInformationProcess(
                (HANDLE)hProcess.DangerousGetHandle(),
                WdkThreading.PROCESSINFOCLASS.ProcessBasicInformation,
                pbiPtr,
                (uint)sizeof(PROCESS_BASIC_INFORMATION),
                ref returnLength);
            pSize = (int)returnLength;
            return status.Value;
        }
    }

#endif
}
