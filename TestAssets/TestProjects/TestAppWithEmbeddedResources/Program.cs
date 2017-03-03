// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reflection;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var thisAssembly = typeof(Program).GetTypeInfo().Assembly;
            var resources = from resourceName in thisAssembly.GetManifestResourceNames()
                            select resourceName;

            if (resources.Count() > 1)
            {
                throw new Exception($"{resources.Count()} found in the assembly. Was expecting only 1.");
            }

            var resourceNames = string.Join(",", resources);
            Console.WriteLine($"{resources.Count()} Resources Found: {resourceNames}");
        }
    }
}
