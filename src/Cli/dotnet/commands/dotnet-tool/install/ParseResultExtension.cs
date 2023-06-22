// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tools.Tool.Install
{
    internal static class ParseResultExtension
    {
        public static VersionRange GetVersionRange(this ParseResult parseResult)
        {
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

            return versionRange;
        }
    }
}
