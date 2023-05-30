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
    /// This class is not intended as a cryptographic security measure, only uniqueness between build executions.
    /// </remarks>
    public class Hash : TaskExtension
    {
        private const char ItemSeparatorCharacter = '\u2028';
        private static readonly Encoding s_encoding = Encoding.UTF8;
        private static readonly byte[] s_itemSeparatorCharacterBytes = s_encoding.GetBytes(new char[] { ItemSeparatorCharacter });

        // Size of buffer where bytes of the strings are stored until sha256.TransformBlock is to be run on them.
        // It is needed to get a balance between amount of costly sha256.TransformBlock calls and amount of allocated memory.
        private const int Sha256BufferSize = 512;

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
                using (var sha = SHA256.Create())
                {
                    // Buffer in which bytes of the strings are to be stored until their number reaches the limit size.
                    // Once the limit is reached, the sha256.TransformBlock is to be run on all the bytes of this buffer.
                    byte[] shaBuffer = null;

                    // Buffer in which bytes of items' ItemSpec are to be stored.
                    byte[] itemSpecChunkByteBuffer = null;

                    try
                    {
                        shaBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(Sha256BufferSize);
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

                                shaBufferPosition = AddBytesToShaBuffer(sha, shaBuffer, shaBufferPosition, Sha256BufferSize, itemSpecChunkByteBuffer, byteCount);
                            }

                            shaBufferPosition = AddBytesToShaBuffer(sha, shaBuffer, shaBufferPosition, Sha256BufferSize, s_itemSeparatorCharacterBytes, s_itemSeparatorCharacterBytes.Length);
                        }

                        sha.TransformFinalBlock(shaBuffer, 0, shaBufferPosition);

                        using (var stringBuilder = new ReuseableStringBuilder(sha.HashSize))
                        {
                            foreach (var b in sha.Hash)
                            {
                                stringBuilder.Append(b.ToString("x2"));
                            }
                            HashResult = stringBuilder.ToString();
                        }
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

        /// <summary>
        /// Add bytes to the sha buffer. Once the limit size is reached, sha.TransformBlock is called and the buffer is flushed.
        /// </summary>
        /// <param name="sha256">Hashing algorithm sha256.</param>
        /// <param name="shaBuffer">The sha buffer which stores bytes of the strings. Bytes should be added to this buffer.</param>
        /// <param name="shaBufferPosition">Number of used bytes of the sha buffer.</param>
        /// <param name="shaBufferSize">The size of sha buffer.</param>
        /// <param name="byteBuffer">Bytes buffer which contains bytes to be written to sha buffer.</param>
        /// <param name="byteCount">Amount of bytes that are to be added to sha buffer.</param>
        /// <returns>Updated shaBufferPosition.</returns>
        private int AddBytesToShaBuffer(SHA256 sha256, byte[] shaBuffer, int shaBufferPosition, int shaBufferSize, byte[] byteBuffer, int byteCount)
        {
            int bytesProcessed = 0;
            while (shaBufferPosition + byteCount >= shaBufferSize)
            {
                int shaBufferFreeSpace = shaBufferSize - shaBufferPosition;

                if (shaBufferPosition == 0)
                {
                    // If sha buffer is empty and bytes number is big enough there is no need to copy bytes to sha buffer.
                    // Pass the bytes to TransformBlock right away.
                    sha256.TransformBlock(byteBuffer, bytesProcessed, shaBufferSize, null, 0);
                }
                else
                {
                    Array.Copy(byteBuffer, bytesProcessed, shaBuffer, shaBufferPosition, shaBufferFreeSpace);
                    sha256.TransformBlock(shaBuffer, 0, shaBufferSize, null, 0);
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
