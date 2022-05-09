// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Workloads.Workload;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class SdkDirectoryWorkloadManifestProvider : IWorkloadManifestProvider
    {
        private readonly string _sdkRootPath;
        private readonly SdkFeatureBand _sdkVersionBand;
        private readonly string [] _manifestDirectories;
        private static HashSet<string> _outdatedManifestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "microsoft.net.workload.android", "microsoft.net.workload.blazorwebassembly", "microsoft.net.workload.ios",
            "microsoft.net.workload.maccatalyst", "microsoft.net.workload.macos", "microsoft.net.workload.tvos" };
        private readonly HashSet<string>? _knownManifestIds;

        public SdkDirectoryWorkloadManifestProvider(string sdkRootPath, string sdkVersion, string? userProfileDir)
            : this(sdkRootPath, sdkVersion, Environment.GetEnvironmentVariable, userProfileDir)
        {

        }

        internal SdkDirectoryWorkloadManifestProvider(string sdkRootPath, string sdkVersion, Func<string, string?> getEnvironmentVariable, string? userProfileDir)
        {
            if (string.IsNullOrWhiteSpace(sdkVersion))
            {
                throw new ArgumentException($"'{nameof(sdkVersion)}' cannot be null or whitespace", nameof(sdkVersion));
            }

            if (string.IsNullOrWhiteSpace(sdkRootPath))
            {
                throw new ArgumentException($"'{nameof(sdkRootPath)}' cannot be null or whitespace",
                    nameof(sdkRootPath));
            }

            _sdkRootPath = sdkRootPath;
            _sdkVersionBand = new SdkFeatureBand(sdkVersion);

            var knownManifestIdsFilePath = Path.Combine(_sdkRootPath, "sdk", sdkVersion, "IncludedWorkloadManifests.txt");
            if (File.Exists(knownManifestIdsFilePath))
            {
                _knownManifestIds = File.ReadAllLines(knownManifestIdsFilePath).Where(l => !string.IsNullOrEmpty(l)).ToHashSet();
            }

            string? userManifestsDir = userProfileDir is null ? null : Path.Combine(userProfileDir, "sdk-manifests", _sdkVersionBand.ToString());
            string dotnetManifestDir = Path.Combine(_sdkRootPath, "sdk-manifests", _sdkVersionBand.ToString());
            if (userManifestsDir != null && WorkloadFileBasedInstall.IsUserLocal(_sdkRootPath, _sdkVersionBand.ToString()) && Directory.Exists(userManifestsDir))
            {
                _manifestDirectories = new[] { userManifestsDir, dotnetManifestDir };
            }
            else
            {
                _manifestDirectories = new[] { dotnetManifestDir };
            }

            var manifestDirectoryEnvironmentVariable = getEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_MANIFEST_ROOTS);
            if (manifestDirectoryEnvironmentVariable != null)
            {
                //  Append the SDK version band to each manifest root specified via the environment variable.  This allows the same
                //  environment variable settings to be shared by multiple SDKs.
                _manifestDirectories = manifestDirectoryEnvironmentVariable.Split(Path.PathSeparator)
                    .Select(p => Path.Combine(p, _sdkVersionBand.ToString()))
                    .Concat(_manifestDirectories).ToArray();
            }
        }

        public IEnumerable<ReadableWorkloadManifest> GetManifests()
        {
            foreach (var workloadManifestDirectory in GetManifestDirectories())
            {
                var workloadManifestPath = Path.Combine(workloadManifestDirectory, "WorkloadManifest.json");
                var id = Path.GetFileName(workloadManifestDirectory);

                yield return new(
                    id,
                    workloadManifestPath,
                    () => File.OpenRead(workloadManifestPath),
                    () => WorkloadManifestReader.TryOpenLocalizationCatalogForManifest(workloadManifestPath)
                );
            }
        }

        public IEnumerable<string> GetManifestDirectories()
        {
            var manifestIdsToDirectories = new Dictionary<string, string>();
            if (_manifestDirectories.Length == 1)
            {
                //  Optimization for common case where test hook to add additional directories isn't being used
                if (Directory.Exists(_manifestDirectories[0]))
                {
                    foreach (var workloadManifestDirectory in Directory.EnumerateDirectories(_manifestDirectories[0]))
                    {
                        if (!IsManifestIdOutdated(workloadManifestDirectory))
                        {
                            manifestIdsToDirectories.Add(Path.GetFileName(workloadManifestDirectory), workloadManifestDirectory);
                        }
                    }
                }
            }
            else
            {
                //  If the same folder name is in multiple of the workload manifest directories, take the first one
                Dictionary<string, string> directoriesWithManifests = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var manifestDirectory in _manifestDirectories.Reverse())
                {
                    if (Directory.Exists(manifestDirectory))
                    {
                        foreach (var workloadManifestDirectory in Directory.EnumerateDirectories(manifestDirectory))
                        {
                            directoriesWithManifests[Path.GetFileName(workloadManifestDirectory)] = workloadManifestDirectory;
                        }
                    }
                }

                foreach (var workloadManifestDirectory in directoriesWithManifests.Values)
                {
                    if (!IsManifestIdOutdated(workloadManifestDirectory))
                    {
                        manifestIdsToDirectories.Add(Path.GetFileName(workloadManifestDirectory), workloadManifestDirectory);
                    }
                }
            }

            if (_knownManifestIds != null && _knownManifestIds.Any(id => !manifestIdsToDirectories.ContainsKey(id)))
            {
                var missingManifestIds = _knownManifestIds.Where(id => !manifestIdsToDirectories.ContainsKey(id));
                foreach (var missingManifestId in missingManifestIds)
                {
                    var manifestDir = FallbackForMissingManifest(missingManifestId);
                    if (!string.IsNullOrEmpty(manifestDir))
                    {
                        manifestIdsToDirectories.Add(missingManifestId, manifestDir);
                    }
                }
            }

            return manifestIdsToDirectories.Values;
        }

        private string FallbackForMissingManifest(string manifestId)
        {
            var sdkManifestPath = Path.Combine(_sdkRootPath, "sdk-manifests");
            if (!Directory.Exists(sdkManifestPath))
            {
                return string.Empty;
            }

            var candidateFeatureBands = Directory.GetDirectories(sdkManifestPath)
                .Select(dir => Path.GetFileName(dir))
                .Select(featureBand => new SdkFeatureBand(featureBand))
                .Where(featureBand => featureBand < _sdkVersionBand || _sdkVersionBand.ToStringWithoutPrerelease().Equals(featureBand.ToString(), StringComparison.Ordinal));
            var matchingManifestFatureBands = candidateFeatureBands
                .Where(featureBand => Directory.Exists(Path.Combine(sdkManifestPath, featureBand.ToString(), manifestId)));
            if (matchingManifestFatureBands.Any())
            {
                return Path.Combine(sdkManifestPath, matchingManifestFatureBands.Max()!.ToString(), manifestId);
            }
            else
            {
                // Manifest does not exist
                return string.Empty;
            }
        }

        private bool IsManifestIdOutdated(string workloadManifestDir)
        {
            var manifestId = Path.GetFileName(workloadManifestDir);
            return _outdatedManifestIds.Contains(manifestId);
        }

        public string GetSdkFeatureBand()
        {
            return _sdkVersionBand.ToString();
        }
    }
}
