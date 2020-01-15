// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;

public static class AssertionHelper
{
    public static string[] AppendApphostOnNonMacOS(string ProjectName, string[] expectedFiles)
    {
        string apphost = $"{ProjectName}{Constants.ExeSuffix}";
        // No UseApphost is false by default on macOS
        return RuntimeEnvironment.OperatingSystemPlatform != Platform.Darwin
            ? expectedFiles.Append(apphost).ToArray()
            : expectedFiles;
    }
}
