// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
        private readonly ICompatibilityLogger _log;
        private readonly IApiCompatRunner _apiCompatRunner;

        public BaselinePackageValidator(ICompatibilityLogger log,
            IApiCompatRunner apiCompatRunner)
        {
            _log = log;
            _apiCompatRunner = apiCompatRunner;
        }

        /// <summary>
        /// Validates the latest nuget package doesnot drop any target framework/rid and does not introduce any breaking changes.
        /// </summary>
        /// <param name="package">Nuget Package that needs to be validated.</param>
        public void Validate(PackageValidatorOption options)
        {
            if (options.BaselinePackage is null)
                throw new ArgumentNullException(nameof(options.BaselinePackage));

            ApiCompatRunnerOptions apiCompatOptions = new(options.EnableStrictMode, isBaselineComparison: true);

            // Iterate over all available baseline assets
            foreach (ContentItem baselineCompileTimeAsset in options.BaselinePackage.RefAssets)
            {
                // Search for a compatible compile time asset in the latest package
                NuGetFramework baselineTargetFramework = (NuGetFramework)baselineCompileTimeAsset.Properties["tfm"];
                ContentItem? latestCompileTimeAsset = options.Package.FindBestCompileAssetForFramework(baselineTargetFramework);
                if (latestCompileTimeAsset == null)
                {
                    _log.LogError(
                        new Suppression(DiagnosticIds.TargetFrameworkDropped) { Target = baselineTargetFramework.ToString() },
                        DiagnosticIds.TargetFrameworkDropped,
                        Resources.MissingTargetFramework,
                        baselineTargetFramework.ToString());
                }
                else if (options.EnqueueApiCompatWorkItems)
                {
                    _apiCompatRunner.QueueApiCompatFromContentItem(_log,
                        baselineCompileTimeAsset,
                        latestCompileTimeAsset,
                        apiCompatOptions,
                        options.BaselinePackage,
                        options.Package);
                }
            }

            // Iterates over both runtime and runtime specific baseline assets and searches for a compatible non runtime
            // specific asset in the latest package.
            foreach (ContentItem baselineRuntimeAsset in options.BaselinePackage.RuntimeAssets)
            {
                // Search for a compatible runtime asset in the latest package
                NuGetFramework baselineTargetFramework = (NuGetFramework)baselineRuntimeAsset.Properties["tfm"];
                ContentItem? latestRuntimeAsset = options.Package.FindBestRuntimeAssetForFramework(baselineTargetFramework);
                if (latestRuntimeAsset == null)
                {
                    _log.LogError(
                        new Suppression(DiagnosticIds.TargetFrameworkDropped) { Target = baselineTargetFramework.ToString() },
                        DiagnosticIds.TargetFrameworkDropped,
                        Resources.MissingTargetFramework,
                        baselineTargetFramework.ToString());
                }
                else if (options.EnqueueApiCompatWorkItems)
                {
                    _apiCompatRunner.QueueApiCompatFromContentItem(_log,
                        baselineRuntimeAsset,
                        latestRuntimeAsset,
                        apiCompatOptions,
                        options.BaselinePackage,
                        options.Package);
                }
            }

            // Compares runtime specific baseline assets against runtime specific latest assets.
            foreach (ContentItem baselineRuntimeSpecificAsset in options.BaselinePackage.RuntimeSpecificAssets)
            {
                NuGetFramework baselineTargetFramework = (NuGetFramework)baselineRuntimeSpecificAsset.Properties["tfm"];
                string baselineRid = (string)baselineRuntimeSpecificAsset.Properties["rid"];
                ContentItem? latestRuntimeSpecificAsset = options.Package.FindBestRuntimeAssetForFrameworkAndRuntime(baselineTargetFramework, baselineRid);
                if (latestRuntimeSpecificAsset == null)
                {
                    _log.LogError(
                        new Suppression(DiagnosticIds.TargetFrameworkAndRidPairDropped) { Target = baselineTargetFramework.ToString() + "-" + baselineRid },
                        DiagnosticIds.TargetFrameworkAndRidPairDropped,
                        Resources.MissingTargetFrameworkAndRid,
                        baselineTargetFramework.ToString(),
                        baselineRid);
                }
                else if (options.EnqueueApiCompatWorkItems)
                {
                    _apiCompatRunner.QueueApiCompatFromContentItem(_log,
                        baselineRuntimeSpecificAsset,
                        latestRuntimeSpecificAsset,
                        apiCompatOptions,
                        options.BaselinePackage,
                        options.Package);
                }
            }

            if (options.ExecuteApiCompatWorkItems)
                _apiCompatRunner.ExecuteWorkItems();
        }
    }
}
