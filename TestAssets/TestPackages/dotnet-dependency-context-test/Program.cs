// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Extensions.DependencyModel;

namespace Microsoft.DotNet.Tools.DependencyContextTest
{
    public class Program
    {
        public static int Main(string[] args)
        {
            if(args.Length > 0 && args[0] == "--debug")
            {
                Console.WriteLine("Waiting for Debugger to attach, press ENTER to continue");
                Console.WriteLine($"Process ID: {System.Diagnostics.Process.GetCurrentProcess().Id}");
                Console.ReadLine();
            }

            if(DependencyContext.Default != null)
            {
                Console.WriteLine("DependencyContext.Default is set!");
            }
            else
            {
                Console.Error.WriteLine("DependencyContext.Default is NULL!");
                return 1;
            }

            if(DependencyContext.Default.RuntimeGraph.Any())
            {
                Console.WriteLine("DependencyContext.Default.RuntimeGraph has items!");
            }
            else
            {
                Console.WriteLine("DependencyContext.Default.RuntimeGraph is empty!");
                return 1;
            }

            Console.WriteLine("Tests succeeded!");
            return 0;
        }
    }
}
