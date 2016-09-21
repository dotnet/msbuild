// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>
// A custom string comparer restricted to valid item/property names and with the
// ability to work on an indexed substring.
// </summary>
//-----------------------------------------------------------------------

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
#if FEATURE_BINARY_SERIALIZATION
    [Serializable]
#endif
    internal class MSBuildNameIgnoreCaseComparer : EqualityComparer<string>, IEqualityComparer<IKeyed>
    {
        /// <summary>
        /// The default immutable comparer instance operating on the whole string that can be used instead of creating once each time
        /// </summary>
        private static MSBuildNameIgnoreCaseComparer s_immutableComparer = new MSBuildNameIgnoreCaseComparer(true /* immutable */);

        /// <summary>
        /// The default mutable comparer instance that will ideally be shared by all users who need a mutable comparer. 
        /// </summary>
        private static MSBuildNameIgnoreCaseComparer s_mutableComparer = new MSBuildNameIgnoreCaseComparer(false /* mutable */);

        /// <summary>
        /// The processor architecture on which we are running, but default it will be x86
        /// </summary>
        private static NativeMethodsShared.ProcessorArchitectures s_runningProcessorArchitecture = NativeMethodsShared.ProcessorArchitectures.X86;

        /// <summary>
        /// Object used to lock the internal state s.t. we know that only one person is modifying
        /// it at any one time.  
        /// This is necessary to prevent, e.g., someone from reading the comparer (through GetHashCode when setting 
        /// a property, for example) at the same time that someone else is writing to it. 
        /// </summary>
        private Object lockObject = new Object();

        /// <summary>
        /// String to be constrained. 
        /// If null, comparer is unconstrained.
        /// If empty string, comparer is unconstrained and immutable.
        /// </summary>
        private string constraintString;

        /// <summary>
        /// Start of constraint
        /// </summary>
        private int startIndex;

        /// <summary>
        /// End of constraint
        /// </summary>
        private int endIndex;

        /// <summary>
        /// True if the comparer is immutable; false otherwise.
        /// </summary>
        private bool immutable;

        /// <summary>
        /// We need a static constructor to retrieve the running ProcessorArchitecture that way we can
        /// Avoid using optimized code that will not run correctly on IA64 due to alignment issues
        /// </summary>
        static MSBuildNameIgnoreCaseComparer()
        {
            s_runningProcessorArchitecture = NativeMethodsShared.ProcessorArchitecture;
        }

        /// <summary>
        /// Constructor. If specified, comparer is immutable and operates on the whole string.
        /// </summary>
        private MSBuildNameIgnoreCaseComparer(bool immutable)
        {
            this.immutable = immutable;
        }

        /// <summary>
        /// The default immutable comparer instance.
        /// </summary>
        internal static new MSBuildNameIgnoreCaseComparer Default
        {
            get { return s_immutableComparer; }
        }

        /// <summary>
        /// The default mutable comparer instance.
        /// </summary>
        internal static MSBuildNameIgnoreCaseComparer Mutable
        {
            get { return s_mutableComparer; }
        }

        /// <summary>
        /// Performs the "Equals" operation on two MSBuild property, item or metadata names
        /// </summary>
        public static bool Equals(string compareToString, string constrainedString, int start, int lengthToCompare)
        {
            if (Object.ReferenceEquals(compareToString, constrainedString))
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
                                int chx = (int)px[i];
                                int chy = (int)py[i + start];
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
        /// Given a set of constraints and a dictionary for which we are the comparer, return the value for the given key. 
        /// The key is also used as the string for the constraint. 
        /// </summary>
        /// <typeparam name="T">The value type of the dictionary being looked up</typeparam>
        public T GetValueWithConstraints<T>(IDictionary<string, T> dictionary, string key, int startIndex, int endIndex)
            where T : class
        {
            if (immutable)
            {
                ErrorUtilities.ThrowInternalError("immutable");
            }

            ErrorUtilities.VerifyThrowInternalNull(dictionary, "dictionary");

#if DEBUG
            // doing this rather than checking the strong type because otherwise, I would have to define T to be several other things 
            // (IKeyed, IValued, IImmutable, IEquatable<T>), some of which are not compiled into Microsoft.Build.Utilities, which also 
            // uses the MSBuildNameIgnoreCaseComparer. 
            ErrorUtilities.VerifyThrow(dictionary.GetType().Name.Contains("PropertyDictionary"), "Needs to be PropertyDictionary or CopyOnWritePropertyDictionary");
#endif
            if (startIndex < 0)
            {
                ErrorUtilities.ThrowInternalError("Invalid start index '{0}' {1} {2}", key, startIndex, endIndex);
            }

            if (key != null && (endIndex > key.Length || endIndex < startIndex))
            {
                ErrorUtilities.ThrowInternalError("Invalid end index '{0}' {1} {2}", key, startIndex, endIndex);
            }

            T returnValue;
            lock (lockObject)
            {
                constraintString = key;
                this.startIndex = startIndex;
                this.endIndex = endIndex;

                try
                {
                    returnValue = dictionary[key];
                }
                finally
                {
                    // Make sure we always reset the constraint
                    constraintString = null;
                    this.startIndex = 0;
                    this.endIndex = 0;
                }
            }

            return returnValue;
        }

        /// <summary>
        /// Compare keyed operands
        /// </summary>
        public bool Equals(IKeyed x, IKeyed y)
        {
            if (x == null && y == null)
            {
                return true;
            }
            else if (x == null || y == null)
            {
                return false;
            }

            return Equals(x.Key, y.Key);
        }

        /// <summary>
        /// Performs the "Equals" operation on two MSBuild property, item or metadata names
        /// </summary>
        public override bool Equals(string x, string y)
        {
            if (x == null && y == null)
            {
                return true;
            }
            else if (x == null || y == null)
            {
                return false;
            }

            string compareToString;
            string constrainedString;
            int start;
            int lengthToCompare;

            if (immutable)
            {
                // by definition we don't have a constraint
                if (Object.ReferenceEquals(x, y))
                {
                    return true;
                }

                compareToString = x;
                constrainedString = y;
                start = 0;
                lengthToCompare = y.Length;
            }
            else
            {
                lock (lockObject)
                {
                    if (constraintString != null)
                    {
                        bool constraintInX = Object.ReferenceEquals(x, constraintString);
                        bool constraintInY = Object.ReferenceEquals(y, constraintString);

                        if (!constraintInX && !constraintInY)
                        {
                            ErrorUtilities.ThrowInternalError("Expected to compare to constraint");
                        }

                        // Put constrained string in 'y', regular in 'x'
                        compareToString = constraintInX ? y : x;
                        constrainedString = constraintInY ? y : x;

                        start = startIndex;
                        lengthToCompare = endIndex - startIndex + 1;
                    }
                    else
                    {
                        if (Object.ReferenceEquals(x, y))
                        {
                            return true;
                        }

                        // Manually setup the "constraints" for the comparison
                        compareToString = x;
                        constrainedString = y;
                        start = 0;
                        lengthToCompare = y.Length;
                    }
                }
            }

            return Equals(compareToString, constrainedString, start, lengthToCompare);
        }

        /// <summary>
        /// Get case insensitive hashcode for key
        /// </summary>
        public int GetHashCode(IKeyed keyed)
        {
            if (keyed == null)
            {
                return 0; // per BCL convention
            }

            return GetHashCode(keyed.Key);
        }

        /// <summary>
        /// Getting a case insensitive hash code for the msbuild property, item or metadata name
        /// </summary>
        public override int GetHashCode(string obj)
        {
            if (obj == null)
            {
                return 0; // per BCL convention
            }

            int start = 0;
            int length = obj.Length;

            if (!immutable)
            {
                lock (lockObject)
                {
                    if (constraintString != null && Object.ReferenceEquals(obj, constraintString))
                    {
                        start = startIndex;
                        length = endIndex - startIndex + 1;
                    }
                }
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
                        int* pint = (int*)src2;

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

        /// <summary>
        /// Set the constraints in the comparer explicitly -- should ONLY be used for unit tests
        /// </summary>
        internal void SetConstraintsForUnitTestingOnly(string constraintString, int startIndex, int endIndex)
        {
            if (immutable)
            {
                ErrorUtilities.ThrowInternalError("immutable");
            }

            if (startIndex < 0)
            {
                ErrorUtilities.ThrowInternalError("Invalid start index '{0}' {1} {2}", constraintString, startIndex, endIndex);
            }

            if (constraintString != null && (endIndex > constraintString.Length || endIndex < startIndex))
            {
                ErrorUtilities.ThrowInternalError("Invalid end index '{0}' {1} {2}", constraintString, startIndex, endIndex);
            }

            lock (lockObject)
            {
                this.constraintString = constraintString;
                this.startIndex = startIndex;
                this.endIndex = endIndex;
            }
        }

        /// <summary>
        /// Companion to SetConstraintsForUnitTestingOnly -- makes the comparer unconstrained again. 
        /// </summary>
        internal void RemoveConstraintsForUnitTestingOnly()
        {
            if (immutable)
            {
                ErrorUtilities.ThrowInternalError("immutable");
            }

            lock (lockObject)
            {
                constraintString = null;
                startIndex = 0;
                endIndex = 0;
            }
        }
    }
}
