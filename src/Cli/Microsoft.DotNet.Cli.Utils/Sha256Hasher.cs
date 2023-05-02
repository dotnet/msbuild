// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

#if NET

using System;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class Sha256Hasher
    {
        /// <summary>
        /// The hashed mac address needs to be the same hashed value as produced by the other distinct sources given the same input. (e.g. VsCode)
        /// </summary>
        public static string Hash(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public static string HashWithNormalizedCasing(string text)
        {
            return Hash(text.ToUpperInvariant());
        }
    }
}
#endif
