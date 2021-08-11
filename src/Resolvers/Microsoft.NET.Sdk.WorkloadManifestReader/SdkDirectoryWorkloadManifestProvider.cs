// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class SdkDirectoryWorkloadManifestProvider : IWorkloadManifestProvider
    {
        private readonly string _sdkRootPath;
        private readonly string _sdkVersionBand;
        private readonly string [] _manifestDirectories;
        private static HashSet<string> _outdatedManifestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "microsoft.net.workload.android", "microsoft.net.workload.blazorwebassembly", "microsoft.net.workload.ios",
            "microsoft.net.workload.maccatalyst", "microsoft.net.workload.macos", "microsoft.net.workload.tvos" };

        public SdkDirectoryWorkloadManifestProvider(string sdkRootPath, string sdkVersion)
            : this(sdkRootPath, sdkVersion, Environment.GetEnvironmentVariable)
        {

        }

        internal SdkDirectoryWorkloadManifestProvider(string sdkRootPath, string sdkVersion, Func<string, string?> getEnvironmentVariable)
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

            if (!Version.TryParse(sdkVersion.Split('-')[0], out var sdkVersionParsed))
            {
                throw new ArgumentException($"'{nameof(sdkVersion)}' should be a version, but get {sdkVersion}");
            }

            static int Last2DigitsTo0(int versionBuild)
            {
                return (versionBuild / 100) * 100;
            }

            var sdkVersionBand =
                $"{sdkVersionParsed.Major}.{sdkVersionParsed.Minor}.{Last2DigitsTo0(sdkVersionParsed.Build)}";

            _sdkRootPath = sdkRootPath;
            _sdkVersionBand = sdkVersionBand;

            var manifestDirectory = Path.Combine(_sdkRootPath, "sdk-manifests", _sdkVersionBand);

            var manifestDirectoryEnvironmentVariable = getEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_MANIFEST_ROOTS);
            if (manifestDirectoryEnvironmentVariable != null)
            {
                //  Append the SDK version band to each manifest root specified via the environment variable.  This allows the same
                //  environment variable settings to be shared by multiple SDKs.
                _manifestDirectories = manifestDirectoryEnvironmentVariable.Split(Path.PathSeparator)
                    .Select(p => Path.Combine(p, _sdkVersionBand))
                    .Append(manifestDirectory).ToArray();
            }
            else
            {
                _manifestDirectories = new[] { manifestDirectory };
            }
        }

        public IEnumerable<(string manifestId, string? informationalPath, Func<Stream> openManifestStream)> GetManifests()
        {
            foreach (var workloadManifestDirectory in GetManifestDirectories())
            {
                var workloadManifest = Path.Combine(workloadManifestDirectory, "WorkloadManifest.json");
                var id = Path.GetFileName(workloadManifestDirectory);
                yield return (id, workloadManifest, () => File.OpenRead(workloadManifest));
            }
        }

        public IEnumerable<string> GetManifestDirectories()
        {
            if (_manifestDirectories.Length == 1)
            {
                //  Optimization for common case where test hook to add additional directories isn't being used
                if (Directory.Exists(_manifestDirectories[0]))
                {
                    foreach (var workloadManifestDirectory in Directory.EnumerateDirectories(_manifestDirectories[0]))
                    {
                        if (!IsManifestIdOutdated(workloadManifestDirectory))
                        {
                            yield return workloadManifestDirectory;
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
                        yield return workloadManifestDirectory;
                    }
                }
            }
        }

        private bool IsManifestIdOutdated(string workloadManifestDir)
        {
            var manifestId = Path.GetFileName(workloadManifestDir);
            return _outdatedManifestIds.Contains(manifestId);
        }

        public string GetSdkFeatureBand()
        {
            return _sdkVersionBand;
        }
    }
}
