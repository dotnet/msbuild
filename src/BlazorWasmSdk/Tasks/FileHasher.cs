// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.NET.Sdk.BlazorWebAssembly
{
    public static class FileHasher
    {
        public static string GetFileHash(string filePath)
        {
            using var hash = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(filePath);
            var hashBytes = hash.ComputeHash(bytes);
            return ToBase36(hashBytes);
        }

        private static string ToBase36(byte[] hash)
        {
            const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";

            var result = new char[10];
            var dividend = BigInteger.Abs(new BigInteger(hash.AsSpan().Slice(0, 9).ToArray()));
            for (var i = 0; i < 10; i++)
            {
                dividend = BigInteger.DivRem(dividend, 36, out var remainder);
                result[i] = chars[(int)remainder];
            }

            return new string(result);
        }
    }
}
