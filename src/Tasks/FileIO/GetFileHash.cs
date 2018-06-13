// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Computes the checksum for a single file.
    /// </summary>
    public sealed class GetFileHash : TaskExtension
    {
        internal const string _defaultFileHashAlgorithm = "SHA256";
        internal const string _hashEncodingHex = "hex";
        internal const string _hashEncodingBase64 = "base64";

        /// <summary>
        /// The files to be hashed.
        /// </summary>
        [Required]
        public ITaskItem[] Files { get; set; }

        /// <summary>
        /// The algorithm. Allowed values: SHA256, SHA384, SHA512. Default = SHA256.
        /// </summary>
        public string Algorithm { get; set; } = _defaultFileHashAlgorithm;

        /// <summary>
        /// The metadata name where the hash is stored in each item. Defaults to "FileHash".
        /// </summary>
        public string MetadataName { get; set; } = "FileHash";

        /// <summary>
        /// The encoding to use for generated hashs. Defaults to "hex". Allowed values = "hex", "base64"
        /// </summary>
        public string HashEncoding { get; set; } = _hashEncodingHex;

        /// <summary>
        /// The hash of the file. This is only set if there was one item group passed in.
        /// </summary>
        [Output]
        public string Hash { get; set; }

        /// <summary>
        /// The input files with additional metadata set to include the file hash.
        /// </summary>
        [Output]
        public ITaskItem[] Items { get; set; }

        public override bool Execute()
        {
            if (!SupportsAlgorithm(Algorithm))
            {
                Log.LogErrorFromResources("FileHash.UnrecognizedHashAlgorithm", Algorithm);
                return false;
            }

            if (!TryParseHashEncoding(HashEncoding, out var encoding))
            {
                Log.LogErrorFromResources("FileHash.UnrecognizedHashEncoding", HashEncoding);
                return false;
            }

            foreach (var file in Files)
            {
                if (!File.Exists(file.ItemSpec))
                {
                    Log.LogErrorFromResources("FileHash.FileNotFound", file.ItemSpec);
                }
            }

            if (Log.HasLoggedErrors)
            {
                return false;
            }

            foreach (var file in Files)
            {
                var hash = ComputeHash(Algorithm, file.ItemSpec);
                file.SetMetadata("FileHashAlgoritm", Algorithm);
                file.SetMetadata(MetadataName, EncodeHash(encoding, hash));
            }

            Items = Files;

            if (Files.Length == 1)
            {
                Hash = Files[0].GetMetadata(MetadataName);
            }

            return !Log.HasLoggedErrors;
        }

        internal static string EncodeHash(HashEncoding encoding, byte[] hash)
        {
            switch (encoding)
            {
                case Tasks.HashEncoding.Hex:
                    return ConversionUtilities.ConvertByteArrayToHex(hash);
                case Tasks.HashEncoding.Base64:
                    return Convert.ToBase64String(hash);
                default:
                    throw new NotImplementedException();
            }
        }

        internal static bool TryParseHashEncoding(string value, out HashEncoding encoding)
            => Enum.TryParse<HashEncoding>(value, /*ignoreCase:*/ true, out encoding);

        internal static bool SupportsAlgorithm(string algorithmName)
            => _supportedAlgorithms.Contains(algorithmName);

        internal static byte[] ComputeHash(string algorithmName, string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var algorithm = CreateAlgorithm(algorithmName))
            {
                return algorithm.ComputeHash(stream);
            }
        }

        private static readonly HashSet<string> _supportedAlgorithms
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "SHA256",
                "SHA384",
                "SHA512",
            };

        private static HashAlgorithm CreateAlgorithm(string algorithmName)
        {
            switch (algorithmName.ToUpperInvariant())
            {
                case "SHA256":
                    return SHA256.Create();
                case "SHA384":
                    return SHA384.Create();
                case "SHA512":
                    return SHA512.Create();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
