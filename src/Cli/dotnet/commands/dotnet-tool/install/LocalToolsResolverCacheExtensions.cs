// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Tool.Install
{
    internal static class LocalToolsResolverCacheExtensions
    {
        public static void SaveToolPackage(
            this ILocalToolsResolverCache localToolsResolverCache,
            IToolPackage toolDownloadedPackage,
            string targetFrameworkToInstall)
        {
            if (localToolsResolverCache == null)
            {
                throw new ArgumentNullException(nameof(localToolsResolverCache));
            }

            if (toolDownloadedPackage == null)
            {
                throw new ArgumentNullException(nameof(toolDownloadedPackage));
            }

            if (string.IsNullOrWhiteSpace(targetFrameworkToInstall))
            {
                throw new ArgumentException("targetFrameworkToInstall cannot be null or whitespace",
                    nameof(targetFrameworkToInstall));
            }

            foreach (var restoredCommand in toolDownloadedPackage.Commands)
            {
                localToolsResolverCache.Save(
                    new Dictionary<RestoredCommandIdentifier, RestoredCommand>
                    {
                        [new RestoredCommandIdentifier(
                                toolDownloadedPackage.Id,
                                toolDownloadedPackage.Version,
                                NuGetFramework.Parse(targetFrameworkToInstall),
                                Constants.AnyRid,
                                restoredCommand.Name)] =
                            restoredCommand
                    });
            }
        }
    }
}
