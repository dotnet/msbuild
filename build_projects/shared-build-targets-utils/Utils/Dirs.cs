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
