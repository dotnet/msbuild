// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Computes the checksum for a single file.
    /// </summary>
    public sealed class GetFileHash : TaskExtension, ICancelableTask
    {
        internal const string _defaultFileHashAlgorithm = "SHA256";
        internal const string _hashEncodingHex = "hex";
        internal const string _hashEncodingBase64 = "base64";
        internal static readonly Dictionary<string, Func<HashAlgorithm>> SupportedAlgorithms
            = new Dictionary<string, Func<HashAlgorithm>>(StringComparer.OrdinalIgnoreCase)
            {
                ["SHA256"] = SHA256.Create,
                ["SHA384"] = SHA384.Create,
                ["SHA512"] = SHA512.Create,
            };

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
        /// The encoding to use for generated hashs. Defaults to "hex". Allowed values = "hex", "base64".
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
            if (!SupportedAlgorithms.TryGetValue(Algorithm, out var algorithmFactory))
            {
                Log.LogErrorWithCodeFromResources("FileHash.UnrecognizedHashAlgorithm", Algorithm);
                return false;
            }

            if (!TryParseHashEncoding(HashEncoding, out var encoding))
            {
                Log.LogErrorWithCodeFromResources("FileHash.UnrecognizedHashEncoding", HashEncoding);
                return false;
            }

            var parallelOptions = new ParallelOptions() { CancellationToken = _cancellationTokenSource.Token };

            var writeLock = new object();
            Parallel.For(0, Files.Length, parallelOptions, index =>
            {
                var file = Files[index];

                if (!FileSystems.Default.FileExists(file.ItemSpec))
                {
                    Log.LogErrorWithCodeFromResources("FileHash.FileNotFound", file.ItemSpec);
                    return;
                }

                var hash = ComputeHash(algorithmFactory, file.ItemSpec, _cancellationTokenSource.Token);
                var encodedHash = EncodeHash(encoding, hash);

                lock (writeLock)
                {
                    // We cannot guarantee Files instances are unique. Write to it inside a lock to
                    // avoid concurrent edits.
                    file.SetMetadata("FileHashAlgorithm", Algorithm);
                    file.SetMetadata(MetadataName, encodedHash);
                }
            });

            if (Log.HasLoggedErrors)
            {
                return false;
            }

            Items = Files;

            if (Files.Length == 1)
            {
                Hash = Files[0].GetMetadata(MetadataName);
            }

            return !Log.HasLoggedErrors;
        }

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }

        internal static string EncodeHash(HashEncoding encoding, byte[] hash)
        {
            return encoding switch
            {
                Tasks.HashEncoding.Hex => ConversionUtilities.ConvertByteArrayToHex(hash),
                Tasks.HashEncoding.Base64 => Convert.ToBase64String(hash),
                _ => throw new NotImplementedException(),
            };
        }

        internal static bool TryParseHashEncoding(string value, out HashEncoding encoding)
            => Enum.TryParse<HashEncoding>(value, /*ignoreCase:*/ true, out encoding);

        internal static byte[] ComputeHash(Func<HashAlgorithm> algorithmFactory, string filePath, CancellationToken ct)
        {
            using (var stream = File.OpenRead(filePath))
            using (var algorithm = algorithmFactory())
            {
#if NET
                return algorithm.ComputeHashAsync(stream, ct).Result;
#else
                return algorithm.ComputeHash(stream);
#endif
            }
        }
    }
}
