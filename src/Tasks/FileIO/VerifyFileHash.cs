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
        /// The expected hash of the file.
        /// </summary>
        [Required]
        public string Hash { get; set; }

        /// <summary>
        /// The encoding format of <see cref="Hash"/>. Defaults to "hex".
        /// </summary>
        public string HashEncoding { get; set; } = GetFileHash._hashEncodingHex;

        /// <summary>
        /// The hasing algorithm to use. Allowed values: SHA256, SHA384, SHA512. Default = SHA256
        /// </summary>
        public string Algorithm { get; set; } = GetFileHash._defaultFileHashAlgorithm;

        public override bool Execute()
        {
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

            if (!GetFileHash.TryParseHashEncoding(HashEncoding, out var encoding))
            {
                Log.LogErrorFromResources("FileHash.UnrecognizedHashEncoding", HashEncoding);
                return false;
            }

            byte[] hash = GetFileHash.ComputeHash(Algorithm, File);
            bool hashesMatch;
            switch (encoding)
            {
                case Tasks.HashEncoding.Hex:
                    var actualHash = ConversionUtilities.ConvertByteArrayToHex(hash);
                    hashesMatch = string.Equals(actualHash, Hash, StringComparison.OrdinalIgnoreCase);
                    break;
                case Tasks.HashEncoding.Base64:
                    byte[] expectedHash = Convert.FromBase64String(Hash);
                    hashesMatch = expectedHash.SequenceEqual(hash);
                    break;
                default:
                    throw new NotImplementedException();
            }

            if (!hashesMatch)
            {
                var actualHash = encoding == Tasks.HashEncoding.Hex
                    ? ConversionUtilities.ConvertByteArrayToHex(hash)
                    : Convert.ToBase64String(hash);
                Log.LogErrorFromResources("VerifyFileHash.HashMismatch", File, Algorithm, Hash, actualHash);
                return false;
            }

            return !Log.HasLoggedErrors;
        }
    }
}
