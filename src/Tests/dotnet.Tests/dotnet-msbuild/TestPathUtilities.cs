// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public static class TestPathUtilities
    {
        public static string FormatAbsolutePath(string directoryName = null)
            => Path.Combine(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "A:"
                    : Path.DirectorySeparatorChar.ToString(),
                directoryName ?? "TestWorkDir") + Path.DirectorySeparatorChar;
    }
}
