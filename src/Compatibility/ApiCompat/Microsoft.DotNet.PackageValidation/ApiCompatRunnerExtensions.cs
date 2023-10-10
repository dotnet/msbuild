// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.ApiCompatibility;
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
            ISuppressibleLog log,
            IReadOnlyList<ContentItem> leftContentItems,
            IReadOnlyList<ContentItem> rightContentItems,
            ApiCompatRunnerOptions options,
            Package leftPackage,
            Package? rightPackage = null)
        {
            Debug.Assert(leftContentItems.Count > 0);
            Debug.Assert(rightContentItems.Count > 0);

            // Don't enqueue duplicate items (if no right package is supplied and items match)
            if (rightPackage is null && ContentItemCollectionEquals(leftContentItems, rightContentItems))
            {
                return;
            }

            // If a right hand side package is provided in addition to the left side, package validation runs in baseline comparison mode.
            // The assumption stands that the right hand side is "current" and has more information than the left, i.e. assembly references.
            // If the left package doesn't provide assembly references, fallback to the references from the right side if the TFMs are compatible.
            IEnumerable<string>? fallbackAssemblyReferences = null;
            if (rightPackage is not null && leftPackage.AssemblyReferences is null)
            {
                // Retrieve the left TFM and try to find the best assembly references from the right hand side package.
                if (leftContentItems[0].Properties.TryGetValue("tfm", out object? tfmObj) && tfmObj is NuGetFramework leftTargetFramework)
                {
                    fallbackAssemblyReferences = rightPackage.FindBestAssemblyReferencesForFramework(leftTargetFramework);
                }
            }

            MetadataInformation[] left = new MetadataInformation[leftContentItems.Count];
            for (int leftIndex = 0; leftIndex < leftContentItems.Count; leftIndex++)
            {
                left[leftIndex] = GetMetadataInformation(log,
                    leftPackage,
                    leftContentItems[leftIndex],
                    displayString: options.IsBaselineComparison ? Resources.Baseline + " " + leftContentItems[leftIndex].Path : null,
                    assemblyReferences: fallbackAssemblyReferences);
            }

            MetadataInformation[] right = new MetadataInformation[rightContentItems.Count];
            for (int rightIndex = 0; rightIndex < rightContentItems.Count; rightIndex++)
            {
                right[rightIndex] = GetMetadataInformation(log,
                    rightPackage ?? leftPackage,
                    rightContentItems[rightIndex]);
            }

            apiCompatRunner.EnqueueWorkItem(new ApiCompatRunnerWorkItem(left, options, right));
        }

        private static MetadataInformation GetMetadataInformation(ISuppressibleLog log,
            Package package,
            ContentItem item,
            string? displayString = null,
            IEnumerable<string>? assemblyReferences = null)
        {
            displayString ??= item.Path;

            // Don't pass assembly references to the work item if the TFM can't be retrieved.
            if (item.Properties.TryGetValue("tfm", out object? tfmObj) && tfmObj is NuGetFramework nuGetFramework)
            {
                // If assembly references are provided for the package, use those.
                if (package.AssemblyReferences is not null && package.AssemblyReferences.Count > 0)
                {
                    // See if the package's assembly reference entries have the same target framework.
                    if (!package.AssemblyReferences.TryGetValue(nuGetFramework, out assemblyReferences))
                    {
                        log.LogWarning(new Suppression(DiagnosticIds.SearchDirectoriesNotFoundForTfm) { Target = displayString },
                            DiagnosticIds.SearchDirectoriesNotFoundForTfm,
                            string.Format(Resources.MissingSearchDirectory,
                                nuGetFramework.GetShortFolderName(),
                                displayString));
                    }
                }
            }

            return new MetadataInformation(Path.GetFileName(item.Path), item.Path, package.PackagePath, assemblyReferences, displayString);
        }

        private static bool ContentItemCollectionEquals(IReadOnlyList<ContentItem> leftContentItems,
            IReadOnlyList<ContentItem> rightContentItems)
        {
            if (leftContentItems.Count != rightContentItems.Count)
            {
                return false;
            }

            for (int i = 0; i < leftContentItems.Count; i++)
            {
                if (leftContentItems[i].Path != rightContentItems[i].Path)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
