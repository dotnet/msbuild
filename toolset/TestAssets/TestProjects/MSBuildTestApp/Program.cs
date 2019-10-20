// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MSBuildTestApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                Console.WriteLine("echo args:" + string.Join(";", args));
            }
            Console.WriteLine("Hello World!");
        }
    }
}
