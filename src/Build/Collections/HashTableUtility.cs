// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// Tools for working with Hashtables.
    /// </summary>
    internal static class HashTableUtility
    {
        /// <summary>
        /// Compares the given hashtables.
        /// </summary>
        /// <param name="h1">May be null</param>
        /// <param name="h2">May be null</param>
        /// <returns>
        /// -1, if first hashtable is "less than" the second one
        ///  0, if hashtables have identical keys and equivalent (case-insensitive) values
        /// +1, if first hashtable is "greater than" the second one
        /// </returns>
        internal static int Compare(Dictionary<string, string> h1, Dictionary<string, string> h2)
        {
            // NOTE: These are deliberately typed as Dictionary<TKey, TValue> and not
            // IDictionary<TKey, TValue> to avoid boxing the enumerator

            if (h1 == h2) // eg null
            {
                return 0;
            }
            else if (h1 == null)
            {
                return -1;
            }
            else if (h2 == null)
            {
                return +1;
            }

            int comparison = Math.Sign(h1.Count - h2.Count);

            if (comparison == 0)
            {
                foreach (KeyValuePair<string, string> h1Entry in h1)
                {
                    // NOTE: String.Compare() allows null values -- any string,
                    // including the empty string (""), compares greater than a
                    // null reference, and two null references compare equal
                    comparison = String.Compare(h1Entry.Value, h2[h1Entry.Key],
                        StringComparison.OrdinalIgnoreCase);

                    if (comparison != 0)
                    {
                        break;
                    }
                }
            }

            return comparison;
        }
    }
}
