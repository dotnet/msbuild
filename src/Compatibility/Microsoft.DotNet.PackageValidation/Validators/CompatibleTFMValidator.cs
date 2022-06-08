// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ApiCompatibility.Logging;
using NuGet.ContentModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation.Validators
{
    /// <summary>
    /// Validates that there are compile time and runtime assets for all the compatible frameworks.
    /// Queues the apicompat between the applicable compile and runtime assemblies for these frameworks.
    /// </summary>
    public class CompatibleTfmValidator : IPackageValidator
    {
        private static readonly Dictionary<NuGetFramework, HashSet<NuGetFramework>> s_packageTfmMapping = InitializeTfmMappings();
        private readonly CompatibilityLoggerBase _log;

        public CompatibleTfmValidator(CompatibilityLoggerBase log)
        {
            _log = log;
        }

        /// <summary>
        /// Validates that there are compile time and runtime assets for all the compatible frameworks.
        /// Validates that the surface between compile time and runtime assets is compatible.
        /// </summary>
        /// <param name="package">Nuget Package that needs to be validated.</param>
        public void Validate(PackageValidatorOption option)
        {
            ApiCompatRunner apiCompatRunner = new(_log,
                option.EnableStrictMode,
                option.FrameworkReferences,
                option.Package.PackagePath);

            HashSet<NuGetFramework> compatibleTargetFrameworks = new();
            foreach (NuGetFramework item in option.Package.FrameworksInPackage)
            {
                compatibleTargetFrameworks.Add(item);
                if (s_packageTfmMapping.ContainsKey(item))
                {
                    compatibleTargetFrameworks.UnionWith(s_packageTfmMapping[item]);
                }
            }

            foreach (NuGetFramework framework in compatibleTargetFrameworks)
            {
                ContentItem? compileTimeAsset = option.Package.FindBestCompileAssetForFramework(framework);
                if (compileTimeAsset == null)
                {
                    _log.LogError(
                        new Suppression(DiagnosticIds.ApplicableCompileTimeAsset) { Target = framework.ToString() },
                        DiagnosticIds.ApplicableCompileTimeAsset,
                        Resources.NoCompatibleCompileTimeAsset,
                        framework.ToString());
                    break;
                }

                ContentItem? runtimeAsset = option.Package.FindBestRuntimeAssetForFramework(framework);
                if (runtimeAsset == null)
                {
                    _log.LogError(
                        new Suppression(DiagnosticIds.CompatibleRuntimeRidLessAsset) { Target = framework.ToString() },
                        DiagnosticIds.CompatibleRuntimeRidLessAsset,
                        Resources.NoCompatibleRuntimeAsset,
                        framework.ToString());
                }
                // Invoke ApiCompat to compare the compile time asset with the runtime asset if they are not the same assembly.
                else if (option.RunApiCompat && compileTimeAsset.Path != runtimeAsset.Path)
                {
                    string header = string.Format(Resources.ApiCompatibilityHeader, compileTimeAsset.Path, runtimeAsset.Path);
                    apiCompatRunner.QueueApiCompatFromContentItem(option.Package.PackageId, compileTimeAsset, runtimeAsset, header);
                }

                foreach (string rid in option.Package.Rids.Where(t => IsSupportedRidTargetFrameworkPair(framework, t)))
                {
                    ContentItem? runtimeRidSpecificAsset = option.Package.FindBestRuntimeAssetForFrameworkAndRuntime(framework, rid);
                    if (runtimeRidSpecificAsset == null)
                    {
                        _log.LogError(
                            new Suppression(DiagnosticIds.CompatibleRuntimeRidSpecificAsset) { Target = framework.ToString() + "-" + rid },
                            DiagnosticIds.CompatibleRuntimeRidSpecificAsset,
                            Resources.NoCompatibleRidSpecificRuntimeAsset,
                            framework.ToString(),
                            rid);
                    }
                    // Invoke ApiCompat to compare the compile time asset with the runtime specific asset if they are not the same and
                    // if the comparison hasn't already happened (when the runtime asset is the same as the runtime specific asset).
                    else if (option.RunApiCompat &&
                        compileTimeAsset.Path != runtimeRidSpecificAsset.Path &&
                        (runtimeAsset == null || runtimeAsset.Path != runtimeRidSpecificAsset.Path))
                    {
                        string header = string.Format(Resources.ApiCompatibilityHeader, compileTimeAsset.Path, runtimeRidSpecificAsset.Path);
                        apiCompatRunner.QueueApiCompatFromContentItem(option.Package.PackageId, compileTimeAsset, runtimeRidSpecificAsset, header);
                    }
                }
            }

            if (option.RunApiCompat)
                apiCompatRunner.RunApiCompat();
        }

        private static Dictionary<NuGetFramework, HashSet<NuGetFramework>> InitializeTfmMappings()
        {
            Dictionary<NuGetFramework, HashSet<NuGetFramework>> packageTfmMapping = new();

            // creating a map framework in package => frameworks to test based on default compatibilty mapping.
            foreach (OneWayCompatibilityMappingEntry item in DefaultFrameworkMappings.Instance.CompatibilityMappings)
            {
                NuGetFramework forwardTfm = item.SupportedFrameworkRange.Max;
                NuGetFramework reverseTfm = item.TargetFrameworkRange.Min;
                if (packageTfmMapping.ContainsKey(forwardTfm))
                {
                    packageTfmMapping[forwardTfm].Add(reverseTfm);
                }
                else
                {
                    packageTfmMapping.Add(forwardTfm, new HashSet<NuGetFramework> { reverseTfm });
                }
            }

            return packageTfmMapping;
        }

        private static bool IsSupportedRidTargetFrameworkPair(NuGetFramework tfm, string rid) =>
            tfm.Framework != ".NETFramework" || rid.StartsWith("win", StringComparison.OrdinalIgnoreCase);
    }
}
