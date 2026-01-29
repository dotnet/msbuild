// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Verifies that a file matches the expected file hash.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public sealed class VerifyFileHash : TaskExtension, ICancelableTask, IMultiThreadableTask
    {
        /// <inheritdoc />
        public TaskEnvironment TaskEnvironment { get; set; }

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
        /// The hashing algorithm to use. Allowed values: SHA256, SHA384, SHA512. Default = SHA256.
        /// </summary>
        public string Algorithm { get; set; } = GetFileHash._defaultFileHashAlgorithm;

        public override bool Execute()
        {
            AbsolutePath filePath = TaskEnvironment.GetAbsolutePath(File);

            if (!FileSystems.Default.FileExists(filePath))
            {
                Log.LogErrorWithCodeFromResources("FileHash.FileNotFound", filePath.OriginalValue);
                return false;
            }

            if (!GetFileHash.SupportedAlgorithms.TryGetValue(Algorithm, out var algorithmFactory))
            {
                Log.LogErrorWithCodeFromResources("FileHash.UnrecognizedHashAlgorithm", Algorithm);
                return false;
            }

            if (!GetFileHash.TryParseHashEncoding(HashEncoding, out var encoding))
            {
                Log.LogErrorWithCodeFromResources("FileHash.UnrecognizedHashEncoding", HashEncoding);
                return false;
            }

            byte[] hash = GetFileHash.ComputeHash(algorithmFactory, filePath, _cancellationTokenSource.Token);
            string actualHash = GetFileHash.EncodeHash(encoding, hash);
            var comparison = encoding == Tasks.HashEncoding.Hex
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!string.Equals(actualHash, Hash, comparison))
            {
                Log.LogErrorWithCodeFromResources("VerifyFileHash.HashMismatch", filePath.OriginalValue, Algorithm, Hash, actualHash);
                return false;
            }

            return !Log.HasLoggedErrors;
        }

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }
    }
}
