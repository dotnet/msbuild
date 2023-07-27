// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Runner;
using NuGet.ContentModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation.Validators
{
    /// <summary>
    /// Validates that no target framework / rid support is dropped in the latest package.
    /// Reports all the breaking changes in the latest package.
    /// </summary>
    public class BaselinePackageValidator : IPackageValidator
    {
        private readonly ISuppressableLog _log;
        private readonly IApiCompatRunner _apiCompatRunner;

        public BaselinePackageValidator(ISuppressableLog log,
            IApiCompatRunner apiCompatRunner)
        {
            _log = log;
            _apiCompatRunner = apiCompatRunner;
        }

        /// <summary>
        /// Validates the latest NuGet package doesn't drop any target framework/rid and does not introduce any breaking changes.
        /// </summary>
        /// <param name="options"><see cref="PackageValidatorOption"/> to configure the baseline package validation.</param>
        public void Validate(PackageValidatorOption options)
        {
            if (options.BaselinePackage is null)
                throw new ArgumentNullException(nameof(options.BaselinePackage));

            ApiCompatRunnerOptions apiCompatOptions = new(options.EnableStrictMode, isBaselineComparison: true);

            // Iterate over all target frameworks in the package.
            foreach (NuGetFramework baselineTargetFramework in options.BaselinePackage.FrameworksInPackage)
            {
                // Retrieve the compile time assets from the baseline package
                IReadOnlyList<ContentItem>? baselineCompileAssets = options.BaselinePackage.FindBestCompileAssetForFramework(baselineTargetFramework);
                if (baselineCompileAssets != null)
                {
                    // Search for compatible compile time assets in the latest package.
                    IReadOnlyList<ContentItem>? latestCompileAssets = options.Package.FindBestCompileAssetForFramework(baselineTargetFramework);
                    if (latestCompileAssets == null)
                    {
                        _log.LogError(new Suppression(DiagnosticIds.TargetFrameworkDropped) { Target = baselineTargetFramework.ToString() },
                            DiagnosticIds.TargetFrameworkDropped,
                            string.Format(Resources.MissingTargetFramework,
                                baselineTargetFramework));
                    }
                    else if (options.EnqueueApiCompatWorkItems)
                    {
                        _apiCompatRunner.QueueApiCompatFromContentItem(_log,
                            baselineCompileAssets,
                            latestCompileAssets,
                            apiCompatOptions,
                            options.BaselinePackage,
                            options.Package);
                    }
                }

                // Retrieve runtime baseline assets and searches for compatible runtime assets in the latest package.
                IReadOnlyList<ContentItem>? baselineRuntimeAssets = options.BaselinePackage.FindBestRuntimeAssetForFramework(baselineTargetFramework);
                if (baselineRuntimeAssets != null)
                {
                    // Search for compatible runtime assets in the latest package.
                    IReadOnlyList<ContentItem>? latestRuntimeAssets = options.Package.FindBestRuntimeAssetForFramework(baselineTargetFramework);
                    if (latestRuntimeAssets == null)
                    {
                        _log.LogError(new Suppression(DiagnosticIds.TargetFrameworkDropped) { Target = baselineTargetFramework.ToString() },
                            DiagnosticIds.TargetFrameworkDropped,
                            string.Format(Resources.MissingTargetFramework,
                                baselineTargetFramework));
                    }
                    else if (options.EnqueueApiCompatWorkItems)
                    {
                        _apiCompatRunner.QueueApiCompatFromContentItem(_log,
                            baselineRuntimeAssets,
                            latestRuntimeAssets,
                            apiCompatOptions,
                            options.BaselinePackage,
                            options.Package);
                    }
                }

                // Retrieve runtime specific baseline assets and searches for compatible runtime specific assets in the latest package.
                IReadOnlyList<ContentItem>? baselineRuntimeSpecificAssets = options.BaselinePackage.FindBestRuntimeSpecificAssetForFramework(baselineTargetFramework);
                if (baselineRuntimeSpecificAssets != null && baselineRuntimeSpecificAssets.Count > 0)
                {
                    IEnumerable<IGrouping<string, ContentItem>> baselineRuntimeSpecificAssetsRidGroupedPerRid = baselineRuntimeSpecificAssets
                        .Where(t => t.Path.StartsWith("runtimes"))
                        .GroupBy(t => (string)t.Properties["rid"]);

                    foreach (IGrouping<string, ContentItem> baselineRuntimeSpecificAssetsRidGroup in baselineRuntimeSpecificAssetsRidGroupedPerRid)
                    {
                        IReadOnlyList<ContentItem>? latestRuntimeSpecificAssets = options.Package.FindBestRuntimeAssetForFrameworkAndRuntime(baselineTargetFramework, baselineRuntimeSpecificAssetsRidGroup.Key);
                        if (latestRuntimeSpecificAssets == null)
                        {
                            _log.LogError(new Suppression(DiagnosticIds.TargetFrameworkAndRidPairDropped) { Target = baselineTargetFramework.ToString() + "-" + baselineRuntimeSpecificAssetsRidGroup.Key },
                                DiagnosticIds.TargetFrameworkAndRidPairDropped,
                                string.Format(Resources.MissingTargetFrameworkAndRid,
                                    baselineTargetFramework,
                                    baselineRuntimeSpecificAssetsRidGroup.Key));
                        }
                        else if (options.EnqueueApiCompatWorkItems)
                        {
                            _apiCompatRunner.QueueApiCompatFromContentItem(_log,
                                baselineRuntimeSpecificAssetsRidGroup.ToArray(),
                                latestRuntimeSpecificAssets,
                                apiCompatOptions,
                                options.BaselinePackage,
                                options.Package);
                        }
                    }
                }
            }

            if (options.ExecuteApiCompatWorkItems)
                _apiCompatRunner.ExecuteWorkItems();
        }
    }
}
