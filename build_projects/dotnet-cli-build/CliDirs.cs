// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
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
