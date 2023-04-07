// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
