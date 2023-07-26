// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.DotNetSdkResolver;
using Microsoft.DotNet.NativeWrapper;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.TemplateLocator
{
    public sealed class TemplateLocator
    {
        private IWorkloadManifestProvider? _workloadManifestProvider;
        private IWorkloadResolver? _workloadResolver;
        private readonly Lazy<NETCoreSdkResolver> _netCoreSdkResolver;
        private readonly Func<string, string> _getEnvironmentVariable;
#nullable disable
        public TemplateLocator()
            : this(Environment.GetEnvironmentVariable, VSSettings.Ambient, null, null)
        {
        }
#nullable restore

        /// <summary>
        /// Test constructor
        /// </summary>
        public TemplateLocator(Func<string, string> getEnvironmentVariable, VSSettings vsSettings,
            IWorkloadManifestProvider? workloadManifestProvider, IWorkloadResolver? workloadResolver)
        {
            _netCoreSdkResolver =
                new Lazy<NETCoreSdkResolver>(() => new NETCoreSdkResolver(getEnvironmentVariable, vsSettings));

            _workloadManifestProvider = workloadManifestProvider;
            _workloadResolver = workloadResolver;
            _getEnvironmentVariable = getEnvironmentVariable;
        }

        public IReadOnlyCollection<IOptionalSdkTemplatePackageInfo> GetDotnetSdkTemplatePackages(
            string sdkVersion,
            string dotnetRootPath,
            string? userProfileDir)
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

            //  Will the current directory correspond to the folder we are creating a project in?  If we need
            //  to honor global.json workload version selection for template creation in Visual Studio, we may
            //  need to update this interface to pass a folder where we should start the search for global.json
            string? globalJsonPath = SdkDirectoryWorkloadManifestProvider.GetGlobalJsonPath(Environment.CurrentDirectory);

            _workloadManifestProvider ??= new SdkDirectoryWorkloadManifestProvider(dotnetRootPath, sdkVersion, userProfileDir, globalJsonPath);
            _workloadResolver ??= WorkloadResolver.Create(_workloadManifestProvider, dotnetRootPath, sdkVersion, userProfileDir);

            return _workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Template)
                .Select(pack => new OptionalSdkTemplatePackageInfo(pack.Id, pack.Version, pack.Path)).ToList();
        }

        public bool TryGetDotnetSdkVersionUsedInVs(string vsVersion, out string? sdkVersion)
        {
            string dotnetExeDir = EnvironmentProvider.GetDotnetExeDirectory(_getEnvironmentVariable);

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
