// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private Dictionary<string, double> _performanceMeasurements;

        public DotnetFirstTimeUseConfigurer(
            IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel,
            IAspNetCertificateSentinel aspNetCertificateSentinel,
            IAspNetCoreCertificateGenerator aspNetCoreCertificateGenerator,
            IFileSentinel toolPathSentinel,
            DotnetFirstRunConfiguration dotnetFirstRunConfiguration,
            IReporter reporter,
            string cliFallbackFolderPath,
            IEnvironmentPath pathAdder,
            Dictionary<string, double> performanceMeasurements = null)
        {
            _firstTimeUseNoticeSentinel = firstTimeUseNoticeSentinel;
            _aspNetCertificateSentinel = aspNetCertificateSentinel;
            _aspNetCoreCertificateGenerator = aspNetCoreCertificateGenerator;
            _toolPathSentinel = toolPathSentinel;
            _dotnetFirstRunConfiguration = dotnetFirstRunConfiguration;
            _reporter = reporter;
            _cliFallbackFolderPath = cliFallbackFolderPath;
            _pathAdder = pathAdder ?? throw new ArgumentNullException(nameof(pathAdder));
            _performanceMeasurements ??= performanceMeasurements;
        }

        public void Configure()
        {
            if (ShouldAddPackageExecutablePath())
            {
                Stopwatch beforeAddPackageExecutablePath = Stopwatch.StartNew();
                AddPackageExecutablePath();
                _performanceMeasurements?.Add("AddPackageExecutablePath Time", beforeAddPackageExecutablePath.Elapsed.TotalMilliseconds);
            }

            var isFirstTimeUse = ShouldPrintFirstTimeUseNotice();
            var canShowFirstUseMessages = isFirstTimeUse && !_dotnetFirstRunConfiguration.NoLogo;
            if (isFirstTimeUse)
            {
                Stopwatch beforeFirstTimeUseNotice = Stopwatch.StartNew();
                // Migrate the nuget state from earlier SDKs
                NuGet.Common.Migrations.MigrationRunner.Run();

                if (canShowFirstUseMessages)
                {
                    PrintFirstTimeMessageWelcome();
                    if (ShouldPrintTelemetryMessageWhenFirstTimeUseNoticeIsEnabled())
                    {
                        PrintTelemetryMessage();
                    }
                }

                _firstTimeUseNoticeSentinel.CreateIfNotExists();
                _performanceMeasurements?.Add("FirstTimeUseNotice Time", beforeFirstTimeUseNotice.Elapsed.TotalMilliseconds);
            }

            if (ShouldGenerateAspNetCertificate())
            {
                Stopwatch beforeGenerateAspNetCertificate = Stopwatch.StartNew();
                GenerateAspNetCertificate();

                if (canShowFirstUseMessages)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        // The instructions in this message only apply to Windows and MacOS.
                        PrintFirstTimeMessageAspNetCertificate();
                    }
                    else
                    {
                        // The instructions in this message only apply to Linux (various distros).
                        // OSPlatform.FreeBSD would also see this message, which is acceptable since we have no specific FreeBSD instructions.
                        PrintFirstTimeMessageAspNetCertificateLinux();
                    }
                }

                _performanceMeasurements?.Add("GenerateAspNetCertificate Time", beforeGenerateAspNetCertificate.Elapsed.TotalMilliseconds);
            }

            if (canShowFirstUseMessages)
            {
                PrintFirstTimeMessageMoreInformation();
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

        private void PrintFirstTimeMessageAspNetCertificate()
        {
            _reporter.WriteLine();
            _reporter.WriteLine(LocalizableStrings.FirstTimeMessageAspNetCertificate);
        }

        private void PrintFirstTimeMessageAspNetCertificateLinux()
        {
            _reporter.WriteLine();
            _reporter.WriteLine(LocalizableStrings.FirstTimeMessageAspNetCertificateLinux);
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
