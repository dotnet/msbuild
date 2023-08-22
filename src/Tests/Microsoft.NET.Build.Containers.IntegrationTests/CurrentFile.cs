// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public static class CurrentFile
{
    public static string Path([CallerFilePath] string file = "") => file;

    public static string Relative(string relative, [CallerFilePath] string file = "")
    {
        return global::System.IO.Path.Combine(global::System.IO.Path.GetDirectoryName(file)!, relative); // file known to be not-null due to the mechanics of CallerFilePath
    }
}
