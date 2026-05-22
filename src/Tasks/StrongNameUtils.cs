// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Security;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Possible strong name states of an assembly
    /// </summary>
    internal enum StrongNameLevel
    {
        None,
        DelaySigned,
        FullySigned,
        Unknown,
    }

    /// <summary>
    /// Strong naming utilities.
    /// </summary>
    internal static class StrongNameUtils
    {
        private static byte[] ReadKeyFileContents(TaskLoggingHelper log, AbsolutePath keyFile)
        {
            try
            {
                // Read the stuff from the file stream
                using (FileStream fs = new FileStream(keyFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    int fileLength = (int)fs.Length;
                    byte[] keyFileContents = new byte[fileLength];

                    // TODO: Read the count of read bytes and check if it matches the expected length, if not raise an exception
                    fs.ReadExactly(keyFileContents, 0, fileLength);
                    return keyFileContents;
                }
            }
            catch (ArgumentException e)
            {
                log.LogErrorWithCodeFromResources("StrongNameUtils.KeyFileReadFailure", keyFile.OriginalValue);
                log.LogErrorFromException(e);
                throw new StrongNameException(e);
            }
            catch (IOException e)
            {
                log.LogErrorWithCodeFromResources("StrongNameUtils.KeyFileReadFailure", keyFile.OriginalValue);
                log.LogErrorFromException(e);
                throw new StrongNameException(e);
            }
            catch (SecurityException e)
            {
                log.LogErrorWithCodeFromResources("StrongNameUtils.KeyFileReadFailure", keyFile.OriginalValue);
                log.LogErrorFromException(e);
                throw new StrongNameException(e);
            }
        }

        /// <summary>
        /// Given a key file or container, extract private/public key data. Reused from vsdesigner code.
        /// </summary>
        internal static void GetStrongNameKey(TaskLoggingHelper log, string keyFile, string keyContainer, out StrongNameKeyPair keyPair, out byte[] publicKey)
        {
            // Wrap the raw key file path as an AbsolutePath (without rootedness validation) so we can
            // funnel through the single AbsolutePath-based implementation. Value and OriginalValue
            // are the same here because callers of this overload supply an already user-facing path.
            AbsolutePath keyFilePath = string.IsNullOrEmpty(keyFile)
                ? default
                : new AbsolutePath(keyFile, ignoreRootedCheck: true);
            GetStrongNameKey(log, keyFilePath, keyContainer, out keyPair, out publicKey);
        }

        /// <summary>
        /// Given a key file or container, extract private/public key data. Reused from vsdesigner code.
        /// </summary>
        internal static void GetStrongNameKey(TaskLoggingHelper log, AbsolutePath keyFile, string keyContainer, out StrongNameKeyPair keyPair, out byte[] publicKey)
        {
            if (!string.IsNullOrEmpty(keyContainer))
            {
                GetStrongNameKeyFromContainer(log, keyContainer, out keyPair, out publicKey);
            }
            else if (!string.IsNullOrEmpty(keyFile))
            {
                byte[] keyFileContents = ReadKeyFileContents(log, keyFile);
                var snp = new StrongNameKeyPair(keyFileContents);

                try
                {
                    publicKey = snp.PublicKey;
                    keyPair = snp;
                }
                catch (ArgumentException)
                {
                    keyPair = null;
                    publicKey = keyFileContents;
                }
            }
            else
            {
                keyPair = null;
                publicKey = null;
            }
        }

        private static void GetStrongNameKeyFromContainer(TaskLoggingHelper log, string keyContainer, out StrongNameKeyPair keyPair, out byte[] publicKey)
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

        /// <summary>
        /// Given an assembly path, determine if the assembly is [delay] signed or not.
        /// </summary>
        internal static StrongNameLevel GetAssemblyStrongNameLevel(string assemblyPath)
        {
            ErrorUtilities.VerifyThrowArgumentNull(assemblyPath);

            try
            {
                using FileStream stream = File.OpenRead(assemblyPath);
                using PEReader peReader = new PEReader(stream);
                CorHeader corHeader = peReader.PEHeaders.CorHeader;

                // No COR20 header means this isn't a managed PE; preserve the historical
                // "we don't know" answer rather than claiming the image is unsigned.
                if (corHeader is null)
                {
                    return StrongNameLevel.Unknown;
                }

                DirectoryEntry signature = corHeader.StrongNameSignatureDirectory;
                if (signature.RelativeVirtualAddress == 0 || signature.Size == 0)
                {
                    return StrongNameLevel.None;
                }

                return (corHeader.Flags & CorFlags.StrongNameSigned) != 0
                    ? StrongNameLevel.FullySigned
                    : StrongNameLevel.DelaySigned;
            }
            catch (IOException)
            {
                return StrongNameLevel.Unknown;
            }
            catch (BadImageFormatException)
            {
                return StrongNameLevel.Unknown;
            }
            catch (UnauthorizedAccessException)
            {
                return StrongNameLevel.Unknown;
            }
        }
    }
}
