// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Generates a hash of a given ItemGroup items. Metadata is not considered in the hash.
    /// </summary>
    /// <remarks>
    /// Currently uses SHA256. Implementation subject to change between MSBuild versions.
    /// This class is not intended as a cryptographic security measure, only uniqueness between build executions
    /// - collisions can theoretically be possible in the future (should we move to noncrypto hash) and should be handled gracefully by the caller.
    ///
    /// Usage of cryptographic secure hash brings slight performance penalty, but it is considered acceptable.
    /// Would this need to be revised - XxHash64 from System.Io.Hashing could be used instead for better performance.
    /// (That however currently requires load of additional binary into VS process which has it's own costs)
    /// </remarks>
    [MSBuildMultiThreadableTask]
    public class Hash : TaskExtension
    {
        private const char ItemSeparatorCharacter = '\u2028';
        private static readonly Encoding s_encoding = Encoding.UTF8;
        private static readonly byte[] s_itemSeparatorCharacterBytes = s_encoding.GetBytes([ItemSeparatorCharacter]);

        // Size of buffer where bytes of the strings are stored until sha.TransformBlock is to be run on them.
        // It is needed to get a balance between amount of costly sha.TransformBlock calls and amount of allocated memory.
        private const int ShaBufferSize = 512;

        // Size of chunks in which ItemSpecs would be cut.
        // We have chosen this length so itemSpecChunkByteBuffer rented from ArrayPool will be close but not bigger than 512.
        private const int MaxInputChunkLength = 169;

        /// <summary>
        /// Items from which to generate a hash.
        /// </summary>
        [Required]
        public ITaskItem[] ItemsToHash { get; set; }

        /// <summary>
        /// When true, will generate a case-insensitive hash.
        /// </summary>
        public bool IgnoreCase { get; set; }

        /// <summary>
        /// Optional path map, in the same "from=to,from2=to2" form the compiler accepts for <c>/pathmap</c>.
        /// When set, each mapping is applied (as a path-prefix replacement) to every item before it is hashed.
        /// This makes the hash independent of absolute, machine- or checkout-location-specific paths, matching
        /// the determinism the compiler already applies to its own output. When empty (the default) the task
        /// behaves exactly as before, so enabling it is opt-in for the caller.
        /// </summary>
        public string PathMap { get; set; }

        /// <summary>
        /// Hash of the ItemsToHash ItemSpec.
        /// </summary>
        [Output]
        public string HashResult { get; set; }

        /// <summary>
        /// Execute the task.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "This is not intended as a cryptographic security measure, only for uniqueness between build executions.")]
        public override bool Execute()
        {
            if (ItemsToHash?.Length > 0)
            {
                // Parse the optional path map once. When empty, items are hashed verbatim (legacy behavior).
                List<KeyValuePair<string, string>> pathMap = ParsePathMap(PathMap);

                using (var sha = CreateHashAlgorithm())
                {
                    // Buffer in which bytes of the strings are to be stored until their number reaches the limit size.
                    // Once the limit is reached, the sha.TransformBlock is to be run on all the bytes of this buffer.
                    byte[] shaBuffer = null;

                    // Buffer in which bytes of items' ItemSpec are to be stored.
                    byte[] itemSpecChunkByteBuffer = null;

                    try
                    {
                        shaBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(ShaBufferSize);
                        itemSpecChunkByteBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(s_encoding.GetMaxByteCount(MaxInputChunkLength));

                        int shaBufferPosition = 0;
                        for (int i = 0; i < ItemsToHash.Length; i++)
                        {
                            string mappedItemSpec = NormalizePathPrefix(ItemsToHash[i].ItemSpec, pathMap);
                            string itemSpec = IgnoreCase ? mappedItemSpec.ToUpperInvariant() : mappedItemSpec;

                            // Slice the itemSpec string into chunks of reasonable size and add them to sha buffer.
                            for (int itemSpecPosition = 0; itemSpecPosition < itemSpec.Length; itemSpecPosition += MaxInputChunkLength)
                            {
                                int charsToProcess = Math.Min(itemSpec.Length - itemSpecPosition, MaxInputChunkLength);
                                int byteCount = s_encoding.GetBytes(itemSpec, itemSpecPosition, charsToProcess, itemSpecChunkByteBuffer, 0);

                                shaBufferPosition = AddBytesToShaBuffer(sha, shaBuffer, shaBufferPosition, ShaBufferSize, itemSpecChunkByteBuffer, byteCount);
                            }

                            shaBufferPosition = AddBytesToShaBuffer(sha, shaBuffer, shaBufferPosition, ShaBufferSize, s_itemSeparatorCharacterBytes, s_itemSeparatorCharacterBytes.Length);
                        }

                        sha.TransformFinalBlock(shaBuffer, 0, shaBufferPosition);

#if NET
                        HashResult = Convert.ToHexStringLower(sha.Hash);
#else
                        using (var stringBuilder = new ReuseableStringBuilder(sha.HashSize))
                        {
                            foreach (var b in sha.Hash)
                            {
                                stringBuilder.Append(b.ToString("x2"));
                            }
                            HashResult = stringBuilder.ToString();
                        }
#endif
                    }
                    finally
                    {
                        if (shaBuffer != null)
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(shaBuffer);
                        }
                        if (itemSpecChunkByteBuffer != null)
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(itemSpecChunkByteBuffer);
                        }
                    }
                }
            }
            return true;
        }

        private HashAlgorithm CreateHashAlgorithm()
        {
            return SHA256.Create();
        }

        /// <summary>
        /// Parses a "from=to,from2=to2" path map (the same value the compiler receives via /pathmap) into
        /// prefix-replacement pairs, ordered longest key first so the most specific prefix wins. Returns an
        /// empty list when there is nothing to apply. Kept in sync with the compiler's path-map parsing
        /// (Microsoft.CodeAnalysis.CommandLineParser.ParsePathMap / SortPathMap) so the hashed paths match
        /// the paths the compiler produces.
        /// </summary>
        private static List<KeyValuePair<string, string>> ParsePathMap(string pathMap)
        {
            var result = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrEmpty(pathMap))
            {
                return result;
            }

            foreach (var kEqualsV in SplitWithDoubledSeparatorEscaping(pathMap, ','))
            {
                if (kEqualsV.Length == 0)
                {
                    continue;
                }

                var kv = SplitWithDoubledSeparatorEscaping(kEqualsV, '=');
                if (kv.Length != 2)
                {
                    continue;
                }

                var from = kv[0];
                var to = kv[1];
                if (from.Length == 0 || to.Length == 0)
                {
                    continue;
                }

                result.Add(new KeyValuePair<string, string>(EnsureTrailingSeparator(from), EnsureTrailingSeparator(to)));
            }

            result.Sort((x, y) => -x.Key.Length.CompareTo(y.Key.Length));
            return result;
        }

        /// <summary>
        /// Kept in sync with Microsoft.CodeAnalysis.CommandLineParser.SplitWithDoubledSeparatorEscaping.
        /// </summary>
        private static string[] SplitWithDoubledSeparatorEscaping(string str, char separator)
        {
            if (str.Length == 0)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>();
            var part = new StringBuilder();

            int i = 0;
            while (i < str.Length)
            {
                char c = str[i++];
                if (c == separator)
                {
                    if (i < str.Length && str[i] == separator)
                    {
                        i++;
                    }
                    else
                    {
                        result.Add(part.ToString());
                        part.Clear();
                        continue;
                    }
                }

                part.Append(c);
            }

            result.Add(part.ToString());
            return result.ToArray();
        }

        /// <summary>
        /// Kept in sync with Roslyn.Utilities.PathUtilities.EnsureTrailingSeparator.
        /// </summary>
        private static string EnsureTrailingSeparator(string s)
        {
            if (s.Length == 0 || s[s.Length - 1] == '/' || s[s.Length - 1] == '\\')
            {
                return s;
            }

            // Use the existing slashes in the path, if they're consistent.
            bool hasSlash = s.IndexOf('/') >= 0;
            bool hasBackslash = s.IndexOf('\\') >= 0;
            if (hasSlash && !hasBackslash)
            {
                return s + '/';
            }
            else if (!hasSlash && hasBackslash)
            {
                return s + '\\';
            }
            else
            {
                // If there are no slashes or they are inconsistent, use the current platform's slash.
                return s + Path.DirectorySeparatorChar;
            }
        }

        /// <summary>
        /// Applies the first matching path-prefix mapping to <paramref name="filePath"/>. Kept in sync with
        /// Roslyn.Utilities.PathUtilities.NormalizePathPrefix. Comparison is ordinal because the compiler
        /// expects consistent capitalization for path-map keys.
        /// </summary>
        private static string NormalizePathPrefix(string filePath, List<KeyValuePair<string, string>> pathMap)
        {
            if (pathMap.Count == 0 || string.IsNullOrEmpty(filePath))
            {
                return filePath;
            }

            foreach (var kv in pathMap)
            {
                var oldPrefix = kv.Key;
                if (!(oldPrefix?.Length > 0))
                {
                    continue;
                }

                // oldPrefix always ends with a path separator, so a prefix match cannot be a partial segment
                // (e.g. map /goo=/bar does not match /goooo).
                if (filePath.StartsWith(oldPrefix, StringComparison.Ordinal))
                {
                    var replacementPrefix = kv.Value;
                    var replacement = replacementPrefix + filePath.Substring(oldPrefix.Length);

                    // Normalize the path separators if they are used uniformly in the replacement prefix.
                    bool hasSlash = replacementPrefix.IndexOf('/') >= 0;
                    bool hasBackslash = replacementPrefix.IndexOf('\\') >= 0;
                    return
                        (hasSlash && !hasBackslash) ? replacement.Replace('\\', '/') :
                        (hasBackslash && !hasSlash) ? replacement.Replace('/', '\\') :
                        replacement;
                }
            }

            return filePath;
        }

        /// <summary>
        /// Add bytes to the sha buffer. Once the limit size is reached, sha.TransformBlock is called and the buffer is flushed.
        /// </summary>
        /// <param name="sha">Hashing algorithm sha.</param>
        /// <param name="shaBuffer">The sha buffer which stores bytes of the strings. Bytes should be added to this buffer.</param>
        /// <param name="shaBufferPosition">Number of used bytes of the sha buffer.</param>
        /// <param name="shaBufferSize">The size of sha buffer.</param>
        /// <param name="byteBuffer">Bytes buffer which contains bytes to be written to sha buffer.</param>
        /// <param name="byteCount">Amount of bytes that are to be added to sha buffer.</param>
        /// <returns>Updated shaBufferPosition.</returns>
        private int AddBytesToShaBuffer(HashAlgorithm sha, byte[] shaBuffer, int shaBufferPosition, int shaBufferSize, byte[] byteBuffer, int byteCount)
        {
            int bytesProcessed = 0;
            while (shaBufferPosition + byteCount >= shaBufferSize)
            {
                int shaBufferFreeSpace = shaBufferSize - shaBufferPosition;

                if (shaBufferPosition == 0)
                {
                    // If sha buffer is empty and bytes number is big enough there is no need to copy bytes to sha buffer.
                    // Pass the bytes to TransformBlock right away.
                    sha.TransformBlock(byteBuffer, bytesProcessed, shaBufferSize, null, 0);
                }
                else
                {
                    Array.Copy(byteBuffer, bytesProcessed, shaBuffer, shaBufferPosition, shaBufferFreeSpace);
                    sha.TransformBlock(shaBuffer, 0, shaBufferSize, null, 0);
                    shaBufferPosition = 0;
                }

                bytesProcessed += shaBufferFreeSpace;
                byteCount -= shaBufferFreeSpace;
            }

            Array.Copy(byteBuffer, bytesProcessed, shaBuffer, shaBufferPosition, byteCount);
            shaBufferPosition += byteCount;

            return shaBufferPosition;
        }
    }
}
