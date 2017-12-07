// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Reflection;

namespace TestApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine(TestLibrary.Helper.GetMessage());
            VerifySatelliteAssemblies();
        }

        public static void VerifySatelliteAssemblies()
        {
            PrintCultureResources();
            PrintCultureResourcesInFolder();
        }

        public static void PrintCultureResources()
        {
            var rm = new ResourceManager("TestApp.Strings", typeof(Program).GetTypeInfo().Assembly);

            string[] cultures = new string[] { "", "de", "fr" };

            foreach (var culture in cultures)
            {
                Console.WriteLine(rm.GetString("hello", new CultureInfo(culture)));
            }
        }

        public static void PrintCultureResourcesInFolder()
        {
            var rm = new ResourceManager("TestApp.FolderWithResource.Strings", typeof(Program).GetTypeInfo().Assembly);
            Console.WriteLine(rm.GetString("hello", new CultureInfo("da")));
        }
    }
}
