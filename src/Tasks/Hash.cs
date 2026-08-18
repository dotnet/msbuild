// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
                            string itemSpec = IgnoreCase ? ItemsToHash[i].ItemSpec.ToUpperInvariant() : ItemsToHash[i].ItemSpec;

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
