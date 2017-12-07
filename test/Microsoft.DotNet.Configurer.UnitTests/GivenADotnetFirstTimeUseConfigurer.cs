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
        private Mock<IEnvironmentProvider> _environmentProviderMock;
        private Mock<IReporter> _reporterMock;
        private Mock<IEnvironmentPath> _pathAdder;

        public GivenADotnetFirstTimeUseConfigurer()
        {
            _nugetCachePrimerMock = new Mock<INuGetCachePrimer>();
            _nugetCacheSentinelMock = new Mock<INuGetCacheSentinel>();
            _firstTimeUseNoticeSentinelMock = new Mock<IFirstTimeUseNoticeSentinel>();
            _aspNetCertificateSentinelMock = new Mock<IAspNetCertificateSentinel>();
            _aspNetCoreCertificateGeneratorMock = new Mock<IAspNetCoreCertificateGenerator>();
            _environmentProviderMock = new Mock<IEnvironmentProvider>();
            _reporterMock = new Mock<IReporter>();
            _pathAdder = new Mock<IEnvironmentPath>();

            _environmentProviderMock
                .Setup(e => e.GetEnvironmentVariableAsBool("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", false))
                .Returns(false);
            _environmentProviderMock
                .Setup(e => e.GetEnvironmentVariableAsBool("DOTNET_PRINT_TELEMETRY_MESSAGE", true))
                .Returns(true);
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
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.FirstTimeWelcomeMessage)), Times.Never);
            _reporterMock.Verify(r => r.Write(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void It_does_not_print_the_first_time_use_notice_when_the_user_has_set_the_DOTNET_SKIP_FIRST_TIME_EXPERIENCE_environemnt_variable()
        {
            _firstTimeUseNoticeSentinelMock.Setup(n => n.Exists()).Returns(false);
            _environmentProviderMock
                .Setup(e => e.GetEnvironmentVariableAsBool("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", false))
                .Returns(true);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.FirstTimeWelcomeMessage)), Times.Never);
            _reporterMock.Verify(r => r.Write(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void It_does_not_print_the_first_time_use_notice_when_the_user_has_set_the_DOTNET_PRINT_TELEMETRY_MESSAGE_environemnt_variable()
        {
            _firstTimeUseNoticeSentinelMock.Setup(n => n.Exists()).Returns(false);
            _environmentProviderMock
                .Setup(e => e.GetEnvironmentVariableAsBool("DOTNET_PRINT_TELEMETRY_MESSAGE", true))
                .Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

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
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

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
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

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
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

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
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _nugetCachePrimerMock.Verify(r => r.PrimeCache(), Times.Never);
        }

        [Fact]
        public void It_does_not_prime_the_cache_if_the_sentinel_exists_but_the_user_has_set_the_DOTNET_SKIP_FIRST_TIME_EXPERIENCE_environemnt_variable()
        {
            _nugetCacheSentinelMock.Setup(n => n.Exists()).Returns(false);
            _environmentProviderMock
                .Setup(e => e.GetEnvironmentVariableAsBool("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", false))
                .Returns(true);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

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
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

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
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

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
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r =>
                r.WriteLine(It.Is<string>(str => str == LocalizableStrings.FirstTimeWelcomeMessage)));
            _reporterMock.Verify(
                r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.NugetCachePrimeMessage)),
                Times.Never);
            _reporterMock.Verify(r => r.Write(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void It_adds_executable_package_path_to_environment_path_when_the_first_notice_sentinel_does_not_exist()
        {
            _nugetCacheSentinelMock.Setup(n => n.Exists()).Returns(true);
            _firstTimeUseNoticeSentinelMock.Setup(n => n.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                new FakeCreateWillExistNuGetCacheSentinel(false, true), 
                new FakeCreateWillExistFirstTimeUseNoticeSentinel(false),
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

            dotnetFirstTimeUseConfigurer.Configure();
            
            _pathAdder.Verify(p => p.AddPackageExecutablePathToUserPath(), Times.AtLeastOnce);
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
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

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
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

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
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.AspNetCertificateInstalled)), Times.Never);
            _aspNetCoreCertificateGeneratorMock.Verify(s => s.GenerateAspNetCoreDevelopmentCertificate(), Times.Never);
        }

        [Fact]
        public void It_does_not_generate_the_aspnet_https_development_certificate_when_the_user_has_set_the_DOTNET_SKIP_FIRST_TIME_EXPERIENCE_environment_variable()
        {
            _aspNetCertificateSentinelMock.Setup(n => n.Exists()).Returns(false);
            _environmentProviderMock
                .Setup(e => e.GetEnvironmentVariableAsBool("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", false))
                .Returns(true);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.AspNetCertificateInstalled)), Times.Never);
            _aspNetCoreCertificateGeneratorMock.Verify(s => s.GenerateAspNetCoreDevelopmentCertificate(), Times.Never);
        }

        [Fact]
        public void It_does_not_generate_the_aspnet_https_development_certificate_when_the_user_has_set_the_DOTNET_GENERATE_ASPNET_CERTIFICATE_environment_variable()
        {
            _aspNetCertificateSentinelMock.Setup(n => n.Exists()).Returns(false);
            _environmentProviderMock
                .Setup(e => e.GetEnvironmentVariableAsBool("DOTNET_GENERATE_ASPNET_CERTIFICATE", true))
                .Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.AspNetCertificateInstalled)), Times.Never);
            _aspNetCoreCertificateGeneratorMock.Verify(s => s.GenerateAspNetCoreDevelopmentCertificate(), Times.Never);
        }

        [Fact]
        public void It_generates_the_aspnet_https_development_certificate_if_the_sentinel_does_not_exist()
        {
            _aspNetCertificateSentinelMock.Setup(n => n.Exists()).Returns(false);
            _environmentProviderMock.Setup(e => e.GetEnvironmentVariableAsBool("DOTNET_GENERATE_ASPNET_CERTIFICATE", true))
                .Returns(true);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _nugetCachePrimerMock.Object,
                _nugetCacheSentinelMock.Object,
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _environmentProviderMock.Object,
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdder.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.FirstTimeWelcomeMessage)));
            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.NugetCachePrimeMessage)));
            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str == LocalizableStrings.AspNetCertificateInstalled)));
            _aspNetCoreCertificateGeneratorMock.Verify(s => s.GenerateAspNetCoreDevelopmentCertificate(), Times.Once);
        }

        private class FakeCreateWillExistFirstTimeUseNoticeSentinel : IFirstTimeUseNoticeSentinel
        {
            private bool _exists;

            public FakeCreateWillExistFirstTimeUseNoticeSentinel(bool exists)
            {
                _exists = exists;
            }

            public void Dispose()
            {
            }

            public bool Exists()
            {
                return _exists;
            }

            public void CreateIfNotExists()
            {
                _exists = true;
            }
        }

        private class FakeCreateWillExistNuGetCacheSentinel : INuGetCacheSentinel
        {
            private bool _inProgressSentinelAlreadyExists;
            private bool _exists;

            public FakeCreateWillExistNuGetCacheSentinel(bool inProgressSentinelAlreadyExists, bool exists)
            {
                _inProgressSentinelAlreadyExists = inProgressSentinelAlreadyExists;
                _exists = exists;
            }

            public void Dispose()
            {
            }

            public bool InProgressSentinelAlreadyExists()
            {
                return _inProgressSentinelAlreadyExists;
            }

            public bool Exists()
            {
                return _exists;
            }

            public void CreateIfNotExists()
            {
                _exists = true;
            }

            public bool UnauthorizedAccess { get; set; }
        }
    }
}
