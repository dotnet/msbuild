// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello Portable World!");

            var depsFile = new FileInfo(AppContext.GetData("FX_DEPS_FILE") as string);
            string frameworkVersion = depsFile.Directory.Name;

            Console.WriteLine($"I'm running on shared framework version {frameworkVersion}!");
        }
    }
}
