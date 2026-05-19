// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Shared
{
    internal static class StringUtils
    {
        /// <summary>
        /// Generates a random string of the specified length.
        /// The generated string is suitable for use in file paths.
        /// The randomness distribution is given by the System.Random.
        /// </summary>
        /// <param name="length">The length of the string.</param>
        /// <returns>Random generated string of the specified length.</returns>
        public static string GenerateRandomString(int length)
        {
#if NET
            return string.Create(length, 0, static (dest, _) =>
                Random.Shared.GetItems("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+_", dest));
#else
            // Base64, 2^6 = 64
            const int eachStringCharEncodesBites = 6;
            const int eachByteHasBits = 8;
            const double bytesNumNeededForSingleStringChar = eachStringCharEncodesBites / (double)eachByteHasBits;

            int randomBytesNeeded = (int)Math.Ceiling(length * bytesNumNeededForSingleStringChar);
            Random random = new();

            byte[] randomBytes = new byte[randomBytesNeeded];
            random.NextBytes(randomBytes);

            // Base64: [A-Z], [a-z], [0-9], +, /, =
            // We are replacing '/' to get a valid path
            string randomBase64String = Convert.ToBase64String(randomBytes).Replace('/', '_');

            return randomBase64String.Substring(0, length);
#endif
        }

        /// <summary>
        /// Removes last occurrence of <paramref name="substring"/> from <paramref name="fromString"/>, if present.
        /// </summary>
        /// <param name="fromString">String to be altered.</param>
        /// <param name="substring">String to be removed.</param>
        /// <param name="comparison">The comparison to use for finding.</param>
        /// <returns>The original string (if no occurrences found) or a new string, with last instance of <paramref name="substring"/> removed.</returns>
        public static string RemoveLastInstanceOf(this string fromString, string substring, StringComparison comparison = StringComparison.Ordinal)
        {
            int lastOccurrenceIndex = fromString.LastIndexOf(substring, comparison);

            if (lastOccurrenceIndex >= 0)
            {
                fromString =
#if NET
                    $"{fromString.AsSpan(0, lastOccurrenceIndex)}{fromString.AsSpan(lastOccurrenceIndex + substring.Length)}";
#else
                    $"{fromString.Substring(0, lastOccurrenceIndex)}{fromString.Substring(lastOccurrenceIndex + substring.Length)}";
#endif
            }

            return fromString;
        }
    }
}
