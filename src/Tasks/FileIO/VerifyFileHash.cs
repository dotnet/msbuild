// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Verifies that a file matches the expected file hash.
    /// </summary>
    public sealed class VerifyFileHash : TaskExtension
    {
        /// <summary>
        /// The file path.
        /// </summary>
        [Required]
        public string File { get; set; }

        /// <summary>
        /// The hasing algorithm to use. Allowed values: SHA256, SHA384, SHA512. Default = SHA256
        /// </summary>
        public string Algorithm { get; set; } = GetFileHash.DefaultFileHashAlgorithm;

        /// <summary>
        /// The expected hash of the file in hex.
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// The expected hash of the file in base64.
        /// </summary>
        public string HashBase64 { get; set; }

        public override bool Execute()
        {
            if (!(string.IsNullOrEmpty(Hash) ^ string.IsNullOrEmpty(HashBase64)))
            {
                Log.LogErrorFromResources("VerifyFileHash.InvalidInputParameters");
                return false;
            }

            if (!System.IO.File.Exists(File))
            {
                Log.LogErrorFromResources("FileHash.FileNotFound", File);
                return false;
            }

            if (!GetFileHash.SupportsAlgorithm(Algorithm))
            {
                Log.LogErrorFromResources("FileHash.UnrecognizedHashAlgorithm", Algorithm);
                return false;
            }

            byte[] hash = GetFileHash.ComputeHash(Algorithm, File);
            if (!string.IsNullOrEmpty(Hash))
            {
                var actualHash = ConversionUtilities.ConvertByteArrayToHex(hash);
                if (!string.Equals(actualHash, Hash, StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogErrorFromResources("VerifyFileHash.HashMismatch", File, Algorithm, Hash, actualHash);
                    return false;
                }
            }
            else
            {
                byte[] expectedHash = Convert.FromBase64String(HashBase64);
                if (!expectedHash.SequenceEqual(hash))
                {
                    var actualHash = Convert.ToBase64String(hash);
                    Log.LogErrorFromResources("VerifyFileHash.HashMismatch", File, Algorithm, HashBase64, actualHash);
                    return false;
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}
