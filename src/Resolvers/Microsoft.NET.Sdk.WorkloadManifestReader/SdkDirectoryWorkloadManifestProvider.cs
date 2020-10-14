// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class SdkDirectoryWorkloadManifestProvider : IWorkloadManifestProvider
    {
        private readonly string _sdkRootPath;
        private readonly string _sdkVersionBand;
        private readonly string _manifestDirectory;

        public SdkDirectoryWorkloadManifestProvider(string sdkRootPath, string sdkVersion)
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

            var manifestDirectoryEnvironmentVariable = Environment.GetEnvironmentVariable("DOTNETSDK_WORKLOAD_MANIFEST_ROOT");
            if (!string.IsNullOrEmpty(manifestDirectoryEnvironmentVariable))
            {
                _manifestDirectory = manifestDirectoryEnvironmentVariable;
            }
            else
            {
                _manifestDirectory = Path.Combine(_sdkRootPath, "sdk-manifests", _sdkVersionBand);
            }
        }

        public IEnumerable<Stream> GetManifests()
        {
            foreach (var workloadManifestDirectory in GetManifestDirectories())
            {
                var workloadManifest = Path.Combine(workloadManifestDirectory, "WorkloadManifest.json");
                yield return File.OpenRead(workloadManifest);
            }
        }

        public IEnumerable<string> GetManifestDirectories()
        {
            if (Directory.Exists(_manifestDirectory))
            {
                foreach (var workloadManifestDirectory in Directory.EnumerateDirectories(_manifestDirectory))
                {
                    yield return workloadManifestDirectory;
                }
            }
        }
    }
}
