// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.DotNet.ApiCompatibility.Logging;
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
        private readonly CompatibilityLoggerBase _log;

        public BaselinePackageValidator(CompatibilityLoggerBase log)
        {
            _log = log;
        }

        /// <summary>
        /// Validates the latest nuget package doesnot drop any target framework/rid and does not introduce any breaking changes.
        /// </summary>
        /// <param name="package">Nuget Package that needs to be validated.</param>
        public void Validate(PackageValidatorOption option)
        {
            if (option.BaselinePackage is null)
                throw new ArgumentNullException(nameof(option.BaselinePackage));

            ApiCompatRunner apiCompatRunner = new(_log,
                option.EnableStrictMode,
                option.FrameworkReferences,
                option.BaselinePackage.PackagePath,
                option.Package.PackagePath);

            // Iterate over all available baseline assets
            foreach (ContentItem baselineCompileTimeAsset in option.BaselinePackage.RefAssets)
            {
                // Search for a compatible compile time asset in the latest package
                NuGetFramework baselineTargetFramework = (NuGetFramework)baselineCompileTimeAsset.Properties["tfm"];
                ContentItem? latestCompileTimeAsset = option.Package.FindBestCompileAssetForFramework(baselineTargetFramework);
                if (latestCompileTimeAsset == null)
                {
                    _log.LogError(
                        new Suppression(DiagnosticIds.TargetFrameworkDropped) { Target = baselineTargetFramework.ToString() },
                        DiagnosticIds.TargetFrameworkDropped,
                        Resources.MissingTargetFramework,
                        baselineTargetFramework.ToString());
                }
                else if (option.RunApiCompat)
                {
                    string header = string.Format(Resources.ApiCompatibilityBaselineHeader, baselineCompileTimeAsset.Path, latestCompileTimeAsset.Path, option.BaselinePackage.Version, option.Package.Version);
                    apiCompatRunner.QueueApiCompatFromContentItem(option.Package.PackageId, baselineCompileTimeAsset, latestCompileTimeAsset, header, isBaseline: true);
                }
            }

            // Iterates over both runtime and runtime specific baseline assets and searches for a compatible non runtime
            // specific asset in the latest package.
            foreach (ContentItem baselineRuntimeAsset in option.BaselinePackage.RuntimeAssets)
            {
                // Search for a compatible runtime asset in the latest package
                NuGetFramework baselineTargetFramework = (NuGetFramework)baselineRuntimeAsset.Properties["tfm"];
                ContentItem? latestRuntimeAsset = option.Package.FindBestRuntimeAssetForFramework(baselineTargetFramework);
                if (latestRuntimeAsset == null)
                {
                    _log.LogError(
                        new Suppression(DiagnosticIds.TargetFrameworkDropped) { Target = baselineTargetFramework.ToString() },
                        DiagnosticIds.TargetFrameworkDropped,
                        Resources.MissingTargetFramework,
                        baselineTargetFramework.ToString());
                }
                else if (option.RunApiCompat)
                {
                    string header = string.Format(Resources.ApiCompatibilityBaselineHeader, baselineRuntimeAsset.Path, latestRuntimeAsset.Path, option.BaselinePackage.Version, option.Package.Version);
                    apiCompatRunner.QueueApiCompatFromContentItem(option.Package.PackageId, baselineRuntimeAsset, latestRuntimeAsset, header, isBaseline: true);
                }
            }

            // Compares runtime specific baseline assets against runtime specific latest assets.
            foreach (ContentItem baselineRuntimeSpecificAsset in option.BaselinePackage.RuntimeSpecificAssets)
            {
                NuGetFramework baselineTargetFramework = (NuGetFramework)baselineRuntimeSpecificAsset.Properties["tfm"];
                string baselineRid = (string)baselineRuntimeSpecificAsset.Properties["rid"];
                ContentItem? latestRuntimeSpecificAsset = option.Package.FindBestRuntimeAssetForFrameworkAndRuntime(baselineTargetFramework, baselineRid);
                if (latestRuntimeSpecificAsset == null)
                {
                    _log.LogError(
                        new Suppression(DiagnosticIds.TargetFrameworkAndRidPairDropped) { Target = baselineTargetFramework.ToString() + "-" + baselineRid },
                        DiagnosticIds.TargetFrameworkAndRidPairDropped,
                        Resources.MissingTargetFrameworkAndRid,
                        baselineTargetFramework.ToString(),
                        baselineRid);
                }
                else if (option.RunApiCompat)
                {
                    string header = string.Format(Resources.ApiCompatibilityBaselineHeader, baselineRuntimeSpecificAsset.Path, latestRuntimeSpecificAsset.Path, option.BaselinePackage.Version, option.Package.Version);
                    apiCompatRunner.QueueApiCompatFromContentItem(option.Package.PackageId, baselineRuntimeSpecificAsset, latestRuntimeSpecificAsset, header, isBaseline: true);
                }
            }

            if (option.RunApiCompat)
                apiCompatRunner.RunApiCompat();
        }
    }
}
