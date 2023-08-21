// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Moq;

namespace Microsoft.DotNet.Configurer.UnitTests
{
    public class GivenADotnetFirstTimeUseConfigurer
    {
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
                    addGlobalToolsToPath: true,
                    nologo: false,
                    skipWorkloadIntegrityCheck: false
                ),
                _reporterMock.Object,
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
                    addGlobalToolsToPath: true,
                    nologo: false,
                    skipWorkloadIntegrityCheck: false
                ),
                _reporterMock.Object,
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
                    addGlobalToolsToPath: true,
                    nologo: false,
                    skipWorkloadIntegrityCheck: false
                ),
                _reporterMock.Object,
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
                    addGlobalToolsToPath: true,
                    nologo: false,
                    skipWorkloadIntegrityCheck: false
                ),
                _reporterMock.Object,
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
                    addGlobalToolsToPath: true, 
                    nologo: false,
                    skipWorkloadIntegrityCheck: false
                ),
                _reporterMock.Object,
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
                    addGlobalToolsToPath: true,
                    nologo: false,
                    skipWorkloadIntegrityCheck: false
                ),
                _reporterMock.Object,
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
                    addGlobalToolsToPath: true,
                    nologo: false,
                    skipWorkloadIntegrityCheck: false
                ),
                _reporterMock.Object,
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
                    addGlobalToolsToPath: true,
                    nologo: false,
                    skipWorkloadIntegrityCheck: false
                ),
                _reporterMock.Object,
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
                    addGlobalToolsToPath: false,
                    nologo: false,
                    skipWorkloadIntegrityCheck: false
                ),
                _reporterMock.Object,
                _pathAdderMock.Object);

            dotnetFirstTimeUseConfigurer.Configure();

            _pathAdderMock.Verify(p => p.AddPackageExecutablePathToUserPath(), Times.Never);
        }

        [Fact]
        public void It_does_add_telemetry_when_all_firsttimeuse_values_run()
        {

            _firstTimeUseNoticeSentinelMock.Setup(n => n.Exists()).Returns(false);

            Dictionary<string, double> measurements = new Dictionary<string, double>();
            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                (
                    generateAspNetCertificate: true,
                    telemetryOptout: false,
                    addGlobalToolsToPath: true,
                    nologo: false,
                    skipWorkloadIntegrityCheck: false
                ),
                _reporterMock.Object,
                _pathAdderMock.Object,
                measurements);

            DateTime beforeConfigure = DateTime.Now;
            dotnetFirstTimeUseConfigurer.Configure();
            double configureTime = (DateTime.Now - beforeConfigure).TotalMilliseconds;

            measurements.Should().HaveCount(3);
            measurements.Should().ContainKey("AddPackageExecutablePath Time");
            measurements.Should().ContainKey("FirstTimeUseNotice Time");
            measurements.Should().ContainKey("GenerateAspNetCertificate Time");
            measurements["AddPackageExecutablePath Time"].Should().BeGreaterThan(0);
            measurements["FirstTimeUseNotice Time"].Should().BeGreaterThan(0);
            measurements["GenerateAspNetCertificate Time"].Should().BeGreaterThan(0);
            measurements["AddPackageExecutablePath Time"].Should().BeLessThan(configureTime);
            measurements["FirstTimeUseNotice Time"].Should().BeLessThan(configureTime);
            measurements["GenerateAspNetCertificate Time"].Should().BeLessThan(configureTime);
        }

        [Fact]
        public void It_does_add_telemetry_when_no_firsttimeuse_values_run()
        {

            _firstTimeUseNoticeSentinelMock.Setup(n => n.Exists()).Returns(true);

            Dictionary<string, double> measurements = new Dictionary<string, double>();
            var dotnetFirstTimeUseConfigurer = new DotnetFirstTimeUseConfigurer(
                _firstTimeUseNoticeSentinelMock.Object,
                _aspNetCertificateSentinelMock.Object,
                _aspNetCoreCertificateGeneratorMock.Object,
                _toolPathSentinelMock.Object,
                new DotnetFirstRunConfiguration
                (
                    generateAspNetCertificate: false,
                    telemetryOptout: false,
                    addGlobalToolsToPath: false,
                    nologo: false,
                    skipWorkloadIntegrityCheck: false
                ),
                _reporterMock.Object,
                _pathAdderMock.Object,
                measurements);

            dotnetFirstTimeUseConfigurer.Configure();

            measurements.Should().HaveCount(0);
            measurements.Should().NotContainKey("AddPackageExecutablePath Time");
            measurements.Should().NotContainKey("FirstTimeUseNotice Time");
            measurements.Should().NotContainKey("GenerateAspNetCertificate Time");
         }
    }
}
