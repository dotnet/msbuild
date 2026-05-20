// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Security;
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
                    int fileLength = (int)fs.Length;
                    keyFileContents = new byte[fileLength];

                    // TODO: Read the count of read bytes and check if it matches the expected length, if not raise an exception
                    fs.ReadExactly(keyFileContents, 0, fileLength);
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
