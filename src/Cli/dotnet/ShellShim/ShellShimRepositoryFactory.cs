// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ShellShim
{
    internal static class ShellShimRepositoryFactory
    {
        public static IShellShimRepository CreateShellShimRepository(string appHostSourceDirectory, DirectoryPath? nonGlobalLocation = null)
        {
            return new ShellShimRepository(nonGlobalLocation ?? GetShimLocation(), appHostSourceDirectory);
        }

        private static DirectoryPath GetShimLocation()
        {
            return new DirectoryPath(CliFolderPathCalculator.ToolsShimPath);
        }
    }
}
