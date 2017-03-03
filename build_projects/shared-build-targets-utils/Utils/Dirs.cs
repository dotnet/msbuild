// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet.Cli.Build
{
    public static class Dirs
    {
        public static readonly string RepoRoot = Directory.GetCurrentDirectory();
        public static readonly string Output = Path.Combine(
            RepoRoot,
            "artifacts",
            RuntimeEnvironment.GetRuntimeIdentifier());
        public static readonly string Packages = Path.Combine(Output, "packages");
    }
}
