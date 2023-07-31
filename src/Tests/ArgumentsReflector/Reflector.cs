// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
