// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;

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
        private const string ItemSeparatorCharacter = "\u2028";

        /// <summary>
        /// Items from which to generate a hash.
        /// </summary>
        [Required]
        public ITaskItem[] ItemsToHash { get; set; }

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
                StringBuilder hashInput = new StringBuilder();

                foreach (var item in ItemsToHash)
                {
                    hashInput.Append(item.ItemSpec);
                    hashInput.Append(ItemSeparatorCharacter);
                }

                using (var sha1 = SHA1.Create())
                {
                    var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(hashInput.ToString()));
                    var hashResult = new StringBuilder(hash.Length*2);

                    foreach (byte b in hash)
                    {
                        hashResult.Append(b.ToString("x2"));
                    }

                    HashResult = hashResult.ToString();
                }
            }

            return true;
        }
    }
}
