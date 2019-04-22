// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
