// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
#if !SILVERLIGHT
#if FEATURE_CONSTRAINED_EXECUTION
using System.Runtime.ConstrainedExecution;
#endif
#endif

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// Duplicated because internal to mscorlib
    /// </summary>
    internal static class HashHelpers
    {
        // Table of prime numbers to use as hash table sizes. 
        // The entry used for capacity is the smallest prime number in this array
        // that is larger than twice the previous capacity. 

        internal static readonly int[] primes = {
            3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
            1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
            17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
            187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
            1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369};

#if !SILVERLIGHT
#if FEATURE_CONSTRAINED_EXECUTION
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
#endif
#endif
        internal static bool IsPrime(int candidate)
        {
            if ((candidate & 1) != 0)
            {
                int limit = (int)Math.Sqrt(candidate);
                for (int divisor = 3; divisor <= limit; divisor += 2)
                {
                    if ((candidate % divisor) == 0)
                    {
                        return false;
                    }
                }
                return true;
            }
            return candidate == 2;
        }

#if !SILVERLIGHT
#if FEATURE_CONSTRAINED_EXECUTION
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
#endif
#endif
        internal static int GetPrime(int min)
        {
            Debug.Assert(min >= 0, "min less than zero; handle overflow checking before calling HashHelpers");

            for (int i = 0; i < primes.Length; i++)
            {
                int prime = primes[i];
                if (prime >= min)
                {
                    return prime;
                }
            }

            // Outside of our predefined table. Compute the hard way. 
            for (int i = (min | 1); i < Int32.MaxValue; i += 2)
            {
                if (IsPrime(i))
                {
                    return i;
                }
            }
            return min;
        }

        internal static int GetMinPrime()
        {
            return primes[0];
        }

        // Returns size of hashtable to grow to.
        internal static int ExpandPrime(int oldSize)
        {
            int newSize = 2 * oldSize;

            // Allow the hashtables to grow to maximum possible size (~2G elements) before encoutering capacity overflow.
            // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
            if ((uint) newSize > MaxPrimeArrayLength)
            {
                return MaxPrimeArrayLength;
            }

            return GetPrime(newSize);
        }

        // This is the maximum prime smaller than Array.MaxArrayLength
        internal const int MaxPrimeArrayLength = 0x7FEFFFFD;
    }
}
