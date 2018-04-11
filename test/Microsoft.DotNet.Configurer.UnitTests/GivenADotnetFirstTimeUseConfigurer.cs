// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.DependencyModel.Tests;
using Moq;
using Xunit;

namespace Microsoft.DotNet.Configurer.UnitTests
{
    public class GivenADotnetFirstTimeUseConfigurer
    {
        private const string CliFallbackFolderPath = "some path";

        private Mock<INuGetCachePrimer> _nugetCachePrimerMock;
        private Mock<INuGetCacheSentinel> _nugetCacheSentinelMock;
        private Mock<IFirstTimeUseNoticeSentinel> _firstTimeUseNoticeSentinelMock;
        private Mock<IAspNetCertificateSentinel> _aspNetCertificateSentinelMock;
        private Mock<IAspNetCoreCertificateGenerator> _aspNetCoreCertificateGeneratorMock;
        private Mock<IFileSentinel> _toolPathSentinelMock;
        private Mock<IReporter> _reporterMock;
        private Mock<IEnvironmentPath> _pathAdderMock;

        public GivenADotnetFirstTimeUseConfigurer()
        {
            _nugetCachePrimerMock = new Mock<INuGetCachePrimer>();
            _nugetCacheSentinelMock = new Mock<INuGetCacheSentinel>();
            _firstTimeUseNoticeSentinelMock = new Mock<IFirstTimeUseNoticeSentinel>();
            _aspNetCertificateSentinelMock = new Mock<IAspNetCertificateSentinel>();
            _aspNetCoreCertificateGeneratorMock = new Mock<IAspNetCoreCertificateGenerator>();
            _toolPathSentinelMock = new Mock<IFileSentinel>();
            _reporterMock = new Mock<IReporter>();
            _pathAdderMock = new Mock<IEnvironmentPath>();
        }

        [Fact]
        public void It_does_not_print_the_first_time_use_notice_if_the_sentinel_exists()
        {
            _firstTimeUseNoticeSentinelMock.Setup(n => n.Exists()).Returns(true);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.FirstTimeWelcomeMessage)), Times.Never);
            _reporterMock.Verify(r => r.Write(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void It_does_not_print_the_first_time_use_notice_when_the_user_has_set_the_DOTNET_SKIP_FIRST_TIME_EXPERIENCE_environemnt_variable()
        {
            _firstTimeUseNoticeSentinelMock.Setup(n => n.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = true,
                    PrintTelemetryMessage = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.FirstTimeWelcomeMessage)), Times.Never);
            _reporterMock.Verify(r => r.Write(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void It_does_not_print_the_first_time_use_notice_when_the_user_has_set_the_DOTNET_PRINT_TELEMETRY_MESSAGE_environemnt_variable()
        {
            _firstTimeUseNoticeSentinelMock.Setup(n => n.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = false
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.FirstTimeWelcomeMessage)), Times.Never);
            _reporterMock.Verify(r => r.Write(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void It_prints_the_telemetry_if_the_sentinel_does_not_exist()
        {
            _firstTimeUseNoticeSentinelMock.Setup(n => n.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.FirstTimeWelcomeMessage)));
            _reporterMock.Verify(r => r.Write(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void It_does_not_prime_the_cache_if_the_sentinel_exists()
        {
            _nugetCacheSentinelMock.Setup(n => n.Exists()).Returns(true);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _nugetCachePrimerMock.Verify(r => r.PrimeCache(), Times.Never);
        }

        [Fact]
        public void It_does_not_prime_the_cache_if_first_run_experience_is_already_happening()
        {
            _nugetCacheSentinelMock.Setup(n => n.InProgressSentinelAlreadyExists()).Returns(true);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _nugetCachePrimerMock.Verify(r => r.PrimeCache(), Times.Never);
        }

        [Fact]
        public void It_does_not_prime_the_cache_if_cache_is_missing()
        {
            _nugetCachePrimerMock.Setup(n => n.SkipPrimingTheCache()).Returns(true);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _nugetCachePrimerMock.Verify(r => r.PrimeCache(), Times.Never);
        }

        [Fact]
        public void It_does_not_prime_the_cache_if_the_sentinel_does_not_exist_but_the_user_has_set_the_DOTNET_SKIP_FIRST_TIME_EXPERIENCE_environment_variable()
        {
            _nugetCacheSentinelMock.Setup(n => n.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = true,
                    PrintTelemetryMessage = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _nugetCachePrimerMock.Verify(r => r.PrimeCache(), Times.Never);
        }

        [Fact]
        public void It_primes_the_cache_if_the_sentinel_does_not_exist()
        {
            _nugetCacheSentinelMock.Setup(n => n.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _nugetCachePrimerMock.Verify(r => r.PrimeCache(), Times.Once);
        }        

        [Fact]
        public void It_prints_first_use_notice_and_primes_the_cache_if_the_sentinels_do_not_exist()
        {
            _nugetCacheSentinelMock.Setup(n => n.Exists()).Returns(false);
            _firstTimeUseNoticeSentinelMock.Setup(n => n.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.FirstTimeWelcomeMessage)));
            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.NugetCachePrimeMessage)));
            _nugetCachePrimerMock.Verify(r => r.PrimeCache(), Times.Once);
            _reporterMock.Verify(r => r.Write(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void It_prints_the_first_time_use_notice_if_the_cache_sentinel_exists_but_the_first_notice_sentinel_does_not()
        {
            _nugetCacheSentinelMock.Setup(n => n.Exists()).Returns(true);
            _firstTimeUseNoticeSentinelMock.Setup(n => n.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r =>
                r.WriteLine(It.Is<string>(str => str == LocalizableStrings.FirstTimeWelcomeMessage)));
            _reporterMock.Verify(
                r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.NugetCachePrimeMessage)),
                Times.Never);
            _reporterMock.Verify(r => r.Write(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void It_adds_the_tool_path_to_the_environment_if_the_tool_path_sentinel_does_not_exist()
        {
            _toolPathSentinelMock.Setup(s => s.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();
            
            _toolPathSentinelMock.Verify(s => s.Create(), Times.Once);
            _pathAdderMock.Verify(p => p.AddPackageExecutablePathToUserPath(), Times.Once);
        }

        [Fact]
        public void It_does_not_add_the_tool_path_to_the_environment_if_the_tool_path_sentinel_exists()
        {
            _toolPathSentinelMock.Setup(s => s.Exists()).Returns(true);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = true,
                    GenerateAspNetCertificate = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _toolPathSentinelMock.Verify(s => s.Create(), Times.Never);
            _pathAdderMock.Verify(p => p.AddPackageExecutablePathToUserPath(), Times.Never);
        }

        [Fact]
        public void It_prints_the_unauthorized_notice_if_the_cache_sentinel_reports_Unauthorized()
        {
            _nugetCacheSentinelMock.Setup(n => n.UnauthorizedAccess).Returns(true);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r =>
                r.WriteLine(It.Is<string>(str => str == LocalizableStrings.FirstTimeWelcomeMessage)));
            _reporterMock.Verify(r =>
                r.WriteLine(It.Is<string>(str => 
                    str == string.Format(LocalizableStrings.UnauthorizedAccessMessage, CliFallbackFolderPath))));
            _reporterMock.Verify(
                r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.NugetCachePrimeMessage)),
                Times.Never);
            _reporterMock.Verify(r => r.Write(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void It_does_not_prime_the_cache_if_the_cache_sentinel_reports_Unauthorized()
        {
            _nugetCacheSentinelMock.Setup(n => n.UnauthorizedAccess).Returns(true);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _nugetCachePrimerMock.Verify(r => r.PrimeCache(), Times.Never);
        }

        [Fact]
        public void It_does_not_generate_the_aspnet_https_development_certificate_if_the_sentinel_exists()
        {
            _aspNetCertificateSentinelMock.Setup(n => n.Exists()).Returns(true);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.AspNetCertificateInstalled)), Times.Never);
            _aspNetCoreCertificateGeneratorMock.Verify(s => s.GenerateAspNetCoreDevelopmentCertificate(), Times.Never);
        }

        [Fact]
        public void It_does_not_generate_the_aspnet_https_development_certificate_when_the_user_has_set_the_DOTNET_SKIP_FIRST_TIME_EXPERIENCE_environment_variable()
        {
            _aspNetCertificateSentinelMock.Setup(n => n.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.AspNetCertificateInstalled)), Times.Never);
            _aspNetCoreCertificateGeneratorMock.Verify(s => s.GenerateAspNetCoreDevelopmentCertificate(), Times.Never);
        }

        [Fact]
        public void It_does_not_generate_the_aspnet_https_development_certificate_when_the_user_has_set_the_DOTNET_GENERATE_ASPNET_CERTIFICATE_environment_variable()
        {
            _aspNetCertificateSentinelMock.Setup(n => n.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.AspNetCertificateInstalled)), Times.Never);
            _aspNetCoreCertificateGeneratorMock.Verify(s => s.GenerateAspNetCoreDevelopmentCertificate(), Times.Never);
        }

        [Fact]
        public void It_generates_the_aspnet_https_development_certificate_if_the_sentinel_does_not_exist()
        {
            _aspNetCertificateSentinelMock.Setup(n => n.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = true,
                    GenerateAspNetCertificate = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.FirstTimeWelcomeMessage)));
            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.NugetCachePrimeMessage)));
            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.AspNetCertificateInstalled)));
            _aspNetCoreCertificateGeneratorMock.Verify(s => s.GenerateAspNetCoreDevelopmentCertificate(), Times.Once);
        }

        [Fact]
        public void It_adds_the_tool_path_to_the_environment_if_the_first_run_experience_is_not_skipped()
        {
            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = false,
                    PrintTelemetryMessage = true,
                    GenerateAspNetCertificate = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _pathAdderMock.Verify(p => p.AddPackageExecutablePathToUserPath(), Times.Once);
        }

        [Fact]
        public void It_does_not_add_the_tool_path_to_the_environment_if_the_first_run_experience_is_skipped()
        {
            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                {
                    SkipFirstRunExperience = true,
                    PrintTelemetryMessage = true,
                    GenerateAspNetCertificate = true
                },
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _pathAdderMock.Verify(p => p.AddPackageExecutablePathToUserPath(), Times.Never);
        }
    }
}
