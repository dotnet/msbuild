// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Utils
{
    public class ToolPathCalculator
    {
        private readonly string _packagesDirectory;

        public ToolPathCalculator(string packagesDirectory)
        {
            _packagesDirectory = packagesDirectory;
        }

        public string GetBestLockFilePath(string packageId, VersionRange versionRange, NuGetFramework framework)
        {
            if (versionRange == null)
            {
                throw new ArgumentNullException(nameof(versionRange));
            }

            if (framework == null)
            {
                throw new ArgumentNullException(nameof(framework));
            }

            var availableToolVersions = GetAvailableToolVersions(packageId);

            var bestVersion = versionRange.FindBestMatch(availableToolVersions);
            if (bestVersion == null)
            {
                throw new GracefulException($"Version for package `{packageId}` could not be resolved.");
            }

            return GetLockFilePath(packageId, bestVersion, framework);
        }

        public string GetLockFilePath(string packageId, NuGetVersion version, NuGetFramework framework)
        {
            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (framework == null)
            {
                throw new ArgumentNullException(nameof(framework));
            }

            return Path.Combine(
                GetBaseToolPath(packageId),
                version.ToNormalizedString().ToLowerInvariant(),
                framework.GetShortFolderName(),
                "project.assets.json");
        }

        private string GetBaseToolPath(string packageId)
        {
            return Path.Combine(
                _packagesDirectory,
                ".tools",
                packageId.ToLowerInvariant());
        }

        private IEnumerable<NuGetVersion> GetAvailableToolVersions(string packageId)
        {
            var availableVersions = new List<NuGetVersion>();

            var toolBase = GetBaseToolPath(packageId);
            var versionDirectories = Directory.EnumerateDirectories(toolBase);

            foreach (var versionDirectory in versionDirectories)
            {
                var version = Path.GetFileName(versionDirectory);

                NuGetVersion nugetVersion = null;
                NuGetVersion.TryParse(version, out nugetVersion);

                if (nugetVersion != null)
                {
                    availableVersions.Add(nugetVersion);
                }
            }

            return availableVersions;
        }

    }
}