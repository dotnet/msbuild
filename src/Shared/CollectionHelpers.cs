// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Utilities for collections
    /// </summary>
    internal static class CollectionHelpers
    {
        /// <summary>
        /// Returns a new list containing the input list
        /// contents, except for nulls
        /// </summary>
        /// <typeparam name="T">Type of list elements</typeparam>
        internal static List<T> RemoveNulls<T>(List<T> inputs)
        {
            List<T> inputsWithoutNulls = new List<T>(inputs.Count);

            foreach (T entry in inputs)
            {
                if (entry != null)
                {
                    inputsWithoutNulls.Add(entry);
                }
            }

            // Avoid possibly having two identical lists floating around
            return (inputsWithoutNulls.Count == inputs.Count) ? inputs : inputsWithoutNulls;
        }

        /// <summary>
        /// Extension method -- combines a TryGet with a check to see that the value is equal. 
        /// </summary>
        internal static bool ContainsValueAndIsEqual(this Dictionary<string, string> dictionary, string key, string value, StringComparison comparer)
        {
            string valueFromDictionary;
            if (dictionary.TryGetValue(key, out valueFromDictionary))
            {
                return String.Equals(value, valueFromDictionary, comparer);
            }

            return false;
        }
    }
}
