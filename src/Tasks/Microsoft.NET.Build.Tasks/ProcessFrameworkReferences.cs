// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Newtonsoft.Json;
using NuGet.Frameworks;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// This class processes the FrameworkReference items.  It adds PackageReferences for the
    /// targeting packs which provide the reference assemblies, and creates RuntimeFramework
    /// items, which are written to the runtimeconfig file
    /// </summary>
    public class ProcessFrameworkReferences : TaskBase
    {
        public string TargetFrameworkIdentifier { get; set; }

        public string TargetFrameworkVersion { get; set; }

        public string TargetPlatformIdentifier { get; set; }

        public string TargetPlatformVersion { get; set; }

        public string TargetingPackRoot { get; set; }

        [Required]
        public string RuntimeGraphPath { get; set; }

        public bool SelfContained { get; set; }

        public bool ReadyToRunEnabled { get; set; }

        public bool ReadyToRunUseCrossgen2 { get; set; }

        public string RuntimeIdentifier { get; set; }

        public string[] RuntimeIdentifiers { get; set; }

        public string RuntimeFrameworkVersion { get; set; }

        public bool TargetLatestRuntimePatch { get; set; }

        public bool TargetLatestRuntimePatchIsDefault { get; set; }

        public bool EnableTargetingPackDownload { get; set; }

        public bool EnableRuntimePackDownload { get; set; }

        public ITaskItem[] FrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] KnownFrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] KnownRuntimePacks { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] KnownCrossgen2Packs { get; set; } = Array.Empty<ITaskItem>();

        [Required]
        public string NETCoreSdkRuntimeIdentifier { get; set; }

        [Required]
        public string NetCoreRoot { get; set; }

        [Required]
        public string NETCoreSdkVersion { get; set; }

        [Output]
        public ITaskItem[] PackagesToDownload { get; set; }

        [Output]
        public ITaskItem[] RuntimeFrameworks { get; set; }

        [Output]
        public ITaskItem[] TargetingPacks { get; set; }

        [Output]
        public ITaskItem[] RuntimePacks { get; set; }

        [Output]
        public ITaskItem[] Crossgen2Packs { get; set; }

        //  Runtime packs which aren't available for the specified RuntimeIdentifier
        [Output]
        public ITaskItem[] UnavailableRuntimePacks { get; set; }

        private Version _normalizedTargetFrameworkVersion;

        protected override void ExecuteCore()
        {
            //  Perf optimization: If there are no FrameworkReference items, then don't do anything
            //  (This means that if you don't have any direct framework references, you won't get any transitive ones either
            if (FrameworkReferences == null || FrameworkReferences.Length == 0)
            {
                return;
            }

            _normalizedTargetFrameworkVersion = NormalizeVersion(new Version(TargetFrameworkVersion));

            var knownFrameworkReferencesForTargetFramework =
                KnownFrameworkReferences
                    .Select(item => new KnownFrameworkReference(item))
                    .Where(kfr => KnownFrameworkReferenceAppliesToTargetFramework(kfr.TargetFramework))
                    .ToList();

            //  Get known runtime packs from known framework references.
            //  Only use items where the framework reference name matches the RuntimeFrameworkName.
            //  This will filter out known framework references for "profiles", ie WindowsForms and WPF
            var knownRuntimePacksForTargetFramework =
                knownFrameworkReferencesForTargetFramework
                    .Where(kfr => kfr.Name.Equals(kfr.RuntimeFrameworkName, StringComparison.OrdinalIgnoreCase))
                    .Select(kfr => kfr.ToKnownRuntimePack())
                    .ToList();

            //  Add additional known runtime packs
            knownRuntimePacksForTargetFramework.AddRange(
                KnownRuntimePacks.Select(item => new KnownRuntimePack(item))
                                 .Where(krp => KnownFrameworkReferenceAppliesToTargetFramework(krp.TargetFramework)));

            var frameworkReferenceMap = FrameworkReferences.ToDictionary(fr => fr.ItemSpec, StringComparer.OrdinalIgnoreCase);

            List<ITaskItem> packagesToDownload = new List<ITaskItem>();
            List<ITaskItem> runtimeFrameworks = new List<ITaskItem>();
            List<ITaskItem> targetingPacks = new List<ITaskItem>();
            List<ITaskItem> runtimePacks = new List<ITaskItem>();
            List<ITaskItem> unavailableRuntimePacks = new List<ITaskItem>();

            HashSet<string> unrecognizedRuntimeIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            bool windowsOnlyErrorLogged = false;
            foreach (var knownFrameworkReference in knownFrameworkReferencesForTargetFramework)
            {
                frameworkReferenceMap.TryGetValue(knownFrameworkReference.Name, out ITaskItem frameworkReference);

                // Handle Windows-only frameworks on non-Windows platforms
                if (knownFrameworkReference.IsWindowsOnly &&
                    !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // It is an error to reference the framework from non-Windows
                    if (!windowsOnlyErrorLogged && frameworkReference != null)
                    {
                        Log.LogError(Strings.WindowsDesktopFrameworkRequiresWindows);
                        windowsOnlyErrorLogged = true;
                    }

                    // Ignore (and don't download) this known framework reference as it requires Windows
                    continue;
                }

                KnownRuntimePack? selectedRuntimePack = SelectRuntimePack(frameworkReference, knownFrameworkReference, knownRuntimePacksForTargetFramework);

                //  Add targeting pack and all known runtime packs to "preferred packages" list.
                //  These are packages that will win in conflict resolution for assets that have identical assembly and file versions
                var preferredPackages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                preferredPackages.Add(knownFrameworkReference.TargetingPackName);

                if (selectedRuntimePack != null)
                {
                    var knownFrameworkReferenceRuntimePackRuntimeIdentifiers = selectedRuntimePack?.RuntimePackRuntimeIdentifiers.Split(';');
                    foreach (var runtimeIdentifier in knownFrameworkReferenceRuntimePackRuntimeIdentifiers)
                    {
                        foreach (var runtimePackNamePattern in selectedRuntimePack?.RuntimePackNamePatterns.Split(';'))
                        {
                            string runtimePackName = runtimePackNamePattern.Replace("**RID**", runtimeIdentifier);
                            preferredPackages.Add(runtimePackName);
                        }
                    }
                }

                TaskItem targetingPack = new TaskItem(knownFrameworkReference.Name);
                targetingPack.SetMetadata(MetadataKeys.NuGetPackageId, knownFrameworkReference.TargetingPackName);
                targetingPack.SetMetadata(MetadataKeys.PackageConflictPreferredPackages, string.Join(";", preferredPackages));

                string targetingPackVersion = null;
                if (frameworkReference != null)
                {
                    //  Allow targeting pack version to be overridden via metadata on FrameworkReference
                    targetingPackVersion = frameworkReference.GetMetadata("TargetingPackVersion");
                }
                if (string.IsNullOrEmpty(targetingPackVersion))
                {
                    targetingPackVersion = knownFrameworkReference.TargetingPackVersion;
                }

                //  Look up targeting pack version from workload manifests if necessary
                targetingPackVersion = GetResolvedPackVersion(knownFrameworkReference.TargetingPackName, targetingPackVersion);

                targetingPack.SetMetadata(MetadataKeys.NuGetPackageVersion, targetingPackVersion);
                targetingPack.SetMetadata("TargetingPackFormat", knownFrameworkReference.TargetingPackFormat);
                targetingPack.SetMetadata("TargetFramework", knownFrameworkReference.TargetFramework.GetShortFolderName());
                targetingPack.SetMetadata(MetadataKeys.RuntimeFrameworkName, knownFrameworkReference.RuntimeFrameworkName);
                if (selectedRuntimePack != null)
                {
                    targetingPack.SetMetadata(MetadataKeys.RuntimePackRuntimeIdentifiers, selectedRuntimePack?.RuntimePackRuntimeIdentifiers);
                }

                if (!string.IsNullOrEmpty(knownFrameworkReference.Profile))
                {
                    targetingPack.SetMetadata("Profile", knownFrameworkReference.Profile);
                }

                //  Get the path of the targeting pack in the targeting pack root (e.g. dotnet/packs)
                string targetingPackPath = GetPackPath(knownFrameworkReference.TargetingPackName, targetingPackVersion);
                if (targetingPackPath != null)
                {
                    // Use targeting pack from packs folder
                    targetingPack.SetMetadata(MetadataKeys.PackageDirectory, targetingPackPath);
                    targetingPack.SetMetadata(MetadataKeys.Path, targetingPackPath);
                }
                else
                {
                    if (EnableTargetingPackDownload)
                    {
                        //  Download targeting pack
                        TaskItem packageToDownload = new TaskItem(knownFrameworkReference.TargetingPackName);
                        packageToDownload.SetMetadata(MetadataKeys.Version, targetingPackVersion);

                        packagesToDownload.Add(packageToDownload);
                    }
                }

                targetingPacks.Add(targetingPack);

                var runtimeFrameworkVersion = GetRuntimeFrameworkVersion(
                    frameworkReference,
                    knownFrameworkReference,
                    selectedRuntimePack,
                    out string runtimePackVersion);

                string isTrimmable = null;
                if (frameworkReference != null)
                {
                    // Allow IsTrimmable to be overridden via metadata on FrameworkReference
                    isTrimmable = frameworkReference.GetMetadata(MetadataKeys.IsTrimmable);
                }
                if (string.IsNullOrEmpty(isTrimmable))
                {
                    isTrimmable = selectedRuntimePack?.IsTrimmable;
                }

                bool useRuntimePackAndDownloadIfNecessary;
                KnownRuntimePack runtimePackForRuntimeIDProcessing;
                if (knownFrameworkReference.Name.Equals(knownFrameworkReference.RuntimeFrameworkName, StringComparison.OrdinalIgnoreCase))
                {
                    //  Only add runtime packs where the framework reference name matches the RuntimeFrameworkName
                    //  Framework references for "profiles" will use the runtime pack from the corresponding non-profile framework
                    runtimePackForRuntimeIDProcessing = selectedRuntimePack.Value;
                    useRuntimePackAndDownloadIfNecessary = true;
                }
                else if (!knownFrameworkReference.RuntimePackRuntimeIdentifiers.Equals(selectedRuntimePack?.RuntimePackRuntimeIdentifiers))
                {
                    // If the profile has a different set of runtime identifiers than the runtime pack, use the profile.
                    runtimePackForRuntimeIDProcessing = knownFrameworkReference.ToKnownRuntimePack();
                    useRuntimePackAndDownloadIfNecessary = true;
                }
                else
                {
                    // For the remaining profiles, don't include them in package download but add them to unavailable if necessary.
                    runtimePackForRuntimeIDProcessing = knownFrameworkReference.ToKnownRuntimePack();
                    useRuntimePackAndDownloadIfNecessary = false;
                }

                bool processedPrimaryRuntimeIdentifier = false;

                var hasRuntimePackAlwaysCopyLocal =
                    selectedRuntimePack != null && selectedRuntimePack.Value.RuntimePackAlwaysCopyLocal;
                var runtimeRequiredByDeployment
                    = (SelfContained || ReadyToRunEnabled) &&
                      !string.IsNullOrEmpty(RuntimeIdentifier) &&
                      selectedRuntimePack != null &&
                      !string.IsNullOrEmpty(selectedRuntimePack.Value.RuntimePackNamePatterns);

                if (hasRuntimePackAlwaysCopyLocal || runtimeRequiredByDeployment)
                {
                    //  Find other KnownFrameworkReferences that map to the same runtime pack, if any
                    List<string> additionalFrameworkReferencesForRuntimePack = null;
                    foreach (var additionalKnownFrameworkReference in knownFrameworkReferencesForTargetFramework)
                    {
                        if (additionalKnownFrameworkReference.RuntimeFrameworkName.Equals(knownFrameworkReference.RuntimeFrameworkName, StringComparison.OrdinalIgnoreCase) &&
                            !additionalKnownFrameworkReference.RuntimeFrameworkName.Equals(additionalKnownFrameworkReference.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            if (additionalFrameworkReferencesForRuntimePack == null)
                            {
                                additionalFrameworkReferencesForRuntimePack = new List<string>();
                            }
                            additionalFrameworkReferencesForRuntimePack.Add(additionalKnownFrameworkReference.Name);
                        }
                    }

                    ProcessRuntimeIdentifier(hasRuntimePackAlwaysCopyLocal ? "any" : RuntimeIdentifier, runtimePackForRuntimeIDProcessing, runtimePackVersion, additionalFrameworkReferencesForRuntimePack,
                        unrecognizedRuntimeIdentifiers, unavailableRuntimePacks, runtimePacks, packagesToDownload, isTrimmable, EnableRuntimePackDownload && useRuntimePackAndDownloadIfNecessary);

                    processedPrimaryRuntimeIdentifier = true;
                }

                if (RuntimeIdentifiers != null)
                {
                    foreach (var runtimeIdentifier in RuntimeIdentifiers)
                    {
                        if (processedPrimaryRuntimeIdentifier && runtimeIdentifier == this.RuntimeIdentifier)
                        {
                            //  We've already processed this RID
                            continue;
                        }

                        //  Pass in null for the runtimePacks list, as for these runtime identifiers we only want to
                        //  download the runtime packs, but not use the assets from them
                        ProcessRuntimeIdentifier(runtimeIdentifier, runtimePackForRuntimeIDProcessing, runtimePackVersion, additionalFrameworkReferencesForRuntimePack: null,
                            unrecognizedRuntimeIdentifiers, unavailableRuntimePacks, runtimePacks: null, packagesToDownload, isTrimmable, useRuntimePackAndDownloadIfNecessary);
                    }
                }

                if (!string.IsNullOrEmpty(knownFrameworkReference.RuntimeFrameworkName) && !knownFrameworkReference.RuntimePackAlwaysCopyLocal)
                {
                    TaskItem runtimeFramework = new TaskItem(knownFrameworkReference.RuntimeFrameworkName);

                    runtimeFramework.SetMetadata(MetadataKeys.Version, runtimeFrameworkVersion);
                    runtimeFramework.SetMetadata(MetadataKeys.FrameworkName, knownFrameworkReference.Name);

                    runtimeFrameworks.Add(runtimeFramework);
                }
            }

            if (ReadyToRunEnabled && ReadyToRunUseCrossgen2)
            {
                if (!AddCrossgen2Package(_normalizedTargetFrameworkVersion, packagesToDownload))
                {
                    Log.LogError(Strings.ReadyToRunNoValidRuntimePackageError);
                    return;
                }
            }

            if (packagesToDownload.Any())
            {
                PackagesToDownload = packagesToDownload.Distinct(new PackageToDownloadComparer<ITaskItem>()).ToArray();
            }

            if (runtimeFrameworks.Any())
            {
                RuntimeFrameworks = runtimeFrameworks.ToArray();
            }

            if (targetingPacks.Any())
            {
                TargetingPacks = targetingPacks.ToArray();
            }

            if (runtimePacks.Any())
            {
                RuntimePacks = runtimePacks.ToArray();
            }

            if (unavailableRuntimePacks.Any())
            {
                UnavailableRuntimePacks = unavailableRuntimePacks.ToArray();
            }
        }

        private bool KnownFrameworkReferenceAppliesToTargetFramework(NuGetFramework knownFrameworkReferenceTargetFramework)
        {
            if (!knownFrameworkReferenceTargetFramework.Framework.Equals(TargetFrameworkIdentifier, StringComparison.OrdinalIgnoreCase)
                || NormalizeVersion(knownFrameworkReferenceTargetFramework.Version) != _normalizedTargetFrameworkVersion)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(knownFrameworkReferenceTargetFramework.Platform)
                && knownFrameworkReferenceTargetFramework.PlatformVersion != null)
            {
                if (!knownFrameworkReferenceTargetFramework.Platform.Equals(TargetPlatformIdentifier, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!Version.TryParse(TargetPlatformVersion, out var targetPlatformVersionParsed))
                {
                    return false;
                }

                if (NormalizeVersion(targetPlatformVersionParsed) != NormalizeVersion(knownFrameworkReferenceTargetFramework.PlatformVersion)
                    || NormalizeVersion(knownFrameworkReferenceTargetFramework.Version) != _normalizedTargetFrameworkVersion)
                {
                    return false;
                }
            }

            return true;
        }

        private KnownRuntimePack? SelectRuntimePack(ITaskItem frameworkReference, KnownFrameworkReference knownFrameworkReference, List<KnownRuntimePack> knownRuntimePacks)
        {
            var requiredLabelsMetadata = frameworkReference?.GetMetadata(MetadataKeys.RuntimePackLabels) ?? "";

            HashSet<string> requiredRuntimePackLabels = null;
            if (frameworkReference != null)
            {
                requiredRuntimePackLabels = new HashSet<string>(requiredLabelsMetadata.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            }

            //  The runtime pack name matches the RuntimeFrameworkName on the KnownFrameworkReference
            var matchingRuntimePacks = knownRuntimePacks.Where(krp => krp.Name.Equals(knownFrameworkReference.RuntimeFrameworkName, StringComparison.OrdinalIgnoreCase))
                .Where(krp =>
                {
                    if (requiredRuntimePackLabels == null)
                    {
                        return krp.RuntimePackLabels.Length == 0;
                    }
                    else
                    {
                        return requiredRuntimePackLabels.SetEquals(krp.RuntimePackLabels);
                    }
                })
                .ToList();

            if (matchingRuntimePacks.Count == 0)
            {
                return null;
            }
            else if (matchingRuntimePacks.Count == 1)
            {
                return matchingRuntimePacks[0];
            }
            else
            {
                string runtimePackDescriptionForErrorMessage = knownFrameworkReference.RuntimeFrameworkName +
                    (requiredLabelsMetadata == string.Empty ? string.Empty : ":" + requiredLabelsMetadata);

                Log.LogError(Strings.ConflictingRuntimePackInformation, runtimePackDescriptionForErrorMessage,
                    string.Join(Environment.NewLine, matchingRuntimePacks.Select(rp => rp.RuntimePackNamePatterns)));

                return knownFrameworkReference.ToKnownRuntimePack();
            }
        }

        private void ProcessRuntimeIdentifier(
            string runtimeIdentifier,
            KnownRuntimePack selectedRuntimePack,
            string runtimePackVersion,
            List<string> additionalFrameworkReferencesForRuntimePack,
            HashSet<string> unrecognizedRuntimeIdentifiers,
            List<ITaskItem> unavailableRuntimePacks,
            List<ITaskItem> runtimePacks,
            List<ITaskItem> packagesToDownload,
            string isTrimmable,
            bool addRuntimePackAndDownloadIfNecessary)
        {
            var runtimeGraph = new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath);
            var knownFrameworkReferenceRuntimePackRuntimeIdentifiers = selectedRuntimePack.RuntimePackRuntimeIdentifiers.Split(';');

            string runtimePackRuntimeIdentifier = NuGetUtils.GetBestMatchingRid(
                    runtimeGraph,
                    runtimeIdentifier,
                    knownFrameworkReferenceRuntimePackRuntimeIdentifiers,
                    out bool wasInGraph);

            if (runtimePackRuntimeIdentifier == null)
            {
                if (wasInGraph)
                {
                    //  Report this as an error later, if necessary.  This is because we try to download
                    //  all available runtime packs in case there is a transitive reference to a shared
                    //  framework we don't directly reference.  But we don't want to immediately error out
                    //  here if a runtime pack that we might not need to reference isn't available for the
                    //  targeted RID (e.g. Microsoft.WindowsDesktop.App for a linux RID).
                    var unavailableRuntimePack = new TaskItem(selectedRuntimePack.Name);
                    unavailableRuntimePack.SetMetadata(MetadataKeys.RuntimeIdentifier, runtimeIdentifier);
                    unavailableRuntimePacks.Add(unavailableRuntimePack);
                }
                else if (!unrecognizedRuntimeIdentifiers.Contains(runtimeIdentifier))
                {
                    //  NETSDK1083: The specified RuntimeIdentifier '{0}' is not recognized.
                    Log.LogError(Strings.RuntimeIdentifierNotRecognized, runtimeIdentifier);
                    unrecognizedRuntimeIdentifiers.Add(runtimeIdentifier);
                }
            }
            else if (addRuntimePackAndDownloadIfNecessary)
            {
                foreach (var runtimePackNamePattern in selectedRuntimePack.RuntimePackNamePatterns.Split(';'))
                {
                    string runtimePackName = runtimePackNamePattern.Replace("**RID**", runtimePackRuntimeIdentifier);

                    //  Look up runtimePackVersion from workload manifests if necessary
                    string resolvedRuntimePackVersion = GetResolvedPackVersion(runtimePackName, runtimePackVersion);

                    string runtimePackPath = GetPackPath(runtimePackName, resolvedRuntimePackVersion);

                    if (runtimePacks != null)
                    {
                        TaskItem runtimePackItem = new TaskItem(runtimePackName);
                        runtimePackItem.SetMetadata(MetadataKeys.NuGetPackageId, runtimePackName);
                        runtimePackItem.SetMetadata(MetadataKeys.NuGetPackageVersion, resolvedRuntimePackVersion);
                        runtimePackItem.SetMetadata(MetadataKeys.FrameworkName, selectedRuntimePack.Name);
                        runtimePackItem.SetMetadata(MetadataKeys.RuntimeIdentifier, runtimePackRuntimeIdentifier);
                        runtimePackItem.SetMetadata(MetadataKeys.IsTrimmable, isTrimmable);

                        if (selectedRuntimePack.RuntimePackAlwaysCopyLocal)
                        {
                            runtimePackItem.SetMetadata(MetadataKeys.RuntimePackAlwaysCopyLocal, "true");
                        }

                        if (additionalFrameworkReferencesForRuntimePack != null)
                        {
                            runtimePackItem.SetMetadata(MetadataKeys.AdditionalFrameworkReferences, string.Join(";", additionalFrameworkReferencesForRuntimePack));
                        }

                        if (runtimePackPath != null)
                        {
                            runtimePackItem.SetMetadata(MetadataKeys.PackageDirectory, runtimePackPath);
                        }

                        runtimePacks.Add(runtimePackItem);
                    }

                    if (runtimePackPath == null)
                    {
                        TaskItem packageToDownload = new TaskItem(runtimePackName);
                        packageToDownload.SetMetadata(MetadataKeys.Version, resolvedRuntimePackVersion);

                        packagesToDownload.Add(packageToDownload);
                    }
                }
            }
        }

        private bool AddCrossgen2Package(Version normalizedTargetFrameworkVersion, List<ITaskItem> packagesToDownload)
        {
            var knownCrossgen2Pack = KnownCrossgen2Packs.Where(crossgen2Pack =>
            {
                var packTargetFramework = NuGetFramework.Parse(crossgen2Pack.GetMetadata("TargetFramework"));
                return packTargetFramework.Framework.Equals(TargetFrameworkIdentifier, StringComparison.OrdinalIgnoreCase) &&
                    NormalizeVersion(packTargetFramework.Version) == normalizedTargetFrameworkVersion;
            }).SingleOrDefault();

            if (knownCrossgen2Pack == null)
            {
                return false;
            }

            var crossgen2PackPattern = knownCrossgen2Pack.GetMetadata("Crossgen2PackNamePattern");
            var crossgen2PackVersion = knownCrossgen2Pack.GetMetadata("Crossgen2PackVersion");
            var crossgen2PackSupportedRuntimeIdentifiers = knownCrossgen2Pack.GetMetadata("Crossgen2RuntimeIdentifiers").Split(';');

            // Get the best RID for the host machine, which will be used to validate that we can run crossgen for the target platform and architecture
            var runtimeGraph = new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath);
            var hostRuntimeIdentifier = NuGetUtils.GetBestMatchingRid(runtimeGraph, NETCoreSdkRuntimeIdentifier, crossgen2PackSupportedRuntimeIdentifiers, out bool wasInGraph);
            if (hostRuntimeIdentifier == null)
            {
                return false;
            }

            var crossgen2PackName = crossgen2PackPattern.Replace("**RID**", hostRuntimeIdentifier);
            if (!string.IsNullOrEmpty(RuntimeFrameworkVersion))
            {
                crossgen2PackVersion = RuntimeFrameworkVersion;
            }

            TaskItem packageToDownload = new TaskItem(crossgen2PackName);
            packageToDownload.SetMetadata(MetadataKeys.Version, crossgen2PackVersion);
            packagesToDownload.Add(packageToDownload);

            Crossgen2Packs = new ITaskItem[1];
            Crossgen2Packs[0] = new TaskItem(crossgen2PackName);
            Crossgen2Packs[0].SetMetadata(MetadataKeys.NuGetPackageId, crossgen2PackName);
            Crossgen2Packs[0].SetMetadata(MetadataKeys.NuGetPackageVersion, crossgen2PackVersion);

            return true;
        }

        private string GetRuntimeFrameworkVersion(
            ITaskItem frameworkReference,
            KnownFrameworkReference knownFrameworkReference,
            KnownRuntimePack? knownRuntimePack,
            out string runtimePackVersion)
        {
            //  Precedence order for selecting runtime framework version
            //  - RuntimeFrameworkVersion metadata on FrameworkReference item
            //  - RuntimeFrameworkVersion MSBuild property
            //  - Then, use either the LatestRuntimeFrameworkVersion or the DefaultRuntimeFrameworkVersion of the KnownFrameworkReference, based on
            //      - The value (if set) of TargetLatestRuntimePatch metadata on the FrameworkReference
            //      - The TargetLatestRuntimePatch MSBuild property (which defaults to True if SelfContained is true, and False otherwise)
            //      - But, if TargetLatestRuntimePatch was defaulted and not overridden by user, then acquire latest runtime pack for future
            //        self-contained deployment (or for crossgen of framework-dependent deployment), while targeting the default version.

            string requestedVersion = GetRequestedRuntimeFrameworkVersion(frameworkReference);
            if (!string.IsNullOrEmpty(requestedVersion))
            {
                runtimePackVersion = requestedVersion;
                return requestedVersion;
            }

            switch (GetRuntimePatchRequest(frameworkReference))
            {
                case RuntimePatchRequest.UseDefaultVersion:
                    runtimePackVersion = knownFrameworkReference.DefaultRuntimeFrameworkVersion;
                    return knownFrameworkReference.DefaultRuntimeFrameworkVersion;

                case RuntimePatchRequest.UseLatestVersion:
                    if (knownRuntimePack != null)
                    {
                        runtimePackVersion = knownRuntimePack?.LatestRuntimeFrameworkVersion;
                        return knownRuntimePack?.LatestRuntimeFrameworkVersion;
                    }
                    else
                    {
                        runtimePackVersion = knownFrameworkReference.DefaultRuntimeFrameworkVersion;
                        return knownFrameworkReference.DefaultRuntimeFrameworkVersion;
                    }
                case RuntimePatchRequest.UseDefaultVersionWithLatestRuntimePack:
                    if (knownRuntimePack != null)
                    {
                        runtimePackVersion = knownRuntimePack?.LatestRuntimeFrameworkVersion;
                    }
                    else
                    {
                        runtimePackVersion = knownFrameworkReference.DefaultRuntimeFrameworkVersion;
                    }
                    return knownFrameworkReference.DefaultRuntimeFrameworkVersion;

                default:
                    // Unreachable
                    throw new InvalidOperationException();
            }
        }

        private string GetPackPath(string packName, string packVersion)
        {
            IEnumerable<string> GetPackFolders()
            {
                if (!string.IsNullOrEmpty(TargetingPackRoot))
                {
                    yield return TargetingPackRoot;
                }
                var packRootEnvironmentVariable = Environment.GetEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_PACK_ROOTS);
                if (!string.IsNullOrEmpty(packRootEnvironmentVariable))
                {
                    foreach (var packRoot in packRootEnvironmentVariable.Split(Path.PathSeparator))
                    {
                        yield return Path.Combine(packRoot, "packs");
                    }
                }
            }

            foreach (var packFolder in GetPackFolders())
            {
                string packPath = Path.Combine(packFolder, packName, packVersion);
                if (Directory.Exists(packPath))
                {
                    return packPath;
                }
            }

            return null;
        }

        SdkDirectoryWorkloadManifestProvider _workloadManifestProvider;
        WorkloadResolver _workloadResolver;

        private string GetResolvedPackVersion(string packID, string packVersion)
        {
            if (!packVersion.Equals("**FromWorkload**", StringComparison.OrdinalIgnoreCase))
            {
                return packVersion;
            }

            if (_workloadManifestProvider == null)
            {
                _workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(NetCoreRoot, NETCoreSdkVersion);
                _workloadResolver = WorkloadResolver.Create(_workloadManifestProvider, NetCoreRoot, NETCoreSdkVersion);
            }

            var packInfo = _workloadResolver.TryGetPackInfo(new WorkloadPackId(packID));
            if (packInfo == null)
            {
                Log.LogError("NETSDKZZZZ: Error getting pack version: Pack '{0}' was not present in workload manifests.", packID);
                return packVersion;
            }
            return packInfo.Version;
        }

        private enum RuntimePatchRequest
        {
            UseDefaultVersionWithLatestRuntimePack,
            UseDefaultVersion,
            UseLatestVersion,
        }

        /// <summary>
        /// Compare PackageToDownload by name and version.
        /// Used to deduplicate PackageToDownloads
        /// </summary>
        private class PackageToDownloadComparer<T> : IEqualityComparer<T> where T : ITaskItem
        {
            public bool Equals(T x, T y)
            {
                if (x is null || y is null)
                {
                    return false;
                }

                return x.ItemSpec.Equals(y.ItemSpec,
                           StringComparison.OrdinalIgnoreCase) &&
                       x.GetMetadata(MetadataKeys.Version).Equals(
                           y.GetMetadata(MetadataKeys.Version), StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(T obj)
            {
                var hashCode = -1923861349;
                hashCode = hashCode * -1521134295 + obj.ItemSpec.GetHashCode();
                hashCode = hashCode * -1521134295 + obj.GetMetadata(MetadataKeys.Version).GetHashCode();
                return hashCode;
            }
        }

        private RuntimePatchRequest GetRuntimePatchRequest(ITaskItem frameworkReference)
        {
            string value = frameworkReference?.GetMetadata("TargetLatestRuntimePatch");
            if (!string.IsNullOrEmpty(value))
            {
                return MSBuildUtilities.ConvertStringToBool(value, defaultValue: false)
                    ? RuntimePatchRequest.UseLatestVersion
                    : RuntimePatchRequest.UseDefaultVersion;
            }

            if (TargetLatestRuntimePatch)
            {
                return RuntimePatchRequest.UseLatestVersion;
            }

            return TargetLatestRuntimePatchIsDefault
                ? RuntimePatchRequest.UseDefaultVersionWithLatestRuntimePack
                : RuntimePatchRequest.UseDefaultVersion;
        }

        private string GetRequestedRuntimeFrameworkVersion(ITaskItem frameworkReference)
        {
            string requestedVersion = frameworkReference?.GetMetadata("RuntimeFrameworkVersion");

            if (string.IsNullOrEmpty(requestedVersion))
            {
                requestedVersion = RuntimeFrameworkVersion;
            }

            return requestedVersion;
        }

        internal static Version NormalizeVersion(Version version)
        {
            if (version.Revision == 0)
            {
                if (version.Build == 0)
                {
                    return new Version(version.Major, version.Minor);
                }
                else
                {
                    return new Version(version.Major, version.Minor, version.Build);
                }
            }

            return version;
        }

        private struct KnownFrameworkReference
        {
            ITaskItem _item;
            public KnownFrameworkReference(ITaskItem item)
            {
                _item = item;
                TargetFramework = NuGetFramework.Parse(item.GetMetadata("TargetFramework"));
            }

            //  The name / itemspec of the FrameworkReference used in the project
            public string Name => _item.ItemSpec;

            //  The framework name to write to the runtimeconfig file (and the name of the folder under dotnet/shared)
            public string RuntimeFrameworkName => _item.GetMetadata(MetadataKeys.RuntimeFrameworkName);
            public string DefaultRuntimeFrameworkVersion => _item.GetMetadata("DefaultRuntimeFrameworkVersion");

            //  The ID of the targeting pack NuGet package to reference
            public string TargetingPackName => _item.GetMetadata("TargetingPackName");
            public string TargetingPackVersion => _item.GetMetadata("TargetingPackVersion");
            public string TargetingPackFormat => _item.GetMetadata("TargetingPackFormat");

            public string RuntimePackRuntimeIdentifiers => _item.GetMetadata(MetadataKeys.RuntimePackRuntimeIdentifiers);

            public bool IsWindowsOnly => _item.HasMetadataValue("IsWindowsOnly", "true");
            
            public bool RuntimePackAlwaysCopyLocal =>
                _item.HasMetadataValue(MetadataKeys.RuntimePackAlwaysCopyLocal, "true");

            public string Profile => _item.GetMetadata("Profile");

            public NuGetFramework TargetFramework { get; }

            public KnownRuntimePack ToKnownRuntimePack()
            {
                return new KnownRuntimePack(_item);
            }
        }

        private struct KnownRuntimePack
        {
            ITaskItem _item;

            public KnownRuntimePack(ITaskItem item)
            {
                _item = item;
                TargetFramework = NuGetFramework.Parse(item.GetMetadata("TargetFramework"));
                string runtimePackLabels = item.GetMetadata(MetadataKeys.RuntimePackLabels);
                if (string.IsNullOrEmpty(runtimePackLabels))
                {
                    RuntimePackLabels = Array.Empty<string>();
                }
                else
                {
                    RuntimePackLabels = runtimePackLabels.Split(';');
                }
            }

            //  The name / itemspec of the FrameworkReference used in the project
            public string Name => _item.ItemSpec;

            ////  The framework name to write to the runtimeconfig file (and the name of the folder under dotnet/shared)
            public string LatestRuntimeFrameworkVersion => _item.GetMetadata("LatestRuntimeFrameworkVersion");

            public string RuntimePackNamePatterns => _item.GetMetadata("RuntimePackNamePatterns");

            public string RuntimePackRuntimeIdentifiers => _item.GetMetadata(MetadataKeys.RuntimePackRuntimeIdentifiers);

            public string IsTrimmable => _item.GetMetadata(MetadataKeys.IsTrimmable);

            public bool IsWindowsOnly => _item.HasMetadataValue("IsWindowsOnly", "true");

            public bool RuntimePackAlwaysCopyLocal =>
                _item.HasMetadataValue(MetadataKeys.RuntimePackAlwaysCopyLocal, "true");

            public string[] RuntimePackLabels { get; }

            public NuGetFramework TargetFramework { get; }
        }
    }
}
