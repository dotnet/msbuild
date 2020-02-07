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

        private Mock<IFirstTimeUseNoticeSentinel> _firstTimeUseNoticeSentinelMock;
        private Mock<IAspNetCertificateSentinel> _aspNetCertificateSentinelMock;
        private Mock<IAspNetCoreCertificateGenerator> _aspNetCoreCertificateGeneratorMock;
        private Mock<IFileSentinel> _toolPathSentinelMock;
        private Mock<IReporter> _reporterMock;
        private Mock<IEnvironmentPath> _pathAdderMock;

        public GivenADotnetFirstTimeUseConfigurer()
        {
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
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                (
                    generateAspNetCertificate: true,
                    telemetryOptout: false,
                    addGlobalToolsToPath: true
                ),
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str.Contains(LocalizableStrings.FirstTimeMessageMoreInformation))), Times.Never);
            _reporterMock.Verify(r => r.Write(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void It_prints_the_telemetry_if_the_sentinel_does_not_exist()
        {
            _firstTimeUseNoticeSentinelMock.Setup(n => n.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                (
                    generateAspNetCertificate: true,
                    telemetryOptout: false,
                    addGlobalToolsToPath: true
                ),
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str.Contains(LocalizableStrings.FirstTimeMessageMoreInformation))));
            _reporterMock.Verify(r => r.Write(It.IsAny<string>()), Times.Never);
        }
    
        [Fact]
        public void It_adds_the_tool_path_to_the_environment_if_the_tool_path_sentinel_does_not_exist()
        {
            _toolPathSentinelMock.Setup(s => s.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                (
                    generateAspNetCertificate: true,
                    telemetryOptout: false,
                    addGlobalToolsToPath: true
                ),
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
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                (
                    generateAspNetCertificate: true,
                    telemetryOptout: false,
                    addGlobalToolsToPath: true
                ),
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _toolPathSentinelMock.Verify(s => s.Create(), Times.Never);
            _pathAdderMock.Verify(p => p.AddPackageExecutablePathToUserPath(), Times.Never);
        }

        [Fact]
        public void It_does_not_generate_the_aspnet_https_development_certificate_if_the_sentinel_exists()
        {
            _aspNetCertificateSentinelMock.Setup(n => n.Exists()).Returns(true);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                (
                    generateAspNetCertificate: true,
                    telemetryOptout: false,
                    addGlobalToolsToPath: true
                ),
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _aspNetCoreCertificateGeneratorMock.Verify(s => s.GenerateAspNetCoreDevelopmentCertificate(), Times.Never);
        }

        [Fact]
        public void It_does_not_generate_the_aspnet_https_development_certificate_when_the_user_has_set_the_DOTNET_GENERATE_ASPNET_CERTIFICATE_environment_variable()
        {
            _aspNetCertificateSentinelMock.Setup(n => n.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                (
                    generateAspNetCertificate: false,
                    telemetryOptout: false,
                    addGlobalToolsToPath: true
                ),
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _aspNetCoreCertificateGeneratorMock.Verify(s => s.GenerateAspNetCoreDevelopmentCertificate(), Times.Never);
        }

        [Fact]
        public void It_generates_the_aspnet_https_development_certificate_if_the_sentinel_does_not_exist()
        {
            _aspNetCertificateSentinelMock.Setup(n => n.Exists()).Returns(false);

            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                (
                    generateAspNetCertificate: true,
                    telemetryOptout: false,
                    addGlobalToolsToPath: true
                ),
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _reporterMock.Verify(r => r.WriteLine(It.Is<string>(str => str.Contains(LocalizableStrings.FirstTimeMessageMoreInformation))));
            _aspNetCoreCertificateGeneratorMock.Verify(s => s.GenerateAspNetCoreDevelopmentCertificate(), Times.Once);
        }

        [Fact]
        public void It_adds_the_tool_path_to_the_environment_if_addGlobalToolsToPath_is_enabled()
        {
            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                (
                    generateAspNetCertificate: true,
                    telemetryOptout: false,
                    addGlobalToolsToPath: true
                ),
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _pathAdderMock.Verify(p => p.AddPackageExecutablePathToUserPath(), Times.Once);
        }

        [Fact]
        public void It_does_not_add_the_tool_path_to_the_environment_if_addGlobalToolsToPath_is_disabled()
        {
            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                (
                    generateAspNetCertificate: true,
                    telemetryOptout: false,
                    addGlobalToolsToPath: false
                ),
                _reporterMock.Object,
                CliFallbackFolderPath,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _pathAdderMock.Verify(p => p.AddPackageExecutablePathToUserPath(), Times.Never);
        }
    }
}
