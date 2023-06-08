// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Utilities;

/// <summary>
/// Facilitates pooling of strings constructed from <c>ReadOnlySpan&lt;char&gt;</c> values,
/// providing zero-allocation lookup for strings that have been seen before.
/// </summary>
/// <remarks>
/// Uses ordinal string comparison.
/// </remarks>
internal sealed class StringPool
{
    private int[]? _buckets;
    private Slot[]? _slots;
    private int _count;

    /// <summary>
    /// Returns a string containing the content of <paramref name="span"/>.
    /// If this <see cref="StringPool"/> has seen such a string before,
    /// the previously returned instance is returned again. Otherwise, a string
    /// representing the character span is constructed and cached for future usage.
    /// </summary>
    /// <remarks>
    /// Not thread safe.
    /// </remarks>
    /// <param name="span">The span of characters to return a string for.</param>
    /// <returns>A string containing the characters in <paramref name="span"/>, from an internal cache of strings where possible.</returns>
    public string Intern(ReadOnlySpan<char> span)
    {
        if (_buckets is null)
        {
            const int initialSize = 3; // must be prime
            _buckets = new int[initialSize];
            _slots = new Slot[initialSize];
        }

        int hashCode = InternalGetHashCode(span);

        int bucketIndex = hashCode % _buckets.Length;

        // Search for an existing entry.
        for (int probeIndex = _buckets[hashCode % _buckets.Length] - 1; probeIndex >= 0;)
        {
            ref Slot slot = ref _slots![probeIndex];

            if (slot.HashCode == hashCode && InternalEquals(slot.Value, span))
            {
                // Found a match! Return it.
                return slot.Value;
            }

            // Follow the chain to find the next item.
            probeIndex = slot.Next;
        }

        // We will add a new entry.
        // Resize our storage if needed.
        if (_count == _slots!.Length)
        {
            IncreaseCapacity();
            bucketIndex = hashCode % _buckets.Length;
        }

        // Add the new entry in the last slot.
        int slotIndex = _count++;

        // Materialize a string for the span.
        string str = span.ToString();

        // Store the string.
        _slots[slotIndex].HashCode = hashCode;
        _slots[slotIndex].Value = str;
        _slots[slotIndex].Next = _buckets[bucketIndex] - 1;
        _buckets[bucketIndex] = slotIndex + 1;

        // Return the newly created string.
        return str;

        void IncreaseCapacity()
        {
            int newSize = HashHelpers.ExpandPrime(_count);

            if (newSize <= _count)
            {
                throw new OverflowException("StringPool size overflowed.");
            }

            Slot[] newSlots = new Slot[newSize];

            if (_slots != null)
            {
                Array.Copy(_slots, 0, newSlots, 0, _count);
            }

            int[] newBuckets = new int[newSize];

            for (int i = 0; i < _count; i++)
            {
                int num = newSlots[i].HashCode % newSize;
                newSlots[i].Next = newBuckets[num] - 1;
                newBuckets[num] = i + 1;
            }

            _slots = newSlots;
            _buckets = newBuckets;
        }
    }

    /// <summary>
    /// Determines whether the provided string has equivalent content to the characters
    /// in the provided span, using ordinal comparison.
    /// </summary>
    internal static unsafe bool InternalEquals(string str, ReadOnlySpan<char> span)
    {
        if (str.Length != span.Length)
        {
            return false;
        }

        // Walk both the string and the span
        fixed (char* pStr0 = str)
        {
            fixed (char* pSpan0 = span)
            {
                // Reinterpret the characters (16 bit) as int (32 bit) so that we can
                // compare two at a time per operation, for performance.
                int* pStr = (int*)pStr0;
                int* pSpan = (int*)pSpan0;

                int charactersRemaining;

                // Walk through the string, checking four characters at a time (two ints, 64 bits).
                for (charactersRemaining = span.Length; charactersRemaining >= 4; charactersRemaining -= 4)
                {
                    if (*pStr != *pSpan || pStr[1] != pSpan[1])
                    {
                        return false;
                    }

                    pStr += 2;
                    pSpan += 2;
                }

                if (charactersRemaining > 1)
                {
                    // There are at least two characters remaining, so use our int pointers again.
                    if (*pStr != *pSpan)
                    {
                        return false;
                    }

                    charactersRemaining -= 2;
                }

                if (charactersRemaining == 1)
                {
                    // Check the last character in the string
                    int lastCharacterIndex = str.Length - 1;
                    if (pStr0[lastCharacterIndex] != pSpan0[lastCharacterIndex])
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Gets a hash code for the characters present in <paramref name="span"/>.
    /// </summary>
    internal static unsafe int InternalGetHashCode(ReadOnlySpan<char> span)
    {
        if (span.Length == 0)
        {
            return 0;
        }

        fixed (char* pSpan0 = span)
        {
            int num1 = 0x15051505;
            int num2 = num1;

            int* pSpan = (int*)pSpan0;

            int charactersRemaining;

            for (charactersRemaining = span.Length; charactersRemaining >= 4; charactersRemaining -= 4)
            {
                num1 = ((num1 << 5) + num1 + (num1 >> 27)) ^ *pSpan;
                num2 = ((num2 << 5) + num2 + (num2 >> 27)) ^ pSpan[1];
                pSpan += 2;
            }

            if (charactersRemaining > 0)
            {
                num1 = ((num1 << 5) + num1 + (num1 >> 27)) ^ pSpan0[span.Length - 1];
            }

            return (num1 + (num2 * 0x5D588B65)) & 0x7FFFFFFF;
        }
    }

    /// <summary>Models an entry in our hash table.</summary>
    internal struct Slot
    {
        public int HashCode;
        public int Next;
        public string Value;
    }

    private static class HashHelpers
    {
        public static readonly int[] Primes =
        {
            3, 7, 11, 17, 23, 29, 37, 47, 59, 71,
            89, 107, 131, 163, 197, 239, 293, 353, 431, 521,
            631, 761, 919, 1103, 1327, 1597, 1931, 2333, 2801, 3371,
            4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591, 17519, 21023,
            25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363,
            156437, 187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403,
            968897, 1162687, 1395263, 1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559,
            5999471, 7199369
        };

        public static int ExpandPrime(int oldSize)
        {
            // Start by doubling the size.
            int num = 2 * oldSize;

            if ((uint)num > 0x7FEFFFFDu && oldSize < 0x7FEFFFFDu)
            {
                // If we overflowed int32, cap it.
                return 0x7FEFFFFD;
            }

            return GetPrimeGreaterThan(num);

            static int GetPrimeGreaterThan(int min)
            {
                if (min < 0)
                {
                    throw new OverflowException("StringPool size overflowed.");
                }

                for (int i = 0; i < Primes.Length; i++)
                {
                    int prime = Primes[i];
                    if (prime >= min)
                    {
                        return prime;
                    }
                }

                // If we get here, we're searching for a number greater than 7199369 which
                // isn't something we have cached. Work it out by brute force.
                for (int j = min | 1; j < int.MaxValue; j += 2)
                {
                    if ((j - 1) % 101 != 0 && IsPrime(j))
                    {
                        return j;
                    }
                }

                return min;

                static bool IsPrime(int candidate)
                {
                    if (((uint)candidate & (true ? 1u : 0u)) != 0)
                    {
                        int num = (int)Math.Sqrt(candidate);

                        for (int i = 3; i <= num; i += 2)
                        {
                            if (candidate % i == 0)
                            {
                                return false;
                            }
                        }

                        return true;
                    }

                    return candidate == 2;
                }
            }
        }
    }
}
