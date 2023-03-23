// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;

namespace ConsoleApplication
{
    public class BrowserLaunchApp
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Started");

            // Simulates an ASP.NET Core app.
            Console.WriteLine("info: Microsoft.Hosting.Lifetime[0]");
            Console.WriteLine("      Now listening on: https://localhost:5001");
            Console.WriteLine("info: Microsoft.Hosting.Lifetime[0]");
            Console.WriteLine("      Now listening on: http://localhost:5000");
            Console.WriteLine("info: Microsoft.Hosting.Lifetime[0]");
            Console.WriteLine("      Application started. Press Ctrl+C to shut down.");
            Console.WriteLine("info: Microsoft.Hosting.Lifetime[0]");
            Console.WriteLine("      Hosting environment: Development");
            Console.WriteLine("info: Microsoft.Hosting.Lifetime[0]");
            Console.WriteLine($"      Content root path: {Directory.GetCurrentDirectory()}");

            Thread.Sleep(Timeout.Infinite);
        }
    }
}