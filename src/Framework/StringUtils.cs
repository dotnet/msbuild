﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography;

namespace Microsoft.Build.Framework;

internal static class StringUtils
{
    /// <summary>
    /// Generates a random string of the specified length.
    /// The generated string is suitable for use in file paths.
    /// The randomness distribution is given by the System.Security.Cryptography.RandomNumberGenerator.
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
    internal static string GenerateRandomString(int length)
    {
        // Base64, 2^6 = 64
        using var rng = RandomNumberGenerator.Create();

        const int eachStringCharEncodesBites = 6;
        const int eachByteHasBits = 8;
        const double bytesNumNeededForSingleStringChar = eachStringCharEncodesBites / (double)eachByteHasBits;

        int randomBytesNeeded = (int)Math.Ceiling(length * bytesNumNeededForSingleStringChar);
        byte[] randomBytes = new byte[randomBytesNeeded];
        rng.GetBytes(randomBytes);

        // Base64: [A-Z], [a-z], [0-9], +, /, =
        // We are replacing '/' to get a valid path
        string randomBase64String = Convert.ToBase64String(randomBytes).Replace('/', '_');
        return randomBase64String.Substring(0, length);
    }
}
