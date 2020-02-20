// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;

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
