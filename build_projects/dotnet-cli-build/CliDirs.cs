using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.InternalAbstractions;

namespace Microsoft.DotNet.Cli.Build
{
    public static class CliDirs
    {
        public static readonly string CoreSetupDownload = Path.Combine(
            Dirs.Intermediate, 
            "coreSetupDownload", 
            CliDependencyVersions.SharedFrameworkVersion);
    }
}
