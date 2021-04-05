// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Product = Microsoft.DotNet.Cli.Utils.Product;
using EnvironmentProvider = Microsoft.DotNet.NativeWrapper.EnvironmentProvider;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.DotNet.Configurer;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadInstallCommand : CommandBase
    {
        private readonly IReporter _reporter;
        private readonly bool _skipManifestUpdate;
        private readonly string _fromCacheOption;
        private readonly bool _printDownloadLinkOnly;
        private IReadOnlyCollection<string> _workloadIds;
        private WorkloadInstallManager _workloadInstallManager;

        public readonly string MockInstallDirectory = Path.Combine(CliFolderPathCalculator.DotnetUserProfileFolderPath,
            "DEV_mockworkloads");

        public WorkloadInstallCommand(
            ParseResult parseResult,
            IReporter reporter = null)
            : base(parseResult)
        {
            _reporter = reporter ?? Reporter.Output;
            _skipManifestUpdate = parseResult.ValueForOption<bool>(WorkloadInstallCommandParser.SkipManifestUpdateOption);
            _printDownloadLinkOnly = parseResult.ValueForOption<bool>(WorkloadInstallCommandParser.PrintDownloadLinkOnlyOption);
            _fromCacheOption = parseResult.ValueForOption<string>(WorkloadInstallCommandParser.FromCacheOption);
            _workloadIds = parseResult.ValueForArgument<IReadOnlyCollection<string>>(WorkloadInstallCommandParser.WorkloadIdArgument);

            var dotnetPath = EnvironmentProvider.GetDotnetExeDirectory();
            var sdkVersion = new ReleaseVersion(Product.Version);
            var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(dotnetPath, sdkVersion.ToString());
            var workloadResolver = WorkloadResolver.Create(workloadManifestProvider, dotnetPath, sdkVersion.ToString());
            var workloadInstaller = WorkloadInstallerFactory.GetWorkloadInstaller(_reporter);
            _workloadInstallManager = new WorkloadInstallManager(_reporter, workloadInstaller, workloadResolver);
        }

        public override int Execute()
        {
            if (_printDownloadLinkOnly || !string.IsNullOrWhiteSpace(_fromCacheOption))
            {
                Reporter.Output.WriteLine($"WIP workload install {string.Join("; ", _workloadIds)}");
                List<string> allowedMockWorkloads = new List<string> {"mobile-ios", "mobile-android"};

                if (_workloadIds.Except(allowedMockWorkloads).Any())
                {
                    Reporter.Output.WriteLine("Only support \"mobile-ios\", \"mobile-android\" in the mock");
                }

                SourceRepository source =
                    Repository.Factory.GetCoreV3("https://www.myget.org/F/mockworkloadfeed/api/v3/index.json");
                ServiceIndexResourceV3 serviceIndexResource = source.GetResourceAsync<ServiceIndexResourceV3>().Result;
                IReadOnlyList<Uri> packageBaseAddress =
                    serviceIndexResource?.GetServiceEntryUris(ServiceTypes.PackageBaseAddress);
                List<string> allPackageUrl = new List<string>();

                if (_printDownloadLinkOnly)
                {
                    if (_workloadIds.Contains("mobile-ios"))
                    {
                        allPackageUrl.Add(nupkgUrl(packageBaseAddress.First().ToString(), "Microsoft.iOS.Bundle",
                            NuGetVersion.Parse("6.0.100")));

                        AddNewtonsoftJson(allPackageUrl);
                    }

                    if (_workloadIds.Contains("mobile-android"))
                    {
                        allPackageUrl.Add(nupkgUrl(packageBaseAddress.First().ToString(), "Microsoft.NET.Workload.Android",
                            NuGetVersion.Parse("6.0.100")));


                        AddNewtonsoftJson(allPackageUrl);
                    }

                    Reporter.Output.WriteLine("==allPackageLinksJsonOutputStart==");
                    Reporter.Output.WriteLine(JsonSerializer.Serialize(allPackageUrl));
                    Reporter.Output.WriteLine("==allPackageLinksJsonOutputEnd==");
                }

                if (!string.IsNullOrWhiteSpace(_fromCacheOption))
                {
                    Directory.CreateDirectory(MockInstallDirectory);
                    if (_workloadIds.Contains("mobile-android"))
                    {
                        File.Copy(Path.Combine(_fromCacheOption, "Microsoft.NET.Workload.Android.6.0.100.nupkg"),
                            Path.Combine(MockInstallDirectory, "Microsoft.NET.Workload.Android.6.0.100.nupkg"));
                    }

                    if (_workloadIds.Contains("mobile-ios"))
                    {
                        File.Copy(Path.Combine(_fromCacheOption, "Microsoft.iOS.Bundle.6.0.100.nupkg"),
                            Path.Combine(MockInstallDirectory, "Microsoft.iOS.Bundle.6.0.100.nupkg"));
                    }
                }
            }
            else
            {
                _workloadInstallManager.InstallWorkloads(_workloadIds.Select(id => new WorkloadId(id)), _skipManifestUpdate);
            }

            return 0;
        }

        // Add a Newtonsoft.Json to make sure caller can handle multiple packages
        private static void AddNewtonsoftJson(List<string> allPackageUrl)
        {
            string newtonsoftJsonUrl = "https://www.nuget.org/api/v2/package/Newtonsoft.Json/13.0.1-beta2";
            if (!allPackageUrl.Contains(newtonsoftJsonUrl))
            {
                allPackageUrl.Add(newtonsoftJsonUrl);
            }
        }

        public string nupkgUrl(string baseUri, string id, NuGetVersion version) =>
            baseUri + id.ToLowerInvariant() + "/" + version.ToNormalizedString() + "/" + id.ToLowerInvariant() +
            "." +
            version.ToNormalizedString() + ".nupkg";
    }
}
