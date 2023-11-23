// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System;

namespace Microsoft.Build.Utilities
{
    internal static class FowlerNollVo1aHash
    {
        // Fowler/Noll/Vo hashing.
        // http://www.isthe.com/chongo/tech/comp/fnv/
        // https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function#FNV-1a_hash
        // http://www.isthe.com/chongo/src/fnv/hash_32a.c

        // 32 bit FNV prime and offset basis for FNV-1a.
        private const uint fnvPrimeA32Bit = 16777619;
        private const uint fnvOffsetBasisA32Bit = 2166136261;

        // 64 bit FNV prime and offset basis for FNV-1a.
        private const ulong fnvPrimeA64Bit = 1099511628211;
        private const ulong fnvOffsetBasisA64Bit = 14695981039346656037;

        /// <summary>
        /// Computes 32 bit Fowler/Noll/Vo-1a hash of a string (regardless of encoding).
        /// </summary>
        /// <param name="text">String to be hashed.</param>
        /// <returns>32 bit signed hash</returns>
        internal static int ComputeHash32(string text)
        {
            uint hash = fnvOffsetBasisA32Bit;

            ReadOnlySpan<byte> span = MemoryMarshal.Cast<char, byte>(text.AsSpan());
            foreach (byte b in span)
            {
                hash = unchecked((hash ^ b) * fnvPrimeA32Bit);
            }

            return unchecked((int)hash);
        }

        /// <summary>
        /// Computes 64 bit Fowler/Noll/Vo-1a hash optimized for ASCII strings.
        /// The hashing algorithm considers only the first 8 bits of each character.
        /// Analysis: https://github.com/KirillOsenkov/MSBuildStructuredLog/wiki/String-Hashing#faster-fnv-1a
        /// </summary>
        /// <param name="text">String to be hashed.</param>
        /// <returns>64 bit unsigned hash</returns>
        internal static ulong ComputeHash64Fast(string text)
        {
            ulong hash = fnvOffsetBasisA64Bit;

            unchecked
            {
                for (int i = 0; i < text.Length; i++)
                {
                    char ch = text[i];

                    hash = (hash ^ ch) * fnvPrimeA64Bit;
                }
            }

            return hash;
        }

        /// <summary>
        /// Computes 64 bit Fowler/Noll/Vo-1a hash of a string (regardless of encoding).
        /// </summary>
        /// <param name="text">String to be hashed.</param>
        /// <returns>64 bit unsigned hash</returns>
        internal static ulong ComputeHash64(string text)
        {
            ulong hash = fnvOffsetBasisA64Bit;

            ReadOnlySpan<byte> span = MemoryMarshal.Cast<char, byte>(text.AsSpan());
            foreach (byte b in span)
            {
                hash = unchecked((hash ^ b) * fnvPrimeA64Bit);
            }

            return hash;
        }

        internal static ulong Combine64(ulong left, ulong right)
        {
            unchecked
            {
                return (left ^ right) * fnvPrimeA64Bit;
            }
        }
    }
}
