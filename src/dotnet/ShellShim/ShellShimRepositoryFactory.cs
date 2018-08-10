// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ShellShim
{
    internal static class ShellShimRepositoryFactory
    {
        public static IShellShimRepository CreateShellShimRepository(DirectoryPath? nonGlobalLocation = null)
        {
            return new ShellShimRepository(nonGlobalLocation ?? GetShimLocation());
        }

        private static DirectoryPath GetShimLocation()
        {
            var cliFolderPathCalculator = new CliFolderPathCalculator();
            return new DirectoryPath(cliFolderPathCalculator.ToolsShimPath);
        }
    }
}
