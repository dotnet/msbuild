// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolPackage
{
    internal static class ToolPackageFactory
    {
        public static (IToolPackageStore, IToolPackageStoreQuery, IToolPackageInstaller) CreateToolPackageStoresAndInstaller(
            DirectoryPath? nonGlobalLocation = null,  IEnumerable<string> additionalRestoreArguments = null)
        {
            ToolPackageStoreAndQuery toolPackageStore = CreateConcreteToolPackageStore(nonGlobalLocation);
            var toolPackageInstaller = new ToolPackageInstaller(
                toolPackageStore,
                 new ProjectRestorer(additionalRestoreArguments: additionalRestoreArguments));

            return (toolPackageStore, toolPackageStore, toolPackageInstaller);
        }

        public static (IToolPackageStore, IToolPackageStoreQuery, IToolPackageUninstaller) CreateToolPackageStoresAndUninstaller(
            DirectoryPath? nonGlobalLocation = null)
        {
            ToolPackageStoreAndQuery toolPackageStore = CreateConcreteToolPackageStore(nonGlobalLocation);
            var toolPackageUninstaller = new ToolPackageUninstaller(
                toolPackageStore);

            return (toolPackageStore, toolPackageStore, toolPackageUninstaller);
        }

        public static (IToolPackageStore,
            IToolPackageStoreQuery,
            IToolPackageInstaller,
            IToolPackageUninstaller)
            CreateToolPackageStoresAndInstallerAndUninstaller(
                DirectoryPath? nonGlobalLocation = null, IEnumerable<string> additionalRestoreArguments = null)
        {
            ToolPackageStoreAndQuery toolPackageStore = CreateConcreteToolPackageStore(nonGlobalLocation);
            var toolPackageInstaller = new ToolPackageInstaller(
                toolPackageStore,
                new ProjectRestorer(additionalRestoreArguments: additionalRestoreArguments));
            var toolPackageUninstaller = new ToolPackageUninstaller(
                toolPackageStore);

            return (toolPackageStore, toolPackageStore, toolPackageInstaller, toolPackageUninstaller);
        }

        public static IToolPackageStoreQuery CreateToolPackageStoreQuery(
            DirectoryPath? nonGlobalLocation = null)
        {
            return CreateConcreteToolPackageStore(nonGlobalLocation);
        }

        private static DirectoryPath GetPackageLocation()
        {
            return new DirectoryPath(CliFolderPathCalculator.ToolsPackagePath);
        }

        private static ToolPackageStoreAndQuery CreateConcreteToolPackageStore(
            DirectoryPath? nonGlobalLocation = null)
        {
            var toolPackageStore =
                new ToolPackageStoreAndQuery(nonGlobalLocation.HasValue
                    ? new DirectoryPath(
                        ToolPackageFolderPathCalculator.GetToolPackageFolderPath(nonGlobalLocation.Value.Value))
                    : GetPackageLocation());

            return toolPackageStore;
        }
    }
}
