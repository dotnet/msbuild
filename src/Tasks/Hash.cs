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
    /// Currently uses SHA1. Implementation subject to change between MSBuild versions.
    /// This class is not intended as a cryptographic security measure, only uniqueness between build executions.
    /// </remarks>
    public class Hash : TaskExtension
    {
        private const char ItemSeparatorCharacter = '\u2028';
        private static readonly Encoding s_encoding = Encoding.UTF8;
        private static readonly byte[] s_itemSeparatorCharacterBytes = s_encoding.GetBytes(new char[] { ItemSeparatorCharacter });

        // Size of buffer where bytes of the strings are stored until sha1.TransformBlock is to be run on them.
        // It is needed to get a balance between amount of costly sha1.TransformBlock calls and amount of allocated memory.
        private const int Sha1BufferSize = 512;

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
                using (var sha1 = SHA1.Create())
                {
                    // Buffer in which bytes of the strings are to be stored until their number reaches the limit size.
                    // Once the limit is reached, the sha1.TransformBlock is to be run on all the bytes of this buffer.
                    byte[] sha1Buffer = null;

                    // Buffer in which bytes of items' ItemSpec are to be stored.
                    byte[] itemSpecChunkByteBuffer = null;

                    try
                    {
                        sha1Buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(Sha1BufferSize);
                        itemSpecChunkByteBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(s_encoding.GetMaxByteCount(MaxInputChunkLength));

                        int sha1BufferPosition = 0;
                        for (int i = 0; i < ItemsToHash.Length; i++)
                        {
                            string itemSpec = IgnoreCase ? ItemsToHash[i].ItemSpec.ToUpperInvariant() : ItemsToHash[i].ItemSpec;

                            // Slice the itemSpec string into chunks of reasonable size and add them to sha1 buffer.
                            for (int itemSpecPosition = 0; itemSpecPosition < itemSpec.Length; itemSpecPosition += MaxInputChunkLength)
                            {
                                int charsToProcess = Math.Min(itemSpec.Length - itemSpecPosition, MaxInputChunkLength);
                                int byteCount = s_encoding.GetBytes(itemSpec, itemSpecPosition, charsToProcess, itemSpecChunkByteBuffer, 0);

                                sha1BufferPosition = AddBytesToSha1Buffer(sha1, sha1Buffer, sha1BufferPosition, Sha1BufferSize, itemSpecChunkByteBuffer, byteCount);
                            }

                            sha1BufferPosition = AddBytesToSha1Buffer(sha1, sha1Buffer, sha1BufferPosition, Sha1BufferSize, s_itemSeparatorCharacterBytes, s_itemSeparatorCharacterBytes.Length);
                        }

                        sha1.TransformFinalBlock(sha1Buffer, 0, sha1BufferPosition);

                        using (var stringBuilder = new ReuseableStringBuilder(sha1.HashSize))
                        {
                            foreach (var b in sha1.Hash)
                            {
                                stringBuilder.Append(b.ToString("x2"));
                            }
                            HashResult = stringBuilder.ToString();
                        }
                    }
                    finally
                    {
                        if (sha1Buffer != null)
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(sha1Buffer);
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
        /// Add bytes to the sha1 buffer. Once the limit size is reached, sha1.TransformBlock is called and the buffer is flushed.
        /// </summary>
        /// <param name="sha1">Hashing algorithm sha1.</param>
        /// <param name="sha1Buffer">The sha1 buffer which stores bytes of the strings. Bytes should be added to this buffer.</param>
        /// <param name="sha1BufferPosition">Number of used bytes of the sha1 buffer.</param>
        /// <param name="sha1BufferSize">The size of sha1 buffer.</param>
        /// <param name="byteBuffer">Bytes buffer which contains bytes to be written to sha1 buffer.</param>
        /// <param name="byteCount">Amount of bytes that are to be added to sha1 buffer.</param>
        /// <returns>Updated sha1BufferPosition.</returns>
        private int AddBytesToSha1Buffer(SHA1 sha1, byte[] sha1Buffer, int sha1BufferPosition, int sha1BufferSize, byte[] byteBuffer, int byteCount)
        {
            int bytesProcessed = 0;
            while (sha1BufferPosition + byteCount >= sha1BufferSize)
            {
                int sha1BufferFreeSpace = sha1BufferSize - sha1BufferPosition;

                if (sha1BufferPosition == 0)
                {
                    // If sha1 buffer is empty and bytes number is big enough there is no need to copy bytes to sha1 buffer.
                    // Pass the bytes to TransformBlock right away.
                    sha1.TransformBlock(byteBuffer, bytesProcessed, sha1BufferSize, null, 0);
                }
                else
                {
                    Array.Copy(byteBuffer, bytesProcessed, sha1Buffer, sha1BufferPosition, sha1BufferFreeSpace);
                    sha1.TransformBlock(sha1Buffer, 0, sha1BufferSize, null, 0);
                    sha1BufferPosition = 0;
                }

                bytesProcessed += sha1BufferFreeSpace;
                byteCount -= sha1BufferFreeSpace;
            }

            Array.Copy(byteBuffer, bytesProcessed, sha1Buffer, sha1BufferPosition, byteCount);
            sha1BufferPosition += byteCount;

            return sha1BufferPosition;
        }
    }
}
