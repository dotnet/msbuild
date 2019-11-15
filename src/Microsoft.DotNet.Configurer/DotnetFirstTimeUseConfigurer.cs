// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Configurer
{
    public class DotnetFirstTimeUseConfigurer
    {
        private IReporter _reporter;
        private DotnetFirstRunConfiguration _dotnetFirstRunConfiguration;
        private IFirstTimeUseNoticeSentinel _firstTimeUseNoticeSentinel;
        private IAspNetCertificateSentinel _aspNetCertificateSentinel;
        private IAspNetCoreCertificateGenerator _aspNetCoreCertificateGenerator;
        private IFileSentinel _toolPathSentinel;
        private string _cliFallbackFolderPath;
        private readonly IEnvironmentPath _pathAdder;

        public DotnetFirstTimeUseConfigurer(
            IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel,
            IAspNetCertificateSentinel aspNetCertificateSentinel,
            IAspNetCoreCertificateGenerator aspNetCoreCertificateGenerator,
            IFileSentinel toolPathSentinel,
            DotnetFirstRunConfiguration dotnetFirstRunConfiguration,
            IReporter reporter,
            string cliFallbackFolderPath,
            IEnvironmentPath pathAdder)
        {
            _firstTimeUseNoticeSentinel = firstTimeUseNoticeSentinel;
            _aspNetCertificateSentinel = aspNetCertificateSentinel;
            _aspNetCoreCertificateGenerator = aspNetCoreCertificateGenerator;
            _toolPathSentinel = toolPathSentinel;
            _dotnetFirstRunConfiguration = dotnetFirstRunConfiguration;
            _reporter = reporter;
            _cliFallbackFolderPath = cliFallbackFolderPath;
            _pathAdder = pathAdder ?? throw new ArgumentNullException(nameof(pathAdder));
        }

        public void Configure()
        {
            if (ShouldAddPackageExecutablePath())
            {
                AddPackageExecutablePath();
            }

            if (ShouldPrintFirstTimeUseNotice())
            {
                PrintFirstTimeMessageWelcome();
                if (ShouldPrintTelemetryMessageWhenFirstTimeUseNoticeIsEnabled())
                {
                    PrintTelemetryMessage();
                }

                PrintFirstTimeMessageMoreInformation();
                _firstTimeUseNoticeSentinel.CreateIfNotExists();
            }

            if (ShouldGenerateAspNetCertificate())
            {
                GenerateAspNetCertificate();
            }
        }

        private void GenerateAspNetCertificate()
        {
            _aspNetCoreCertificateGenerator.GenerateAspNetCoreDevelopmentCertificate();

            _aspNetCertificateSentinel.CreateIfNotExists();
        }

        private bool ShouldGenerateAspNetCertificate()
        {
#if EXCLUDE_ASPNETCORE
            return false;
#else
            return _dotnetFirstRunConfiguration.GenerateAspNetCertificate &&
                !_aspNetCertificateSentinel.Exists();
#endif
        }

        private bool ShouldAddPackageExecutablePath()
        {
            return _dotnetFirstRunConfiguration.AddGlobalToolsToPath &&
                !_toolPathSentinel.Exists();
        }

        private void AddPackageExecutablePath()
        {
            _pathAdder.AddPackageExecutablePathToUserPath();

            _toolPathSentinel.Create();
        }

        private bool ShouldPrintFirstTimeUseNotice()
        {
            return !_firstTimeUseNoticeSentinel.Exists();
        }

        private bool ShouldPrintTelemetryMessageWhenFirstTimeUseNoticeIsEnabled()
        {
            return !_dotnetFirstRunConfiguration.TelemetryOptout;
        }

        private void PrintFirstTimeMessageWelcome()
        {
            _reporter.WriteLine();
            string productVersion = Product.Version;
            _reporter.WriteLine(string.Format(
                LocalizableStrings.FirstTimeMessageWelcome,
                DeriveDotnetVersionFromProductVersion(productVersion),
                productVersion));
        }
        private void PrintFirstTimeMessageMoreInformation()
        {
            _reporter.WriteLine();
            _reporter.WriteLine(LocalizableStrings.FirstTimeMessageMoreInformation);
        }

        private void PrintTelemetryMessage()
        {
            _reporter.WriteLine();
            _reporter.WriteLine(LocalizableStrings.TelemetryMessage);
        }

        internal static string DeriveDotnetVersionFromProductVersion(string productVersion)
        {
            if (!NuGetVersion.TryParse(productVersion, out var parsedVersion))
            {
                return string.Empty;
            }

            return $"{parsedVersion.Major}.{parsedVersion.Minor}";
        }
    }
}
