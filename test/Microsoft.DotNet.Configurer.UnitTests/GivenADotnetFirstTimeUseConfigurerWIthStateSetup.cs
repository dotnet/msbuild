// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.Test;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.DependencyModel.Tests;
using Moq;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.DotNet.Configurer.UnitTests.GivenADotnetFirstTimeUseConfigurerWithStateSetup.ActionCalledTime;

namespace Microsoft.DotNet.Configurer.UnitTests
{
    public class GivenADotnetFirstTimeUseConfigurerWithStateSetup
    {
        private const string CliFallbackFolderPath = "some path";

        private MockBasicSentinel _firstTimeUseNoticeSentinelMock;
        private MockBasicSentinel _aspNetCertificateSentinelMock;
        private Mock<IAspNetCoreCertificateGenerator> _aspNetCoreCertificateGeneratorMock;
        private MockBasicSentinel _toolPathSentinelMock;
        private BufferedReporter _reporterMock;
        private Mock<IEnvironmentPath> _pathAdderMock;
        private Mock<IEnvironmentProvider> _environmentProvider;

        private readonly ITestOutputHelper _output;

        public GivenADotnetFirstTimeUseConfigurerWithStateSetup(ITestOutputHelper output)
        {
            ResetObjectState();

            this._output = output;
        }

        private void ResetObjectState()
        {
            _firstTimeUseNoticeSentinelMock = new MockBasicSentinel();
            _aspNetCertificateSentinelMock = new MockBasicSentinel();
            _aspNetCoreCertificateGeneratorMock = new Mock<IAspNetCoreCertificateGenerator>(MockBehavior.Strict);
            _toolPathSentinelMock = new MockBasicSentinel();
            _reporterMock = new BufferedReporter();
            _pathAdderMock = new Mock<IEnvironmentPath>(MockBehavior.Strict);
            _environmentProvider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
        }

        [Theory]
        [InlineData(false, false, false, false, false, Never, Never, Never, Never, false, false)]
        [InlineData(true, false, false, false, false, FirstRun, Never, Never, FirstRun, false, false)]
        [InlineData(false, true, false, false, false, Never, FirstRun, Never, Never, true, true)]
        [InlineData(true, true, false, false, false, FirstRun, FirstRun, Never, FirstRun, true, true)]
        [InlineData(false, false, true, false, false, Never, Never, Never, Never, false, false)]
        [InlineData(true, false, true, false, false, Never, Never, Never, Never, false, false)]
        [InlineData(false, true, true, false, false, Never, Never, FirstRun, Never, true, true)]
        [InlineData(true, true, true, false, false, Never, Never, FirstRun, Never, true, true)]
        [InlineData(false, false, false, true, false, Never, Never, Never, Never, false, false)]
        [InlineData(true, false, false, true, false, FirstRun, Never, Never, FirstRun, false, false)]
        [InlineData(false, true, false, true, false, Never, FirstRun, Never, Never, false, false)]
        [InlineData(true, true, false, true, false, FirstRun, FirstRun, Never, FirstRun, false, false)]
        [InlineData(false, false, true, true, false, Never, Never, Never, Never, false, false)]
        [InlineData(true, false, true, true, false, Never, Never, Never, Never, false, false)]
        [InlineData(false, true, true, true, false, Never, Never, FirstRun, Never, false, false)]
        [InlineData(true, true, true, true, false, Never, Never, FirstRun, Never, false, false)]
        [InlineData(false, false, false, false, true, Never, Never, Never, Never, true, false)]
        [InlineData(true, false, false, false, true, SecondRun, Never, Never, SecondRun, true, false)]
        [InlineData(false, true, false, false, true, Never, SecondRun, Never, Never, true, true)]
        [InlineData(true, true, false, false, true, SecondRun, SecondRun, Never, SecondRun, true, true)]
        [InlineData(false, false, true, false, true, Never, Never, Never, Never, true, false)]
        [InlineData(true, false, true, false, true, Never, Never, Never, Never, true, false)]
        [InlineData(false, true, true, false, true, Never, Never, SecondRun, Never, true, true)]
        [InlineData(true, true, true, false, true, Never, Never, SecondRun, Never, true, true)]
        [InlineData(false, false, false, true, true, Never, Never, Never, Never, false, false)]
        [InlineData(true, false, false, true, true, SecondRun, Never, Never, SecondRun, false, false)]
        [InlineData(false, true, false, true, true, Never, SecondRun, Never, Never, false, false)]
        [InlineData(true, true, false, true, true, SecondRun, SecondRun, Never, SecondRun, false, false)]
        [InlineData(false, false, true, true, true, Never, Never, Never, Never, false, false)]
        [InlineData(true, false, true, true, true, Never, Never, Never, Never, false, false)]
        [InlineData(false, true, true, true, true, Never, Never, SecondRun, Never, false, false)]
        [InlineData(true, true, true, true, true, Never, Never, SecondRun, Never, false, false)]
        public void FlagsCombinationAndAction(
            // Inputs
            bool DOTNET_GENERATE_ASPNET_CERTIFICATE,
            bool DOTNET_PRINT_TELEMETRY_MESSAGE,
            bool DOTNET_SKIP_FIRST_TIME_EXPERIENCE,
            bool DOTNET_CLI_TELEMETRY_OPTOUT,
            //   true to simulate install via installer. The first run is during installer,
            //   silent but has sudo permission
            //   false to simulate install via zip/tar.gz
            bool isFirstRunInstallerRun,
            // Outputs
            ActionCalledTime aspnetCertInstalledTimeShouldBeCalledAt,
            ActionCalledTime printFirstTimeWelcomeMessageShouldBeCalledAt,
            ActionCalledTime printShortFirstTimeWelcomeMessageShouldBeCalledAt,
            ActionCalledTime printAspNetCertificateInstalledMessageShouldBeCalledAt,
            bool telemetryFirstRunShouldBeEnabled,
            bool telemetrySecondRunShouldBeEnabled
            )
        {
            ResetObjectState();

            _environmentProvider
            .Setup(p => p.GetEnvironmentVariableAsBool("DOTNET_GENERATE_ASPNET_CERTIFICATE", It.IsAny<bool>()))
            .Returns(DOTNET_GENERATE_ASPNET_CERTIFICATE);
            _environmentProvider
                .Setup(p => p.GetEnvironmentVariableAsBool("DOTNET_PRINT_TELEMETRY_MESSAGE", It.IsAny<bool>()))
                .Returns(DOTNET_PRINT_TELEMETRY_MESSAGE);
            _environmentProvider
                .Setup(p => p.GetEnvironmentVariableAsBool("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", It.IsAny<bool>()))
                .Returns(DOTNET_SKIP_FIRST_TIME_EXPERIENCE);
            _environmentProvider
                .Setup(p => p.GetEnvironmentVariableAsBool("DOTNET_CLI_TELEMETRY_OPTOUT", It.IsAny<bool>()))
                .Returns(DOTNET_CLI_TELEMETRY_OPTOUT);
            _pathAdderMock.Setup(p => p.AddPackageExecutablePathToUserPath()).Verifiable();
            bool generateAspNetCoreDevelopmentCertificateCalled = false;
            _aspNetCoreCertificateGeneratorMock
                .Setup(_ => _.GenerateAspNetCoreDevelopmentCertificate())
                .Callback(() => generateAspNetCoreDevelopmentCertificateCalled = true).Verifiable();

            var aspnetCertInstalledTime = Never;
            var printFirstTimeWelcomeMessage = Never;
            var printShortFirstTimeWelcomeMessage = Never;
            var printAspNetCertificateInstalledMessage = Never;

            // First run
            var telemetryFirstRun = RunConfigUsingMocks(isFirstRunInstallerRun);

            if (generateAspNetCoreDevelopmentCertificateCalled)
            {
                aspnetCertInstalledTime = FirstRun;
            }

            if (_reporterMock.Lines.Contains(LocalizableStrings.FirstTimeWelcomeMessage))
            {
                printFirstTimeWelcomeMessage = FirstRun;
            }

            if (_reporterMock.Lines.Contains(LocalizableStrings.ShortFirstTimeWelcomeMessage))
            {
                printShortFirstTimeWelcomeMessage = FirstRun;
            }

            if (_reporterMock.Lines.Contains(LocalizableStrings.AspNetCertificateInstalled))
            {
                printAspNetCertificateInstalledMessage = FirstRun;
            }

            // Second run
            var telemetrySecondRun = RunConfigUsingMocks(false);

            aspnetCertInstalledTime = GetCalledTime(generateAspNetCoreDevelopmentCertificateCalled, aspnetCertInstalledTime);
            printFirstTimeWelcomeMessage = GetCalledTime(_reporterMock.Lines.Contains(LocalizableStrings.FirstTimeWelcomeMessage), printFirstTimeWelcomeMessage);
            printShortFirstTimeWelcomeMessage = GetCalledTime(_reporterMock.Lines.Contains(LocalizableStrings.ShortFirstTimeWelcomeMessage), printShortFirstTimeWelcomeMessage);
            printAspNetCertificateInstalledMessage = GetCalledTime(_reporterMock.Lines.Contains(LocalizableStrings.AspNetCertificateInstalled), printAspNetCertificateInstalledMessage);

            // Assertion
            aspnetCertInstalledTime.Should().Be(aspnetCertInstalledTimeShouldBeCalledAt);
            printFirstTimeWelcomeMessage.Should().Be(printFirstTimeWelcomeMessageShouldBeCalledAt);
            printShortFirstTimeWelcomeMessage.Should().Be(printShortFirstTimeWelcomeMessageShouldBeCalledAt);
            printAspNetCertificateInstalledMessage.Should().Be(printAspNetCertificateInstalledMessageShouldBeCalledAt);
            telemetryFirstRun.Enabled.Should().Be(telemetryFirstRunShouldBeEnabled);
            telemetrySecondRun.Enabled.Should().Be(telemetrySecondRunShouldBeEnabled);

        }

        private static ActionCalledTime GetCalledTime(bool predicate, ActionCalledTime actionCalledTime)
        {
            if (actionCalledTime != ActionCalledTime.FirstRun && predicate)
            {
                actionCalledTime = ActionCalledTime.SecondRun;
            }

            return actionCalledTime;
        }

        public enum ActionCalledTime
        {
            Never,
            FirstRun,
            SecondRun
        }

        private static bool GetOffSet(int i, int offset)
        {
            return ((i >> offset) % 2) == 1;
        }

        private Telemetry RunConfigUsingMocks(bool isInstallerRun)
        {
            // Assume the following objects set up are in sync with production behavior.
            // subject to future refractoring to de-dup with production code.

            var _environmentProviderObject = _environmentProvider.Object;
            bool generateAspNetCertificate =
                 _environmentProviderObject.GetEnvironmentVariableAsBool("DOTNET_GENERATE_ASPNET_CERTIFICATE", true);
            bool printTelemetryMessage =
                _environmentProviderObject.GetEnvironmentVariableAsBool("DOTNET_PRINT_TELEMETRY_MESSAGE", true);
            bool skipFirstRunExperience =
                _environmentProviderObject.GetEnvironmentVariableAsBool("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", false);

            IAspNetCertificateSentinel aspNetCertificateSentinel;
            IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel;
            IFileSentinel toolPathSentinel;

            if (isInstallerRun)
            {
                aspNetCertificateSentinel = new NoOpAspNetCertificateSentinel();
                firstTimeUseNoticeSentinel = new NoOpFirstTimeUseNoticeSentinel();
                toolPathSentinel = new NoOpFileSentinel(exists: false);

                // When running through a native installer, we want the cache expansion to happen, so
                // we need to override this.
                skipFirstRunExperience = false;
            }
            else
            {
                aspNetCertificateSentinel = _aspNetCertificateSentinelMock;
                firstTimeUseNoticeSentinel = _firstTimeUseNoticeSentinelMock;
                toolPathSentinel = _toolPathSentinelMock;
            }

            var configurer = new DotnetFirstTimeUseConfigurer(
                 firstTimeUseNoticeSentinel: firstTimeUseNoticeSentinel,
                 aspNetCertificateSentinel: aspNetCertificateSentinel,
                 aspNetCoreCertificateGenerator: _aspNetCoreCertificateGeneratorMock.Object,
                 toolPathSentinel: toolPathSentinel,
                 dotnetFirstRunConfiguration: new DotnetFirstRunConfiguration
                 (
                     generateAspNetCertificate: generateAspNetCertificate,
                     printTelemetryMessage: printTelemetryMessage,
                     skipFirstRunExperience: skipFirstRunExperience
                 ),
                 reporter: _reporterMock,
                 cliFallbackFolderPath: CliFallbackFolderPath,
                 pathAdder: _pathAdderMock.Object);

            configurer.Configure();

            return new Telemetry(firstTimeUseNoticeSentinel, "test", environmentProvider: _environmentProviderObject);
        }

        private class MockBasicSentinel : IFileSentinel, IFirstTimeUseNoticeSentinel, IAspNetCertificateSentinel
        {
            public bool ExistsBackingField { get; set; } = false;
            public void Create()
            {
                ExistsBackingField = true;
            }

            public void CreateIfNotExists()
            {
                if (!Exists())
                {
                    Create();
                }
            }

            public void Dispose()
            {
            }

            public bool Exists()
            {
                return ExistsBackingField;
            }
        }
    }
}
