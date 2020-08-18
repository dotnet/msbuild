// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.DotNetSdkResolver;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.TemplateLocator
{
    public sealed class TemplateLocator
    {
        private readonly Lazy<NETCoreSdkResolver> _netCoreSdkResolver;

        public TemplateLocator()
            : this(Environment.GetEnvironmentVariable, VSSettings.Ambient)
        {
        }

        /// <summary>
        /// Test constructor
        /// </summary>
        public TemplateLocator(Func<string, string> getEnvironmentVariable, VSSettings vsSettings)
        {
            _netCoreSdkResolver =
                new Lazy<NETCoreSdkResolver>(() => new NETCoreSdkResolver(getEnvironmentVariable, vsSettings));
        }

        public IReadOnlyCollection<IOptionalSdkTemplatePackageInfo> GetDotnetSdkTemplatePackages(
            string sdkVersion,
            string dotnetRootPath)
        {
            if (string.IsNullOrWhiteSpace(sdkVersion))
            {
                throw new ArgumentException($"'{nameof(sdkVersion)}' cannot be null or whitespace", nameof(sdkVersion));
            }

            if (string.IsNullOrWhiteSpace(dotnetRootPath))
            {
                throw new ArgumentException($"'{nameof(dotnetRootPath)}' cannot be null or whitespace", nameof(dotnetRootPath));
            }

            if (!Version.TryParse(sdkVersion.Split('-')[0], out var sdkVersionParsed))
            {
                throw new ArgumentException($"'{nameof(sdkVersion)}' should be a version, but get {sdkVersion}");
            }

            bool FindSameVersionBand(DirectoryInfo manifestDirectory)
            {
                var directoryVersion = Version.Parse(manifestDirectory.Name);
                return directoryVersion.Major == sdkVersionParsed.Major &&
                       directoryVersion.Minor == sdkVersionParsed.Minor &&
                       (directoryVersion.Revision / 100) == (sdkVersionParsed.Revision / 100);
            }

            var bondManifestDirectory = new DirectoryInfo(Path.Combine(dotnetRootPath, "workloadmanifests")).GetDirectories().Where(FindSameVersionBand)
                .OrderByDescending(d => Version.Parse(d.Name)).FirstOrDefault();

            if (bondManifestDirectory == null)
            {
                return Array.Empty<IOptionalSdkTemplatePackageInfo>();
            }

            var manifestContent =
                WorkloadManifest.LoadFromFolder(bondManifestDirectory.FullName);
            var dotnetSdkTemplatePackages = new List<IOptionalSdkTemplatePackageInfo>();
            foreach (var pack in manifestContent.SdkPackDetail)
            {
                if (pack.Value.kind.Equals("Template", StringComparison.OrdinalIgnoreCase))
                {
                    var optionalSdkTemplatePackageInfo = new OptionalSdkTemplatePackageInfo(
                        pack.Key,
                        pack.Value.version,
                        Path.Combine(dotnetRootPath, "template-packs",
                            pack.Key.ToLower() + "." + pack.Value.version.ToLower() + ".nupkg"));
                    dotnetSdkTemplatePackages.Add(optionalSdkTemplatePackageInfo);
                }
            }

            return dotnetSdkTemplatePackages;
        }

        public bool TryGetDotnetSdkVersionUsedInVs(string vsVersion, out string sdkVersion)
        {
            string dotnetExeDir = _netCoreSdkResolver.Value.GetDotnetExeDirectory();

            if (!Version.TryParse(vsVersion, out var parsedVsVersion))
            {
                throw new ArgumentException(vsVersion + " is not a valid version");
            }

            // VS major minor version will match msbuild major minor
            // and for resolve SDK, major minor version is enough
            var msbuildMajorMinorVersion = new Version(parsedVsVersion.Major, parsedVsVersion.Minor, 0);

            var resolverResult =
                _netCoreSdkResolver.Value.ResolveNETCoreSdkDirectory(null, msbuildMajorMinorVersion, true, dotnetExeDir);

            if (resolverResult.ResolvedSdkDirectory == null)
            {
                sdkVersion = null;
                return false;
            }
            else
            {
                sdkVersion = new DirectoryInfo(resolverResult.ResolvedSdkDirectory).Name;
                return true;
            }
        }

        private class OptionalSdkTemplatePackageInfo : IOptionalSdkTemplatePackageInfo
        {
            public OptionalSdkTemplatePackageInfo(string templatePackageId, string templateVersion, string path)
            {
                TemplatePackageId = templatePackageId ?? throw new ArgumentNullException(nameof(templatePackageId));
                TemplateVersion = templateVersion ?? throw new ArgumentNullException(nameof(templateVersion));
                Path = path ?? throw new ArgumentNullException(nameof(path));
            }

            public string TemplatePackageId { get; }
            public string TemplateVersion { get; }
            public string Path { get; }
        }
    }
}
