// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NugetSearch;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Search;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tools.Tool.Install
{
    internal static class ParseResultExtension
    {
        public static VersionRange GetVersionRange(this ParseResult parseResult)
        {
            string packageId = parseResult.GetValue(ToolInstallCommandParser.PackageIdArgument);
            string packageVersion = parseResult.GetValue(ToolInstallCommandParser.VersionOption);
            bool prerelease = parseResult.GetValue(ToolInstallCommandParser.PrereleaseOption);

            if (!string.IsNullOrEmpty(packageVersion) && prerelease)
            {
                throw new GracefulException(
                    string.Format(
                        LocalizableStrings.PrereleaseAndVersionAreNotSupportedAtTheSameTime,
                        packageVersion));
            }

            if (prerelease)
            {
                packageVersion = "*-*";
            }

            VersionRange versionRange = null;
            if (!string.IsNullOrEmpty(packageVersion) && !VersionRange.TryParse(packageVersion, out versionRange))
            {
                throw new GracefulException(
                    string.Format(
                        LocalizableStrings.InvalidNuGetVersionRange,
                        packageVersion));
            }
            
            if (string.IsNullOrEmpty(packageVersion))
            {
                var nugetToolSearchApiRequest = new NugetToolSearchApiRequest();
                NugetSearchApiParameter nugetSearchApiParameter = new(searchTerm: packageId, prerelease: prerelease);
                IReadOnlyCollection<SearchResultPackage> searchResultPackages =
                    NugetSearchApiResultDeserializer.Deserialize(
                        nugetToolSearchApiRequest.GetResult(nugetSearchApiParameter).GetAwaiter().GetResult());
                var packageData = searchResultPackages.Where(p => p.Id.ToString().Equals(packageId)).FirstOrDefault();
                if (packageData != null)
                {
                    string latestVersion = packageData.LatestVersion;
                    if (!VersionRange.TryParse(latestVersion, out versionRange))
                    {
                        throw new GracefulException(
                            string.Format(
                                LocalizableStrings.InvalidNuGetVersionRange,
                                latestVersion));
                    }
                }
            }
            return versionRange;
        }
    }
}
