// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Shared.FileSystem;
#if FEATURE_WINDOWSINTEROP
using Microsoft.Build.Tasks.Fusion;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
#endif

#if !NET
using System.Text;
#endif
using System.Reflection;
using Microsoft.Build.Shared;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
#if !NET
using System.Linq;
#endif
#if FEATURE_HANDLEPROCESSCORRUPTEDSTATEEXCEPTIONS
using System.Runtime.ExceptionServices;
#endif
using System.Text.RegularExpressions;
#if FEATURE_WINDOWSINTEROP
using System.Runtime.Versioning;
#endif
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Interop methods.
    /// </summary>
    internal static partial class NativeMethods
    {
        #region Constants

        internal static Guid GUID_TYPELIB_NAMESPACE = new Guid("{0F21F359-AB84-41E8-9A78-36D110E6D2F9}");
        internal static Guid GUID_ExportedFromComPlus = new Guid("{90883f05-3d28-11d2-8f17-00a0c9a6186d}");

        // The stdole2 type library LIBID (not an interface IID, so it has no CsWin32 struct equivalent).
        internal static Guid LIBID_StdOle = new Guid("{00020430-0000-0000-C000-000000000046}");

        // Set of IMAGE_FILE constants which represent the processor architectures for native assemblies.
        internal const UInt16 IMAGE_FILE_MACHINE_UNKNOWN = 0x0; // The contents of this field are assumed to be applicable to any machine type
        internal const UInt16 IMAGE_FILE_MACHINE_INVALID = UInt16.MaxValue; // Invalid value for the machine type.
        internal const UInt16 IMAGE_FILE_MACHINE_AMD64 = 0x8664; // x64
        internal const UInt16 IMAGE_FILE_MACHINE_ARM = 0x1c0; // ARM little endian
        internal const UInt16 IMAGE_FILE_MACHINE_ARMV7 = 0x1c4; // ARMv7 (or higher) Thumb mode only
        internal const UInt16 IMAGE_FILE_MACHINE_I386 = 0x14c; // Intel 386 or later processors and compatible processors
        internal const UInt16 IMAGE_FILE_MACHINE_IA64 = 0x200; // Intel Itanium processor family
        internal const UInt16 IMAGE_FILE_MACHINE_ARM64 = 0xAA64; // ARM64 Little-Endian
        internal const UInt16 IMAGE_FILE_MACHINE_R4000 = 0x166; // Used to test a architecture we do not expect to reference

        internal const int SE_ERR_ACCESSDENIED = 5;

        #endregion

        #region PInvoke

        //------------------------------------------------------------------------------
        // CreateHardLink
        //------------------------------------------------------------------------------
        [DllImport("libc", SetLastError = true)]
        internal static extern int link(string oldpath, string newpath);

        internal static bool MakeHardLink(string newFileName, string exitingFileName, ref string errorMessage, TaskLoggingHelper log)
        {
            bool hardLinkCreated;
            if (NativeMethodsShared.IsWindows)
            {
#if FEATURE_WINDOWSINTEROP
                hardLinkCreated = Windows.Win32.PInvoke.CreateHardLink(newFileName, exitingFileName);
                errorMessage = hardLinkCreated ? null : Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()).Message;
#else
                hardLinkCreated = false;
                errorMessage = "CreateHardLink is not supported in this build (FEATURE_WINDOWSINTEROP is disabled).";
#endif
            }
            else
            {
                hardLinkCreated = link(exitingFileName, newFileName) == 0;
                errorMessage = hardLinkCreated ? null : log.FormatResourceString("Copy.NonWindowsLinkErrorMessage", "link()", Marshal.GetLastWin32Error());
            }

            return hardLinkCreated;
        }

        #endregion

        #region Methods
#if FEATURE_HANDLEPROCESSCORRUPTEDSTATEEXCEPTIONS
        /// <summary>
        /// Given a pointer to a metadata blob, read the string parameter from it.  Returns true if
        /// a valid string was constructed and false otherwise.
        ///
        /// Adapted from bizapps\server\designers\models\packagemodel\nativemethods.cs (TryReadStringArgument) and
        /// the original ARD implementation in vsproject\compsvcspkg\enumcomplus.cpp (GetStringCustomAttribute)
        /// This code was taken from the vsproject\ReferenceManager\Providers\NativeMethods.cs
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        internal static unsafe bool TryReadMetadataString(string fullPath, IntPtr attrData, uint attrDataSize, out string strValue)
        {
            int attrDataOffset = 0;
            strValue = null;

            try
            {
                // Blob structure for an attribute with a constructor receiving one string
                // and no named parameters:
                //
                //     [2 bytes] Prolog: unsigned int16 with value 0x0001
                //     [1, 2 or 4 bytes] PackedLen: Number of bytes of string parameter
                //     [PackedLen bytes] String parameter encoded as UTF8
                //     [1 byte] Name Parameter Count: Named parameter count equal to 0

                // Minimum size is 4-bytes (Prolog + PackedLen).  Prolog must be 0x0001.
                if ((attrDataSize >= 4) && (Marshal.ReadInt16(attrData, attrDataOffset) == 1))
                {
                    int preReadOffset = 2; // pass the prolog
                    IntPtr attrDataPostProlog = attrData + preReadOffset;

                    int strLen;
                    // Get the offset at which the uncompressed data starts, and the
                    // length of the uncompressed data.
                    attrDataOffset = CorSigUncompressData(attrDataPostProlog, out strLen);

                    if (strLen != -1)
                    {
                        // the full size of the blob we were passed in should be sufficient to
                        // cover the prolog, compressed string length, and actual string.
                        if (attrDataSize >= preReadOffset + attrDataOffset + strLen)
                        {
                            // Read in the uncompressed data
                            byte[] bytes = new byte[(int)strLen];
                            int i;
                            for (i = 0; i < strLen; i++)
                            {
                                bytes[i] = Marshal.ReadByte(attrDataPostProlog, attrDataOffset + i);
                            }

                            // And convert it to the output string.
                            strValue = Encoding.UTF8.GetString(bytes);
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (AccessViolationException)
            {
                // The Marshal.ReadXXXX functions throw AVs when they're fed an invalid pointer, and very occasionally,
                // for some reason, on what seem to be otherwise perfectly valid assemblies (it must be
                // intermittent given that otherwise the user would be completely unable to use the reference
                // manager), the pointer that we generate to look up the AssemblyTitle is apparently invalid,
                // or for some reason Marshal.ReadByte thinks it is.
                //
                return false;
            }

            return strValue != null;
        }
#endif
        /// <summary>
        /// Returns the number of bytes that compressed data -- the length of the uncompressed
        /// data -- takes up, and has an out value of the length of the string.
        ///
        /// Decompression algorithm stolen from ndp\clr\src\toolbox\mdbg\corapi\metadata\cormetadata.cs, which
        /// was translated from the base implementation in ndp\clr\src\inc\cor.h
        /// This code was taken from the vsproject\ReferenceManager\Providers\NativeMethods.cs
        /// </summary>
        /// <param name="data">Pointer to the beginning of the data block</param>
        /// <param name="uncompressedDataLength">Length of the uncompressed data block</param>
        internal static unsafe int CorSigUncompressData(IntPtr data, out int uncompressedDataLength)
        {
            // As described in bizapps\server\designers\models\packagemodel\nativemethods.cs:
            // The maximum encodable integer is 29 bits long, 0x1FFFFFFF. The compression algorithm used is as follows (bit 0 is the least significant bit):
            // - If the value lies between 0 (0x00) and 127 (0x7F), inclusive, encode as a one-byte integer (bit 7 is clear, value held in bits 6 through 0)
            // - If the value lies between 2^8 (0x80) and 2^14 - 1 (0x3FFF), inclusive, encode as a 2-byte integer with bit 15 set, bit 14 clear (value held in bits 13 through 0)
            // - Otherwise, encode as a 4-byte integer, with bit 31 set, bit 30 set, bit 29 clear (value held in bits 28 through 0)
            // - A null string should be represented with the reserved single byte 0xFF, and no following data
            int count = -1;
            byte* bytes = (byte*)(data);
            uncompressedDataLength = 0;

            // Smallest.
            if ((*bytes & 0x80) == 0x00)       // 0??? ????
            {
                uncompressedDataLength = *bytes;
                count = 1;
            }
            // Medium.
            else if ((*bytes & 0xC0) == 0x80)  // 10?? ????
            {
                uncompressedDataLength = (int)((*bytes & 0x3f) << 8 | *(bytes + 1));
                count = 2;
            }
            else if ((*bytes & 0xE0) == 0xC0)      // 110? ????
            {
                uncompressedDataLength = (int)((*bytes & 0x1f) << 24 | *(bytes + 1) << 16 | *(bytes + 2) << 8 | *(bytes + 3));
                count = 4;
            }

            return count;
        }
        #endregion
        #region InternalClass
        /// <summary>
        /// This class is a wrapper over the native GAC enumeration API.
        /// </summary>
        [ComVisible(false)]
        internal partial class AssemblyCacheEnum : IEnumerable<AssemblyNameExtension>
        {
            /// <summary>
            /// Path to the gac
            /// </summary>
            private static readonly string s_gacPath = Path.Combine(NativeMethodsShared.FrameworkBasePath, "gac");

            private const string AssemblyVersionPattern = @"^([.\d]+)_([^_]*)_([a-fA-F\d]{16})$";

            /// <summary>
            /// Regex for directory version parsing
            /// </summary>
#if NET
            [GeneratedRegex(AssemblyVersionPattern, RegexOptions.CultureInvariant)]
            private static partial Regex AssemblyVersionRegex { get; }
#else
            private static Regex AssemblyVersionRegex { get; } = new Regex(AssemblyVersionPattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
#endif

#if FEATURE_WINDOWSINTEROP
            /// <summary>
            /// Agile wrapper around the IAssemblyEnum COM pointer from fusion.dll. Provides
            /// thread-agile access and finalizer-driven release if the enum is never iterated.
            /// </summary>
            private AgileComPointer<IAssemblyEnum> _agileAssemblyEnum;
#endif

            /// <summary>
            /// For non-Windows implementation, we need assembly name
            /// </summary>
            private AssemblyName _assemblyNameVersion;

            /// <summary>
            /// For non-Windows implementation, we need assembly name
            /// </summary>
            private IEnumerable<string> _gacDirectories;

            // null means enumerate all the assemblies
            internal AssemblyCacheEnum(String assemblyName)
            {
                InitializeEnum(assemblyName);
            }

            /// <summary>
            /// Initialize the GAC Enum
            /// </summary>
            /// <param name="assemblyName"></param>
            private unsafe void InitializeEnum(String assemblyName)
            {
                if (NativeMethodsShared.IsWindows)
                {
#if !FEATURE_WINDOWSINTEROP
                    throw new PlatformNotSupportedException();
#else
                    using ComScope<IAssemblyName> fusionName = new(null);
                    using ComScope<IAssemblyEnum> assemblyEnum = new(null);

                    HRESULT hr = HRESULT.S_OK;
                    if (assemblyName != null)
                    {
                        fixed (char* pAssemblyName = assemblyName)
                        {
                            hr = Fusion.NativeMethods.CreateAssemblyNameObject(
                                fusionName,
                                pAssemblyName,
                                CreateAssemblyNameObjectFlags.CANOF_PARSE_DISPLAY_NAME
                                /* parse components assuming the assemblyName is a fusion name, this does not have to be a full fusion name*/,
                                null);
                        }
                    }

                    if (hr.Succeeded)
                    {
                        hr = Fusion.NativeMethods.CreateAssemblyEnum(
                            assemblyEnum,
                            null,
                            fusionName.Pointer,
                            AssemblyCacheFlags.GAC,
                            null);
                    }

                    if (hr.Succeeded && !assemblyEnum.IsNull)
                    {
                        // AgileComPointer registers in the GIT (which AddRefs). takeOwnership: false
                        // because the ComScope owns our reference and will Release deterministically
                        // when this method returns.
                        _agileAssemblyEnum = new AgileComPointer<IAssemblyEnum>(assemblyEnum.Pointer, takeOwnership: false);
                    }
#endif
                }
                else
                {
                    if (FileSystems.Default.DirectoryExists(s_gacPath))
                    {
                        if (!string.IsNullOrWhiteSpace(assemblyName))
                        {
                            _assemblyNameVersion = new AssemblyName(assemblyName);
                            _gacDirectories = Directory.EnumerateDirectories(s_gacPath, _assemblyNameVersion.Name);
                        }
                        else
                        {
                            _gacDirectories = Directory.EnumerateDirectories(s_gacPath);
                        }
                    }
                    else
                    {
                        _gacDirectories = [];
                    }
                }
            }

            public IEnumerator<AssemblyNameExtension> GetEnumerator()
            {
                if (NativeMethodsShared.IsWindows)
                {
#if !FEATURE_WINDOWSINTEROP
                    yield break;
#else
                    if (_agileAssemblyEnum is null)
                    {
                        yield break;
                    }

                    try
                    {
                        while (true)
                        {
                            string assemblyFusionName = GetNextAssemblyFusionName();
                            if (assemblyFusionName is null)
                            {
                                yield break;
                            }

                            yield return new AssemblyNameExtension(assemblyFusionName);
                        }
                    }
                    finally
                    {
                        _agileAssemblyEnum?.Dispose();
                        _agileAssemblyEnum = null;
                    }
#endif
                }
                else
                {
                    foreach (var dir in _gacDirectories)
                    {
                        var assemblyName = Path.GetFileName(dir);
                        if (!string.IsNullOrWhiteSpace(assemblyName))
                        {
                            foreach (var version in Directory.EnumerateDirectories(dir))
                            {
                                var versionString = Path.GetFileName(version);
                                if (!string.IsNullOrWhiteSpace(versionString))
                                {
                                    var match = AssemblyVersionRegex.Match(versionString);
                                    if (match.Success)
                                    {
                                        var name = new AssemblyName
                                        {
                                            Name = assemblyName,
                                            CultureInfo =
                                                               !string.IsNullOrWhiteSpace(
                                                                   match.Groups[2].Value)
                                                                   ? new CultureInfo(
                                                                         match.Groups[2].Value)
                                                                   : CultureInfo.InvariantCulture
                                        };
                                        if (!string.IsNullOrEmpty(match.Groups[1].Value))
                                        {
                                            name.Version = new Version(match.Groups[1].Value);
                                        }
                                        if (!string.IsNullOrWhiteSpace(match.Groups[3].Value))
                                        {
                                            var value = match.Groups[3].Value;
                                            byte[] key =
#if NET
                                                Convert.FromHexString(value.AsSpan(0, 16));
#else
                                                Enumerable.Range(0, 16)
                                                .Where(x => x % 2 == 0)
                                                .Select(x => Convert.ToByte(value.Substring(x, 2), 16))
                                                .ToArray();
#endif
                                            name.SetPublicKeyToken(key);
                                        }

                                        yield return new AssemblyNameExtension(name);
                                    }
                                }
                            }
                        }
                    }
                }
            }

#if FEATURE_WINDOWSINTEROP
            [SupportedOSPlatform("windows5.0")]
            private unsafe string GetNextAssemblyFusionName()
            {
                using ComScope<IAssemblyEnum> assemblyEnum = _agileAssemblyEnum.GetInterface();
                using ComScope<IAssemblyName> fusionName = new(null);

                assemblyEnum.Pointer->GetNextAssembly(null, fusionName, 0).ThrowOnFailure();

                if (fusionName.IsNull)
                {
                    return null;
                }

                return GetFullName(fusionName.Pointer);
            }
#endif

#if FEATURE_WINDOWSINTEROP
            private static unsafe string GetFullName(IAssemblyName* fusionAsmName)
            {
#if DEBUG
                // Small initial buffer in DEBUG so the insufficient-buffer retry path is exercised by tests.
                const int InitialBufferSize = 16;
#else
                const int InitialBufferSize = 256;
#endif

                using BufferScope<char> buffer = new(stackalloc char[InitialBufferSize]);
                int ilen = buffer.Length;
                HRESULT hr;
                fixed (char* pBuffer = buffer)
                {
                    hr = fusionAsmName->GetDisplayName(pBuffer, &ilen, AssemblyNameDisplayFlags.ALL);
                }

                // Fusion writes the required size (wide chars including null terminator) to *pccDisplayName
                // and returns HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER) when the buffer is too small.
                if (hr == (HRESULT)WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER)
                {
                    buffer.EnsureCapacity(ilen);
                    fixed (char* pBuffer = buffer)
                    {
                        hr = fusionAsmName->GetDisplayName(pBuffer, &ilen, AssemblyNameDisplayFlags.ALL);
                    }
                }

                hr.ThrowOnFailure();

                // ilen now holds the actual char count including null terminator.
                int length = ilen;
                if (length > 0 && buffer[length - 1] == '\0')
                {
                    length--;
                }

                return buffer.Slice(0, length).ToString();
            }
#endif

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public static string AssemblyPathFromStrongName(string strongName)
            {
                var assemblyNameVersion = new AssemblyName(strongName);
                var path = Path.Combine(s_gacPath, assemblyNameVersion.Name);

                // See if we can find the name as a directory in the GAC
                if (FileSystems.Default.DirectoryExists(path))
                {
                    // Since we have a strong name, create the path to the dll
                    path = Path.Combine(
                        path,
                        string.Format(
                            "{0}_{1}_{2}",
                            assemblyNameVersion.Version.ToString(4),
                            assemblyNameVersion.CultureName != "neutral" ? assemblyNameVersion.CultureName : string.Empty,
#if NET
                            Convert.ToHexStringLower(assemblyNameVersion.GetPublicKeyToken())),
#else
                            assemblyNameVersion.GetPublicKeyToken()
                                .Aggregate(new StringBuilder(), (builder, v) => builder.Append(v.ToString("x2")))),
#endif
                        assemblyNameVersion.Name + ".dll");

                    if (FileSystems.Default.FileExists(path))
                    {
                        return path;
                    }
                }

                return null;
            }
        }
        #endregion
    }
}
