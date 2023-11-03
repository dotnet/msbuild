// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.Build.Evaluation
{
    internal static class FowlerNollVo1aHash
    {
        // Fowler/Noll/Vo hashing.
        // http://www.isthe.com/chongo/tech/comp/fnv/
        // https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function#FNV-1a_hash
        // http://www.isthe.com/chongo/src/fnv/hash_32a.c

        // 32 bit FNV prime and offset basis for FNV-1a.
        private const uint fnvPrimeA = 16777619;
        private const uint fnvOffsetBasisA = 2166136261;

        /// <summary>
        /// Computes 32 bit Fowler/Noll/Vo-1a hash of a UTF8 decoded string.
        /// </summary>
        /// <param name="text">String to be hashed.</param>
        /// <returns>32 bit signed hash</returns>
        internal static int ComputeHash(string text)
        {
            uint hash = fnvOffsetBasisA;

            // We want this to be stable across platforms, so we need to use UTF8 encoding.
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                unchecked
                {
                    hash ^= b;
                    hash *= fnvPrimeA;
                }
            }

            return unchecked((int)hash);
        }
    }
}
