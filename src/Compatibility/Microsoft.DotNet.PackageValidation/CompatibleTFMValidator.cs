// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.Compatibility.ErrorSuppression;
using NuGet.ContentModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Validates that there are compile time and runtime assets for all the compatible frameworks.
    /// Queues the apicompat between the applicable compile and runtime assemblies for these frameworks.
    /// </summary>
    public class CompatibleTfmValidator
    {
        private static HashSet<string> s_diagList = new HashSet<string>{ DiagnosticIds.CompatibleRuntimeRidLessAsset, DiagnosticIds.ApplicableCompileTimeAsset };
        private static Dictionary<NuGetFramework, HashSet<NuGetFramework>> s_packageTfmMapping = InitializeTfmMappings();

        private readonly bool _runApiCompat;
        private readonly DiagnosticBag<IDiagnostic> _diagnosticBag;
        private readonly ApiCompatRunner _apiCompatRunner;
        private readonly IPackageLogger _log;

        public CompatibleTfmValidator(string noWarn, (string, string)[] ignoredDifferences, bool runApiCompat, bool enableStrictMode, IPackageLogger log)
        {
            _runApiCompat = runApiCompat;
            _log = log;
            _apiCompatRunner = new(noWarn, ignoredDifferences, enableStrictMode, _log);
            _diagnosticBag = new(noWarn?.Split(';')?.Where(t => s_diagList.Contains(t)), ignoredDifferences);
        }

        /// <summary>
        /// Validates that there are compile time and runtime assets for all the compatible frameworks.
        /// Validates that the surface between compile time and runtime assets is compatible.
        /// </summary>
        /// <param name="package">Nuget Package that needs to be validated.</param>
        public void Validate(Package package)
        {
            if (_runApiCompat)
                _apiCompatRunner.InitializePaths(package.PackagePath, package.PackagePath);

            HashSet<NuGetFramework> compatibleTargetFrameworks = new();
            foreach (NuGetFramework item in package.FrameworksInPackage)
            {
                compatibleTargetFrameworks.Add(item);
                if (s_packageTfmMapping.ContainsKey(item))
                {
                    compatibleTargetFrameworks.UnionWith(s_packageTfmMapping[item]);
                }
            }

            foreach (NuGetFramework framework in compatibleTargetFrameworks)
            {
                ContentItem compileTimeAsset = package.FindBestCompileAssetForFramework(framework);

                if (compileTimeAsset == null)
                {
                    if (!_diagnosticBag.Filter(DiagnosticIds.ApplicableCompileTimeAsset, framework.ToString()))
                    {
                        _log.LogError(
                            new Suppression { DiagnosticId = DiagnosticIds.ApplicableCompileTimeAsset, Target = framework.ToString() },
                            DiagnosticIds.ApplicableCompileTimeAsset, 
                            Resources.NoCompatibleCompileTimeAsset, 
                            framework.ToString());
                    }
                    break;
                }

                ContentItem runtimeAsset = package.FindBestRuntimeAssetForFramework(framework);
                if (runtimeAsset == null)
                {
                    if (!_diagnosticBag.Filter(DiagnosticIds.CompatibleRuntimeRidLessAsset, framework.ToString()))
                    {
                        _log.LogError(
                            new Suppression { DiagnosticId = DiagnosticIds.CompatibleRuntimeRidLessAsset, Target = framework.ToString() },
                            DiagnosticIds.CompatibleRuntimeRidLessAsset, 
                            Resources.NoCompatibleRuntimeAsset, 
                            framework.ToString());
                    }
                }
                else
                {
                    if (_runApiCompat && compileTimeAsset.Path != runtimeAsset.Path)
                    {
                        string header = string.Format(Resources.ApiCompatibilityHeader, compileTimeAsset.Path, runtimeAsset.Path);
                        _apiCompatRunner.QueueApiCompatFromContentItem(package.PackageId, compileTimeAsset, runtimeAsset, header);
                    }
                }
 
                foreach (string rid in package.Rids.Where(t => IsSupportedRidTargetFrameworkPair(framework, t)))
                {
                    runtimeAsset = package.FindBestRuntimeAssetForFrameworkAndRuntime(framework, rid);
                    if (runtimeAsset == null)
                    {
                        if (!_diagnosticBag.Filter(DiagnosticIds.CompatibleRuntimeRidSpecificAsset, framework.ToString() + "-" + rid))
                        {
                            _log.LogError(
                                new Suppression { DiagnosticId = DiagnosticIds.CompatibleRuntimeRidSpecificAsset, Target = framework.ToString() + "-" + rid },
                                DiagnosticIds.CompatibleRuntimeRidSpecificAsset, 
                                Resources.NoCompatibleRidSpecificRuntimeAsset, 
                                framework.ToString(), 
                                rid);
                        }
                    }
                    else
                    {
                        if (_runApiCompat && compileTimeAsset.Path != runtimeAsset.Path)
                        {
                            string header = string.Format(Resources.ApiCompatibilityHeader, compileTimeAsset.Path, runtimeAsset.Path);
                            _apiCompatRunner.QueueApiCompatFromContentItem(package.PackageId, compileTimeAsset, runtimeAsset, header);
                        }
                    }
                }
            }

            _apiCompatRunner.RunApiCompat();
        }

        private static Dictionary<NuGetFramework, HashSet<NuGetFramework>> InitializeTfmMappings()
        {
            Dictionary<NuGetFramework, HashSet<NuGetFramework>> packageTfmMapping = new();
            // creating a map framework in package => frameworks to test based on default compatibilty mapping.
            foreach (var item in DefaultFrameworkMappings.Instance.CompatibilityMappings)
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

        // https://github.com/NuGet/Home/issues/11146
        private static bool IsSupportedRidTargetFrameworkPair(NuGetFramework tfm, string rid)
        {
            return tfm.Framework == ".NETFramework" ? rid.StartsWith("win", StringComparison.OrdinalIgnoreCase) : true;
        }
    }
}
