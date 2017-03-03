// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Tests.ArgumentForwarding
{
    public class Program
    {
        public static void Main(string[] args)
        {
            bool first=true;
            foreach (var arg in args)
            {
                if (first)
                {
                    first=false;
                }
                else
                {
                    Console.Write(",");
                }
                Console.Write(arg);
            }
        }
    }
}
