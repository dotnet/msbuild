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
    /// <remarks>
    /// Currently uses SHA1. Implementation subject to change between MSBuild versions. Not
    /// intended as a cryptographic security measure, only uniqueness between build executions.
    /// </remarks>
    /// </summary>
    public class Hash : TaskExtension
    {
        private const char ItemSeparatorCharacter = '\u2028';

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
        public override bool Execute()
        {
            if (ItemsToHash != null && ItemsToHash.Length > 0)
            {
                using (var sha1 = SHA1.Create())
                {
                    var concatenatedItemStringSize = ComputeStringSize(ItemsToHash);

                    var hashStringSize = sha1.HashSize;

                    using (var stringBuilder = new ReuseableStringBuilder(Math.Max(concatenatedItemStringSize, hashStringSize)))
                    {
                        foreach (var item in ItemsToHash)
                        {
                            string itemSpec = item.ItemSpec;
                            stringBuilder.Append(IgnoreCase ? itemSpec.ToUpperInvariant() : itemSpec);
                            stringBuilder.Append(ItemSeparatorCharacter);
                        }

                        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(stringBuilder.ToString()));

                        stringBuilder.Clear();

                        foreach (var b in hash)
                        {
                            stringBuilder.Append(b.ToString("x2"));
                        }

                        HashResult = stringBuilder.ToString();
                    }
                }
            }

            return true;
        }

        private int ComputeStringSize(ITaskItem[] itemsToHash)
        {
            if (itemsToHash.Length == 0)
            {
                return 0;
            }

            var totalItemSize = 0;

            foreach (var item in itemsToHash)
            {
                totalItemSize += item.ItemSpec.Length;
            }

            var separatorSize = itemsToHash.Length - 1;

            return totalItemSize + separatorSize;
        }
    }
}
