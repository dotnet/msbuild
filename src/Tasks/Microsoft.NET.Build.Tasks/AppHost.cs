// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Runtime.InteropServices;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Embeds the App Name into the AppHost.exe
    /// </summary>
    internal static class AppHost
    {
        /// <summary>
        /// hash value embedded in default apphost executable in a place where the path to the app binary should be stored.
        /// </summary>
        private const string AppBinaryPathPlaceholder = "c3ab8ff13720e8ad9047dd39466b3c8974e592c2fa383d4a3960714caef0c4f2";
        private readonly static byte[] AppBinaryPathPlaceholderSearchValue = Encoding.UTF8.GetBytes(AppBinaryPathPlaceholder);

        /// <summary>
        /// Create an AppHost with embedded configuration of app binary location
        /// </summary>
        /// <param name="appHostSourceFilePath">The path of Apphost template, which has the place holder</param>
        /// <param name="appHostDestinationFilePath">The destination path for desired location to place, including the file name</param>
        /// <param name="appBinaryFilePath">Full path to app binary or relative path to the result apphost file</param>
        /// <param name="windowsGraphicalUserInterface">Specify whether to set the subsystem to GUI. Only valid for PE apphosts.</param>
        /// <param name="intermediateAssembly">Path to the intermediate assembly, used for copying resources to PE apphosts.</param>
        /// <param name="log">Specify the logger used to log warnings and messages. If null, no logging is done.</param>
        public static void Create(
            string appHostSourceFilePath,
            string appHostDestinationFilePath,
            string appBinaryFilePath,
            bool windowsGraphicalUserInterface = false,
            string intermediateAssembly = null,
            Logger log = null)
        {
            var bytesToWrite = Encoding.UTF8.GetBytes(appBinaryFilePath);
            if (bytesToWrite.Length > 1024)
            {
                throw new BuildErrorException(Strings.FileNameIsTooLong, appBinaryFilePath);
            }

            var destinationDirectory = new FileInfo(appHostDestinationFilePath).Directory.FullName;
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // Copy apphost to destination path so it inherits the same attributes/permissions.
            File.Copy(appHostSourceFilePath, appHostDestinationFilePath, overwrite: true);

            // Re-write the destination apphost with the proper contents.
            bool appHostIsPEImage = false;
            using (var memoryMappedFile = MemoryMappedFile.CreateFromFile(appHostDestinationFilePath))
            {
                using (MemoryMappedViewAccessor accessor = memoryMappedFile.CreateViewAccessor())
                {
                    SearchAndReplace(accessor, AppBinaryPathPlaceholderSearchValue, bytesToWrite, appHostSourceFilePath);

                    appHostIsPEImage = IsPEImage(accessor);

                    if (windowsGraphicalUserInterface)
                    {
                        if (!appHostIsPEImage)
                        {
                            throw new BuildErrorException(Strings.AppHostNotWindows, appHostSourceFilePath);
                        }

                        SetWindowsGraphicalUserInterfaceBit(accessor, appHostSourceFilePath);
                    }
                }
            }

            if (intermediateAssembly != null && appHostIsPEImage)
            {
                if (ResourceUpdater.IsSupportedOS())
                {
                    // Copy resources from managed dll to the apphost
                    new ResourceUpdater(appHostDestinationFilePath)
                        .AddResourcesFromPEImage(intermediateAssembly)
                        .Update();
                }
                else if (log != null)
                {
                    log.LogWarning(Strings.AppHostCustomizationRequiresWindowsHostWarning);
                }
            }

            // Memory-mapped write does not updating last write time
            File.SetLastWriteTimeUtc(appHostDestinationFilePath, DateTime.UtcNow);
        }

        // See: https://en.wikipedia.org/wiki/Knuth%E2%80%93Morris%E2%80%93Pratt_algorithm
        private static int[] ComputeKMPFailureFunction(byte[] pattern)
        {
            int[] table = new int[pattern.Length];
            if (pattern.Length >= 1)
            {
                table[0] = -1;
            }
            if (pattern.Length >= 2)
            {
                table[1] = 0;
            }

            int pos = 2;
            int cnd = 0;
            while (pos < pattern.Length)
            {
                if (pattern[pos - 1] == pattern[cnd])
                {
                    table[pos] = cnd + 1;
                    cnd++;
                    pos++;
                }
                else if (cnd > 0)
                {
                    cnd = table[cnd];
                }
                else
                {
                    table[pos] = 0;
                    pos++;
                }
            }
            return table;
        }

        // See: https://en.wikipedia.org/wiki/Knuth%E2%80%93Morris%E2%80%93Pratt_algorithm
        private static unsafe int KMPSearch(byte[] pattern, byte* bytes, long bytesLength)
        {
            int m = 0;
            int i = 0;
            int[] table = ComputeKMPFailureFunction(pattern);

            while (m + i < bytesLength)
            {
                if (pattern[i] == bytes[m + i])
                {
                    if (i == pattern.Length - 1)
                    {
                        return m;
                    }
                    i++;
                }
                else
                {
                    if (table[i] > -1)
                    {
                        m = m + i - table[i];
                        i = table[i];
                    }
                    else
                    {
                        m++;
                        i = 0;
                    }
                }
            }

            return -1;
        }

        private static unsafe void SearchAndReplace(
            MemoryMappedViewAccessor accessor,
            byte[] searchPattern,
            byte[] patternToReplace,
            string appHostSourcePath)
        {
            byte* pointer = null;

            try
            {
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
                byte* bytes = pointer + accessor.PointerOffset;

                int position = KMPSearch(searchPattern, bytes, accessor.Capacity);
                if (position < 0)
                {
                    throw new BuildErrorException(Strings.AppHostHasBeenModified, appHostSourcePath, AppBinaryPathPlaceholder);
                }

                accessor.WriteArray(
                    position: position,
                    array: patternToReplace,
                    offset: 0,
                    count: patternToReplace.Length);

                Pad0(searchPattern, patternToReplace, bytes, position);
            }
            finally
            {
                if (pointer != null)
                {
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                }
            }
        }

        private static unsafe void Pad0(byte[] searchPattern, byte[] patternToReplace, byte* bytes, int offset)
        {
            if (patternToReplace.Length < searchPattern.Length)
            {
                for (int i = patternToReplace.Length; i < searchPattern.Length; i++)
                {
                    bytes[i + offset] = 0x0;
                }
            }
        }

        /// <summary>
        /// The first two bytes of a PE file are a constant signature.
        /// </summary>
        private const UInt16 PEFileSignature = 0x5A4D;

        /// <summary>
        /// The offset of the PE header pointer in the DOS header.
        /// </summary>
        private const int PEHeaderPointerOffset = 0x3C;

        /// <summary>
        /// The offset of the Subsystem field in the PE header.
        /// </summary>
        private const int SubsystemOffset = 0x5C;

        /// <summary>
        /// The value of the sybsystem field which indicates Windows GUI (Graphical UI)
        /// </summary>
        private const UInt16 WindowsGUISubsystem = 0x2;

        /// <summary>
        /// The value of the subsystem field which indicates Windows CUI (Console)
        /// </summary>
        private const UInt16 WindowsCUISubsystem = 0x3;

        /// <summary>
        /// Check whether the apphost file is a windows PE image by looking at the first few bytes.
        /// </summary>
        /// <param name="accessor">The memory accessor which has the apphost file opened.</param>
        /// <returns>true if the accessor represents a PE image, false otherwise.</returns>
        private static unsafe bool IsPEImage(MemoryMappedViewAccessor accessor)
        {
            byte* pointer = null;

            try
            {
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
                byte* bytes = pointer + accessor.PointerOffset;

                // https://en.wikipedia.org/wiki/Portable_Executable
                // Validate that we're looking at Windows PE file
                if (((UInt16*)bytes)[0] != PEFileSignature || accessor.Capacity < PEHeaderPointerOffset + sizeof(UInt32))
                {
                    return false;
                }
                return true;
            }
            finally
            {
                if (pointer != null)
                {
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                }
            }
        }

        /// <summary>
        /// This method will attempt to set the subsystem to GUI. The apphost file should be a windows PE file.
        /// </summary>
        /// <param name="accessor">The memory accessor which has the apphost file opened.</param>
        /// <param name="appHostSourcePath">The path to the source apphost.</param>
        private static unsafe void SetWindowsGraphicalUserInterfaceBit(
            MemoryMappedViewAccessor accessor,
            string appHostSourcePath)
        {
            byte* pointer = null;

            try
            {
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
                byte* bytes = pointer + accessor.PointerOffset;

                // https://en.wikipedia.org/wiki/Portable_Executable
                UInt32 peHeaderOffset = ((UInt32*)(bytes + PEHeaderPointerOffset))[0];

                if (accessor.Capacity < peHeaderOffset + SubsystemOffset + sizeof(UInt16))
                {
                    throw new BuildErrorException(Strings.AppHostNotWindows, appHostSourcePath);
                }

                UInt16* subsystem = ((UInt16*)(bytes + peHeaderOffset + SubsystemOffset));

                // https://docs.microsoft.com/en-us/windows/desktop/Debug/pe-format#windows-subsystem
                // The subsystem of the prebuilt apphost should be set to CUI
                if (subsystem[0] != WindowsCUISubsystem)
                {
                    throw new BuildErrorException(Strings.AppHostNotWindowsCLI, appHostSourcePath);
                }

                // Set the subsystem to GUI
                subsystem[0] = WindowsGUISubsystem;
            }
            finally
            {
                if (pointer != null)
                {
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                }
            }
        }
    }
}
