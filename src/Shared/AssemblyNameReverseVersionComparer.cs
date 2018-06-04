// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Compare the version numbers only for an AssemblyNameExtension and make sure they are in reverse order. This assumes the names are the same.
    /// </summary>
    sealed internal class AssemblyNameReverseVersionComparer : IComparer<AssemblyNameExtension>
    {
        /// <summary>
        /// A static instance of the comparer for use in a sort method
        /// </summary>
        internal readonly static IComparer<AssemblyNameExtension> GenericComparer = new AssemblyNameReverseVersionComparer();

        /// <summary>
        /// Compare x and y by version only.
        /// 
        /// Change the return value to sort the values in reverse order.
        /// 
        /// If x is greater than y  return -1 indicating x is less than y. 
        /// If x is less than y  return 1 indicating x is greater than  y.
        /// If x and y are equal return 0.
        /// </summary>
        public int Compare(AssemblyNameExtension x, AssemblyNameExtension y)
        {
            if (x != null || y != null)
            {
                if (y == null)
                {
                    // y should be lower than x in the sort. We need to indicate x is less than y in this case.
                    return -1;
                }
                else if (x == null)
                {
                    // y should be higher than x in the sort. We need to indicate x is greater than y in this case..
                    return 1;
                }
            }
            else
            {
                // They are both null
                return 0;
            }

            // We would like to compare the version numerically rather than alphabetically (because for example version 10.0.0. should above 9 not between 1 and 2)
            if (x.Version != y.Version)
            {
                if (y.Version == null)
                {
                    // y should be lower than x in the sort. We need to indicate x is less than y in this case.
                    return -1;
                }
                else if (x.Version == null)
                {
                    // y should be higher than x in the sort. We need to indicate x is greater than y in this case..
                    return 1;
                }
                else
                {
                    // Will not return 0 as the this != that check above takes care of the case where they are equal.
                    // If x is greater than y we want it to return -1, if x is less than y we want 1 to be returned.
                    int result = y.Version.CompareTo(x.Version);
                    return result;
                }
            }

            return 0;
        }
    }
}