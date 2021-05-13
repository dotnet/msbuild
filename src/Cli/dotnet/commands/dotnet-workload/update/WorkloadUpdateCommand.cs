// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.MSBuildSdkResolver;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.DotNet.Workloads.Workload.Update
{
    internal class WorkloadUpdateCommand : CommandBase
    {
        private readonly bool _printDownloadLinkOnly;
        private readonly string _fromCacheOption;

        public static readonly string MockUpdateDirectory = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),
            "DEV_mockworkloads", "update");

        private readonly string _sdkVersion;

        public WorkloadUpdateCommand(
            ParseResult parseResult)
            : base(parseResult)
        {
            _printDownloadLinkOnly =
                parseResult.ValueForOption<bool>(WorkloadInstallCommandParser.PrintDownloadLinkOnlyOption);
            _fromCacheOption = parseResult.ValueForOption<string>(WorkloadInstallCommandParser.FromCacheOption);
            _sdkVersion = parseResult.ValueForOption<string>(WorkloadUpdateCommandParser.SdkVersionOption);
        }

        public override int Execute()
        {
            if (_printDownloadLinkOnly || !string.IsNullOrWhiteSpace(_fromCacheOption))
            {
                if (string.IsNullOrWhiteSpace(_sdkVersion))
                {
                    throw new ArgumentException(
                        "SDK version is require when using --print-download-link-only option. Since the update may cross version band");
                }

                SourceRepository source =
                    Repository.Factory.GetCoreV3("https://www.myget.org/F/mockworkloadfeed/api/v3/index.json");
                ServiceIndexResourceV3 serviceIndexResource = source.GetResourceAsync<ServiceIndexResourceV3>().Result;
                IReadOnlyList<Uri> packageBaseAddress =
                    serviceIndexResource?.GetServiceEntryUris(ServiceTypes.PackageBaseAddress);
                List<string> allPackageUrl = new List<string>();

                if (_printDownloadLinkOnly)
                {
                    allPackageUrl.Add(nupkgUrl(packageBaseAddress.First().ToString(), "Microsoft.iOS.Bundle",
                        NuGetVersion.Parse("6.0.100")));

                    allPackageUrl.Add(nupkgUrl(packageBaseAddress.First().ToString(), "Microsoft.NET.Workload.Android",
                        NuGetVersion.Parse("6.0.100")));

                    Reporter.Output.WriteLine("==allPackageLinksJsonOutputStart==");
                    Reporter.Output.WriteLine(JsonSerializer.Serialize(allPackageUrl));
                    Reporter.Output.WriteLine("==allPackageLinksJsonOutputEnd==");
                }

                if (!string.IsNullOrWhiteSpace(_fromCacheOption))
                {
                    Directory.CreateDirectory(MockUpdateDirectory);

                    File.Copy(Path.Combine(_fromCacheOption, "Microsoft.NET.Workload.Android.6.0.100.nupkg"),
                        Path.Combine(MockUpdateDirectory, "Microsoft.NET.Workload.Android.6.0.100.nupkg"));

                    File.Copy(Path.Combine(_fromCacheOption, "Microsoft.iOS.Bundle.6.0.100.nupkg"),
                        Path.Combine(MockUpdateDirectory, "Microsoft.iOS.Bundle.6.0.100.nupkg"));
                }
            }
            else
            {
                throw new NotImplementedException("Only have mock behavior, need '--print-download-link-only'");
            }

            return 0;
        }

        public string nupkgUrl(string baseUri, string id, NuGetVersion version) =>
            baseUri + id.ToLowerInvariant() + "/" + version.ToNormalizedString() + "/" + id.ToLowerInvariant() +
            "." +
            version.ToNormalizedString() + ".nupkg";
    }
}
