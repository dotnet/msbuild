// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security;
using System.Runtime.InteropServices;

using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Possible strong name states of an assembly
    /// </summary>
    internal enum StrongNameLevel
    {
        None, DelaySigned, FullySigned, Unknown
    };

    /// <summary>
    /// Strong naming utilities.
    /// </summary>
    internal static class StrongNameUtils
    {
        /// <summary>
        /// Reads contents of a key file. Reused from vsdesigner code.
        /// </summary>
        internal static void ReadKeyFile(TaskLoggingHelper log, string keyFile, out StrongNameKeyPair keyPair, out byte[] publicKey)
        {
            // Initialize parameters
            keyPair = null;
            publicKey = null;

            byte[] keyFileContents;

            try
            {
                // Read the stuff from the file stream
                using (FileStream fs = new FileStream(keyFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    keyFileContents = new byte[(int)fs.Length];
                    fs.Read(keyFileContents, 0, (int)fs.Length);
                }
            }
            catch (ArgumentException e)
            {
                log.LogErrorWithCodeFromResources("StrongNameUtils.KeyFileReadFailure", keyFile);
                log.LogErrorFromException(e);
                throw new StrongNameException(e);
            }
            catch (IOException e)
            {
                log.LogErrorWithCodeFromResources("StrongNameUtils.KeyFileReadFailure", keyFile);
                log.LogErrorFromException(e);
                throw new StrongNameException(e);
            }
            catch (SecurityException e)
            {
                log.LogErrorWithCodeFromResources("StrongNameUtils.KeyFileReadFailure", keyFile);
                log.LogErrorFromException(e);
                throw new StrongNameException(e);
            }

            // Make a new key pair from what we read
            var snp = new StrongNameKeyPair(keyFileContents);

            // If anything fails reading the public key portion of the strong name key pair, then
            // assume that keyFile contained only the public key portion of the public/private pair.
            try
            {
                publicKey = snp.PublicKey;

                // If we didn't throw up to this point then we have a valid public/private key pair,
                // so assign the object just created above to the out parameter.  
                keyPair = snp;
            }
            catch (ArgumentException)
            {
                publicKey = keyFileContents;
            }
        }

        /// <summary>
        /// Given a key file or container, extract private/public key data. Reused from vsdesigner code.
        /// </summary>
        internal static void GetStrongNameKey(TaskLoggingHelper log, string keyFile, string keyContainer, out StrongNameKeyPair keyPair, out byte[] publicKey)
        {
            // Gets either a strong name key pair from the key file or a key container.
            // If keyFile and keyContainer are both null/zero length then returns null.
            // Initialize parameters
            keyPair = null;
            publicKey = null;
            if (!string.IsNullOrEmpty(keyContainer))
            {
                try
                {
                    keyPair = new StrongNameKeyPair(keyContainer);
                    publicKey = keyPair.PublicKey;
                }
                catch (SecurityException e)
                {
                    log.LogErrorWithCodeFromResources("StrongNameUtils.BadKeyContainer", keyContainer);
                    log.LogErrorFromException(e);
                    throw new StrongNameException(e);
                }
                catch (ArgumentException e)
                {
                    log.LogErrorWithCodeFromResources("StrongNameUtils.BadKeyContainer", keyContainer);
                    log.LogErrorFromException(e);
                    throw new StrongNameException(e);
                }
            }
            else if (!string.IsNullOrEmpty(keyFile))
            {
                ReadKeyFile(log, keyFile, out keyPair, out publicKey);
            }
        }

        /// <summary>
        /// Given an assembly path, determine if the assembly is [delay] signed or not. This code is based on similar unmanaged
        /// routines in vsproject and sn.exe (ndp tools) codebases.
        /// </summary>
        /// <param name="assemblyPath"></param>
        /// <returns></returns>
        internal static StrongNameLevel GetAssemblyStrongNameLevel(string assemblyPath)
        {
            ErrorUtilities.VerifyThrowArgumentNull(assemblyPath, nameof(assemblyPath));

            StrongNameLevel snLevel = StrongNameLevel.Unknown;
            IntPtr fileHandle = NativeMethods.InvalidIntPtr;

            try
            {
                // open the assembly
                fileHandle = NativeMethods.CreateFile(assemblyPath, NativeMethods.GENERIC_READ, FileShare.Read, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
                if (fileHandle == NativeMethods.InvalidIntPtr)
                {
                    return snLevel;
                }

                // if it's not a disk file, exit
                if (NativeMethods.GetFileType(fileHandle) != NativeMethods.FILE_TYPE_DISK)
                {
                    return snLevel;
                }

                IntPtr fileMappingHandle = IntPtr.Zero;

                try
                {
                    fileMappingHandle = NativeMethods.CreateFileMapping(fileHandle, IntPtr.Zero, NativeMethods.PAGE_READONLY, 0, 0, null);
                    if (fileMappingHandle == IntPtr.Zero)
                    {
                        return snLevel;
                    }

                    IntPtr fileMappingBase = IntPtr.Zero;

                    try
                    {
                        fileMappingBase = NativeMethods.MapViewOfFile(fileMappingHandle, NativeMethods.FILE_MAP_READ, 0, 0, IntPtr.Zero);
                        if (fileMappingBase == IntPtr.Zero)
                        {
                            return snLevel;
                        }

                        // retrieve NT headers pointer from the file
                        IntPtr ntHeader = NativeMethods.ImageNtHeader(fileMappingBase);
                        if (ntHeader == IntPtr.Zero)
                        {
                            return snLevel;
                        }

                        // get relative virtual address of the COR20 header
                        uint cor20HeaderRva = GetCor20HeaderRva(ntHeader);
                        if (cor20HeaderRva == 0)
                        {
                            return snLevel;
                        }

                        // get the pointer to the COR20 header structure
                        IntPtr cor20HeaderPtr = NativeMethods.ImageRvaToVa(ntHeader, fileMappingBase, cor20HeaderRva, out _);
                        if (cor20HeaderPtr == IntPtr.Zero)
                        {
                            return snLevel;
                        }

                        // get the COR20 structure itself
                        NativeMethods.IMAGE_COR20_HEADER cor20Header = (NativeMethods.IMAGE_COR20_HEADER)Marshal.PtrToStructure(cor20HeaderPtr, typeof(NativeMethods.IMAGE_COR20_HEADER));

                        // and finally, examine it. If no space is allocated for strong name signature, assembly is not signed.
                        if ((cor20Header.StrongNameSignature.VirtualAddress == 0) || (cor20Header.StrongNameSignature.Size == 0))
                        {
                            snLevel = StrongNameLevel.None;
                        }
                        else
                        {
                            // if there's allocated space and strong name flag is set, assembly is fully signed, or delay signed otherwise
                            if ((cor20Header.Flags & NativeMethods.COMIMAGE_FLAGS_STRONGNAMESIGNED) != 0)
                            {
                                snLevel = StrongNameLevel.FullySigned;
                            }
                            else
                            {
                                snLevel = StrongNameLevel.DelaySigned;
                            }
                        }
                    }
                    finally
                    {
                        if (fileMappingBase != IntPtr.Zero)
                        {
                            NativeMethods.UnmapViewOfFile(fileMappingBase);
                            fileMappingBase = IntPtr.Zero;
                        }
                    }
                }
                finally
                {
                    if (fileMappingHandle != IntPtr.Zero)
                    {
                        NativeMethods.CloseHandle(fileMappingHandle);
                        fileMappingHandle = IntPtr.Zero;
                    }
                }
            }
            finally
            {
                if (fileHandle != NativeMethods.InvalidIntPtr)
                {
                    NativeMethods.CloseHandle(fileHandle);
                    fileHandle = NativeMethods.InvalidIntPtr;
                }
            }

            return snLevel;
        }

        /// <summary>
        /// Retrieves the relative virtual address of the COR20 header, given the address of the NT headers structure. The catch
        /// here is that the NT headers struct can be either 32 or 64 bit version, and some fields have different sizes there. We
        /// need to see if we're dealing with a 32bit header or a 64bit one first.
        /// </summary>
        /// <param name="ntHeadersPtr"></param>
        /// <returns></returns>
        private static uint GetCor20HeaderRva(IntPtr ntHeadersPtr)
        {
            // read the first ushort in the optional header - we have an uint and IMAGE_FILE_HEADER preceding it
            ushort optionalHeaderMagic = (ushort)Marshal.ReadInt16(ntHeadersPtr, Marshal.SizeOf<uint>() + Marshal.SizeOf<NativeMethods.IMAGE_FILE_HEADER>());

            // this should really be a structure, but NDP can't marshal fixed size struct arrays in a struct... ugh.
            // this ulong corresponds to a IMAGE_DATA_DIRECTORY structure
            ulong cor20DataDirectoryLong;

            // see if we have a 32bit header or a 64bit header
            if (optionalHeaderMagic == NativeMethods.IMAGE_NT_OPTIONAL_HDR32_MAGIC)
            {
                // marshal data into the appropriate structure
                NativeMethods.IMAGE_NT_HEADERS32 ntHeader32 = (NativeMethods.IMAGE_NT_HEADERS32)Marshal.PtrToStructure(ntHeadersPtr, typeof(NativeMethods.IMAGE_NT_HEADERS32));
                cor20DataDirectoryLong = ntHeader32.optionalHeader.DataDirectory[NativeMethods.IMAGE_DIRECTORY_ENTRY_COMHEADER];
            }
            else if (optionalHeaderMagic == NativeMethods.IMAGE_NT_OPTIONAL_HDR64_MAGIC)
            {
                // marshal data into the appropriate structure
                NativeMethods.IMAGE_NT_HEADERS64 ntHeader64 = (NativeMethods.IMAGE_NT_HEADERS64)Marshal.PtrToStructure(ntHeadersPtr, typeof(NativeMethods.IMAGE_NT_HEADERS64));
                cor20DataDirectoryLong = ntHeader64.optionalHeader.DataDirectory[NativeMethods.IMAGE_DIRECTORY_ENTRY_COMHEADER];
            }
            else
            {
                Debug.Assert(false, "invalid file type!");
                return 0;
            }

            // cor20DataDirectoryLong is really a IMAGE_DATA_DIRECTORY structure which I had to pack into an ulong
            // (see comments for IMAGE_OPTIONAL_HEADER32/64 in NativeMethods.cs)
            // this code extracts the virtualAddress (uint) and size (uint) fields from the ulong by doing simple 
            // bit masking/shifting ops
            uint virtualAddress = (uint)(cor20DataDirectoryLong & 0x00000000ffffffff);
            // uint size = (uint)(cor20DataDirectoryLong >> 32);

            return virtualAddress;
        }
    }
}
