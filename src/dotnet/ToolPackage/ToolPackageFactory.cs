// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolPackage
{
    internal static class ToolPackageFactory
    {
        public static (IToolPackageStore, IToolPackageInstaller) CreateToolPackageStoreAndInstaller(
            DirectoryPath? nonGlobalLocation = null,
            IEnumerable<string> additionalRestoreArguments = null)
        {
            IToolPackageStore toolPackageStore = CreateToolPackageStore(nonGlobalLocation);
            var toolPackageInstaller = new ToolPackageInstaller(
                toolPackageStore,
                new ProjectRestorer(additionalRestoreArguments: additionalRestoreArguments));

            return (toolPackageStore, toolPackageInstaller);
        }

        public static IToolPackageStore CreateToolPackageStore(
            DirectoryPath? nonGlobalLocation = null)
        {
            var toolPackageStore =
                new ToolPackageStore(nonGlobalLocation.HasValue
                ? new DirectoryPath(ToolPackageFolderPathCalculator.GetToolPackageFolderPath(nonGlobalLocation.Value.Value))
                : GetPackageLocation());

            return toolPackageStore;
        }

        private static DirectoryPath GetPackageLocation()
        {
            return new DirectoryPath(CliFolderPathCalculator.ToolsPackagePath);
        }
    }
}
