// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;

var assembly = typeof(C).Assembly;

Console.WriteLine("Started");

// Process ID is insufficient because PID's may be reused.
Console.WriteLine($"Process identifier = {Process.GetCurrentProcess().Id}, {Process.GetCurrentProcess().StartTime:hh:mm:ss.FF}");
Console.WriteLine($"DOTNET_WATCH = {Environment.GetEnvironmentVariable("DOTNET_WATCH")}");
Console.WriteLine($"DOTNET_WATCH_ITERATION = {Environment.GetEnvironmentVariable("DOTNET_WATCH_ITERATION")}");
Console.WriteLine($"Arguments = {string.Join(",", args)}");
Console.WriteLine($"AssemblyName = {assembly.GetName()}");
Console.WriteLine($"AssemblyTitle = '{assembly.GetCustomAttributes<AssemblyTitleAttribute>().FirstOrDefault()?.Title ?? "<unspecified>"}'");
Console.WriteLine($"TFM = {assembly.GetCustomAttributes<TargetFrameworkAttribute>().FirstOrDefault()?.FrameworkName ?? "<unspecified>"}");
Console.WriteLine($"Configuration = {assembly.GetCustomAttributes<AssemblyConfigurationAttribute>().FirstOrDefault()?.Configuration ?? "<unspecified>"}");

Loop();

static void Loop()
{
    while (true)
    {
        Console.WriteLine(".");
        Thread.Sleep(1000);
    }
}

class C { }
