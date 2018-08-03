// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// This is a custom string comparer that has three advantages over the regular
    /// string comparer:
    /// 1) It can generate hash codes and perform equivalence operations on parts of a string rather than a whole
    /// 2) It uses "unsafe" pointers to maximize performance of those operations
    /// 3) It takes advantage of limitations on MSBuild Property/Item names to cheaply do case insensitive comparison.
    /// </summary>
    [Serializable]
    internal class MSBuildNameIgnoreCaseComparer : IConstrainedEqualityComparer<string>, IEqualityComparer<string>
    {
        /// <summary>
        /// The processor architecture on which we are running, but default it will be x86
        /// </summary>
        private static readonly NativeMethodsShared.ProcessorArchitectures s_runningProcessorArchitecture;

        /// <summary>
        /// We need a static constructor to retrieve the running ProcessorArchitecture that way we can
        /// avoid using optimized code that will not run correctly on IA64 due to alignment issues
        /// </summary>
        static MSBuildNameIgnoreCaseComparer()
        {
            s_runningProcessorArchitecture = NativeMethodsShared.ProcessorArchitecture;
        }

        /// <summary>
        /// The default immutable comparer instance.
        /// </summary>
        internal static MSBuildNameIgnoreCaseComparer Default { get; } = new MSBuildNameIgnoreCaseComparer();

        public bool Equals(string x, string y)
        {
            return Equals(x, y, 0, y?.Length ?? 0);
        }

        public int GetHashCode(string obj)
        {
            return GetHashCode(obj, 0, obj?.Length ?? 0);
        }

        /// <summary>
        /// Performs the "Equals" operation on two MSBuild property, item or metadata names
        /// </summary>
        public bool Equals(string compareToString, string constrainedString, int start, int lengthToCompare)
        {
            if (lengthToCompare < 0)
            {
                ErrorUtilities.ThrowInternalError("Invalid lengthToCompare '{0}' {1} {2}", constrainedString, start, lengthToCompare);
            }

            if (start < 0 || start > (constrainedString?.Length ?? 0) - lengthToCompare)
            {
                ErrorUtilities.ThrowInternalError("Invalid start '{0}' {1} {2}", constrainedString, start, lengthToCompare);
            }

            if (ReferenceEquals(compareToString, constrainedString))
            {
                return true;
            }

            if (compareToString == null || constrainedString == null)
            {
                return false;
            }

            if (lengthToCompare != compareToString.Length)
            {
                return false;
            }

            if ((s_runningProcessorArchitecture != NativeMethodsShared.ProcessorArchitectures.IA64)
                && (s_runningProcessorArchitecture != NativeMethodsShared.ProcessorArchitectures.ARM))
            {
                // The use of unsafe here is quite a bit faster than the regular
                // mechanism in the BCL. This is because we can make assumptions
                // about the characters that are within the strings being compared
                // i.e. they are valid MSBuild property, item and metadata names
                unsafe
                {
                    fixed (char* px = compareToString)
                    {
                        fixed (char* py = constrainedString)
                        {
                            for (int i = 0; i < compareToString.Length; i++)
                            {
                                int chx = px[i];
                                int chy = py[i + start];
                                chx = chx & 0x00DF; // Extract the uppercase character
                                chy = chy & 0x00DF; // Extract the uppercase character

                                if (chx != chy)
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                return String.Compare(compareToString, 0, constrainedString, start, lengthToCompare, StringComparison.OrdinalIgnoreCase) == 0;
            }

            return true;
        }

        /// <summary>
        /// Getting a case insensitive hash code for the msbuild property, item or metadata name
        /// </summary>
        public int GetHashCode(string obj, int start, int length)
        {
            if (obj == null)
            {
                return 0; // per BCL convention
            }

            if ((s_runningProcessorArchitecture != NativeMethodsShared.ProcessorArchitectures.IA64)
                && (s_runningProcessorArchitecture != NativeMethodsShared.ProcessorArchitectures.ARM))
            {
                unsafe
                {
                    // This algorithm is based on the 32bit version from the CLR's string::GetHashCode
                    fixed (char* src = obj)
                    {
                        int hash1 = (5381 << 16) + 5381;

                        int hash2 = hash1;

                        char* src2 = src + start;
                        var pint = (int*)src2;

                        while (length > 0)
                        {
                            // We're only interested in uppercase ASCII characters
                            int val = pint[0] & 0x00DF00DF;

                            // When we reach the end of the string, we need to
                            // stop short when gathering our data to compute the
                            // hash code - we are only interested in the data within
                            // the string, and not the null terminator etc.
                            if (length == 1)
                            {
                                val = val & 0xFFFF;
                            }

                            hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ val;
                            if (length <= 2)
                            {
                                break;
                            }

                            // Once again we're only interested in the uppercase ASCII characters
                            val = pint[1] & 0x00DF00DF;
                            if (length == 3)
                            {
                                val = val & 0xFFFF;
                            }

                            hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ val;
                            pint += 2;
                            length -= 4;
                        }

                        return hash1 + (hash2 * 1566083941);
                    }
                }
            }
            else
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Substring(start, length));
            }
        }
    }
}
