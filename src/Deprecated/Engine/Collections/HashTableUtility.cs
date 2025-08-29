// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Tools for working with Hashtables.
    /// </summary>
    internal static class HashTableUtility
    {
        /// <summary>
        /// Compares the given hashtables.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="h1">May be null</param>
        /// <param name="h2">May be null</param>
        /// <returns>
        /// -1, if first hashtable is "less than" the second one
        ///  0, if hashtables have identical keys and equivalent (case-insensitive) values
        /// +1, if first hashtable is "greater than" the second one
        /// </returns>
        internal static int Compare(Dictionary<string, string> h1, Dictionary<string, string> h2)
        {
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
