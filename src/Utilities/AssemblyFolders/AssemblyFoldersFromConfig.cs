// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.AssemblyFoldersFromConfig
{
    internal sealed class AssemblyFoldersFromConfig : Microsoft.Build.Shared.AssemblyFoldersFromConfig.AssemblyFoldersFromConfig<AssemblyFoldersFromConfigInfo>
    {
        internal AssemblyFoldersFromConfig(string configFile, string targetRuntimeVersion, System.Reflection.ProcessorArchitecture targetArchitecture)
            : base(
                configFile,
                targetRuntimeVersion,
                targetArchitecture,
                static (path, version) => new AssemblyFoldersFromConfigInfo(path, version))
        {
        }
    }
}
