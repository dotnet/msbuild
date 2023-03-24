// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

internal static class ToolsetUtils
{
    /// <summary>
    /// Gets path to RuntimeIdentifierGraph.json file.
    /// </summary>
    /// <returns></returns>
    internal static string GetRuntimeGraphFilePath()
    {
        string dotnetRoot = TestContext.Current.ToolsetUnderTest.DotNetRoot;

        DirectoryInfo sdksDir = new(Path.Combine(dotnetRoot, "sdk"));

        var lastWrittenSdk = sdksDir.EnumerateDirectories().OrderByDescending(di => di.LastWriteTime).First();

        return lastWrittenSdk.GetFiles("RuntimeIdentifierGraph.json").Single().FullName;
    }
}
