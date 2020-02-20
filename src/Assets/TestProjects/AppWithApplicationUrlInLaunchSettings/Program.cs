// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MSBuildTestApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var message = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
            Console.WriteLine(message);
        }
    }
}
