// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.DotNet.DotNetSdkResolver;
using Microsoft.Net.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.TemplateLocator
{
    public sealed class TemplateLocator
    {
        private IWorkloadManifestProvider _workloadManifestProvider;
        private readonly Lazy<NETCoreSdkResolver> _netCoreSdkResolver;

        public TemplateLocator()
            : this(Environment.GetEnvironmentVariable, VSSettings.Ambient, null)
        {
        }

        /// <summary>
        /// Test constructor
        /// </summary>
        public TemplateLocator(Func<string, string> getEnvironmentVariable, VSSettings vsSettings,
            IWorkloadManifestProvider workloadManifestProvider)
        {
            _netCoreSdkResolver =
                new Lazy<NETCoreSdkResolver>(() => new NETCoreSdkResolver(getEnvironmentVariable, vsSettings));

            _workloadManifestProvider = workloadManifestProvider;
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
                throw new ArgumentException($"'{nameof(dotnetRootPath)}' cannot be null or whitespace",
                    nameof(dotnetRootPath));
            }

            if (!Version.TryParse(sdkVersion.Split('-')[0], out var sdkVersionParsed))
            {
                throw new ArgumentException($"'{nameof(sdkVersion)}' should be a version, but get {sdkVersion}");
            }

            // set the patch version to be x00
            var sdkVersionBand =
                $"{sdkVersionParsed.Major}.{sdkVersionParsed.Minor}.{(sdkVersionParsed.Revision / 100) * 100}";

            _workloadManifestProvider ??= new SdkDirectoryWorkloadManifestProvider(dotnetRootPath, sdkVersionBand);

            var workloadResolver = new WorkloadResolver(_workloadManifestProvider, dotnetRootPath);
            var templates = workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Template);

            var dotnetSdkTemplatePackages = new List<IOptionalSdkTemplatePackageInfo>();
            foreach (var pack in templates)
            {
                var optionalSdkTemplatePackageInfo = new OptionalSdkTemplatePackageInfo(
                    pack.Id,
                    pack.Version,
                    Path.Combine(dotnetRootPath, "template-packs",
                        pack.Id.ToLower() + "." + pack.Version.ToLower() + ".nupkg"));
                dotnetSdkTemplatePackages.Add(optionalSdkTemplatePackageInfo);
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
                _netCoreSdkResolver.Value.ResolveNETCoreSdkDirectory(null, msbuildMajorMinorVersion, true,
                    dotnetExeDir);

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
