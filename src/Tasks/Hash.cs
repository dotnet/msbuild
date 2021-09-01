// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Generates a hash of a given ItemGroup items. Metadata is not considered in the hash.
    /// </summary>
    /// <remarks>
    /// Currently uses SHA1. Implementation subject to change between MSBuild versions. Not
    /// intended as a cryptographic security measure, only uniqueness between build executions.
    /// </remarks>
    public class Hash : TaskExtension
    {
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

        private static readonly char ItemSeparatorCharacter = '\u2028';

        private static readonly Encoding encoding = Encoding.UTF8;

        private static readonly byte[] ItemSeparatorCharacterBytes = encoding.GetBytes(new char[] { ItemSeparatorCharacter });

        private const int sha1BufferSize = 512;

        private const int maxInputChunkLength = 256;

        /// <summary>
        /// Execute the task.
        /// </summary>
        public override bool Execute()
        {
            if (ItemsToHash != null && ItemsToHash.Length > 0)
            {
                using (var sha1 = SHA1.Create())
                {
                    // Buffer in which bytes of the strings are to be stored until their number reachs the limit size.
                    // Once the limit is reached, the sha1.TransformBlock is be run on all the bytes of this buffer.
                    byte[] sha1Buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(sha1BufferSize);

                    int maxItemStringSize = encoding.GetMaxByteCount(Math.Min(ComputeMaxItemSpecLength(ItemsToHash), maxInputChunkLength));
                    byte[] bytesBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(maxItemStringSize);

                    int bufferLength = 0;
                    for (int i = 0; i < ItemsToHash.Length; i++)
                    {
                        string itemSpec = IgnoreCase ? ItemsToHash[i].ItemSpec.ToUpperInvariant() : ItemsToHash[i].ItemSpec;

                        // Slice the itemSpec string into chunks of reasonable size and add them to sha1 buffer.
                        int itemSpecLength = itemSpec.Length;
                        int itemSpecPosition = 0;
                        while (itemSpecLength > 0)
                        {
                            int charsToProcess = Math.Min(itemSpecLength, maxInputChunkLength);
                            int bytesNumber = encoding.GetBytes(itemSpec, itemSpecPosition, charsToProcess, bytesBuffer, 0);
                            itemSpecPosition += charsToProcess;
                            itemSpecLength -= charsToProcess;

                            AddBytesToSha1Buffer(sha1, sha1Buffer, ref bufferLength, sha1BufferSize, bytesBuffer, bytesNumber);
                        }

                        AddBytesToSha1Buffer(sha1, sha1Buffer, ref bufferLength, sha1BufferSize, ItemSeparatorCharacterBytes, ItemSeparatorCharacterBytes.Length);
                    }

                    sha1.TransformFinalBlock(sha1Buffer, 0, bufferLength);

                    using (var stringBuilder = new ReuseableStringBuilder(sha1.HashSize))
                    {
                        foreach (var b in sha1.Hash)
                        {
                            stringBuilder.Append(b.ToString("x2"));
                        }
                        HashResult = stringBuilder.ToString();
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
        /// <param name="sha1BufferLength">Number of used bytes of the sha1 buffer.</param>
        /// <param name="sha1BufferSize">The size of sha1 buffer.</param>
        /// <param name="bytesBuffer">Bytes buffer which contains bytes to be written to sha1 buffer.</param>
        /// <param name="bytesNumber">Amount of bytes that are to be added to sha1 buffer.</param>
        /// <returns></returns>
        private bool AddBytesToSha1Buffer(SHA1 sha1, byte[] sha1Buffer, ref int sha1BufferLength, int sha1BufferSize, byte[] bytesBuffer, int bytesNumber)
        {
            int bytesProcessedNumber = 0;
            while (sha1BufferLength + bytesNumber >= sha1BufferSize)
            {
                int sha1BufferFreeSpace = sha1BufferSize - sha1BufferLength;

                if (sha1BufferLength == 0)
                {
                    // If sha1 buffer is empty and bytes number is big enough there is no need to copy bytes to sha1 buffer.
                    // Pass the bytes to TransformBlock right away.
                    sha1.TransformBlock(bytesBuffer, 0, sha1BufferSize, null, 0);
                }
                else
                {
                    Array.Copy(bytesBuffer, bytesProcessedNumber, sha1Buffer, sha1BufferLength, sha1BufferFreeSpace);
                    sha1.TransformBlock(sha1Buffer, 0, sha1BufferSize, null, 0);
                    sha1BufferLength = 0;
                }

                bytesProcessedNumber += sha1BufferFreeSpace;
                bytesNumber -= sha1BufferFreeSpace;
            }

            Array.Copy(bytesBuffer, bytesProcessedNumber, sha1Buffer, sha1BufferLength, bytesNumber);
            sha1BufferLength += bytesNumber;

            return true;
        }

        private int ComputeMaxItemSpecLength(ITaskItem[] itemsToHash)
        {
            int maxItemSpecLength = 0;
            foreach (var item in itemsToHash)
            {
                if (item.ItemSpec.Length > maxItemSpecLength)
                {
                    maxItemSpecLength = item.ItemSpec.Length;
                }
            }

            return maxItemSpecLength;
        }
    }
}
