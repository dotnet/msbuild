// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Runner;
using NuGet.ContentModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// <see cref="ApiCompatRunner"/> extension methods that are specific to package validation
    /// and rely on NuGet API.
    /// </summary>
    internal static class ApiCompatRunnerExtensions
    {
        public static void QueueApiCompatFromContentItem(this IApiCompatRunner apiCompatRunner,
            ICompatibilityLogger log,
            ContentItem leftItem,
            ContentItem rightItem,
            ApiCompatRunnerOptions options,
            Package leftPackage,
            Package? rightPackage = null)
        {
            string? displayString = options.IsBaselineComparison ? Resources.Baseline + " " + leftItem.Path : null;

            MetadataInformation left = GetMetadataInformation(log, leftPackage, leftItem, displayString);
            MetadataInformation right = GetMetadataInformation(log, rightPackage ?? leftPackage, rightItem);

            apiCompatRunner.EnqueueWorkItem(new ApiCompatRunnerWorkItem(left, options, right));
        }

        private static MetadataInformation GetMetadataInformation(ICompatibilityLogger log,
            Package package,
            ContentItem item,
            string? displayString = null)
        {
            string targetFramework = ((NuGetFramework)item.Properties["tfm"]).GetShortFolderName();
            displayString ??= item.Path;

            string[]? assemblyReferences = null;
            if (package.AssemblyReferences != null && !package.AssemblyReferences.TryGetValue(targetFramework, out assemblyReferences))
            {
                log.LogWarning(
                    new Suppression(DiagnosticIds.SearchDirectoriesNotFoundForTfm)
                    {
                        Target = displayString
                    },
                    DiagnosticIds.SearchDirectoriesNotFoundForTfm,
                    Resources.MissingSearchDirectory,
                    targetFramework,
                    displayString);
            }

            return new MetadataInformation(package.PackageId, item.Path, package.PackagePath, assemblyReferences, displayString);
        }
    }
}
