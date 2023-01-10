// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Utilities;
using Moq;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.DotNet.Configurer.UnitTests.GivenADotnetFirstTimeUseConfigurerWithStateSetup.ActionCalledTime;

namespace Microsoft.DotNet.Configurer.UnitTests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
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

            _output = output;
        }

        private void ResetObjectState()
        {
            Telemetry.EnableForTests();
            _firstTimeUseNoticeSentinelMock = new MockBasicSentinel();
            _aspNetCertificateSentinelMock = new MockBasicSentinel();
            _aspNetCoreCertificateGeneratorMock = new Mock<IAspNetCoreCertificateGenerator>(MockBehavior.Strict);
            _toolPathSentinelMock = new MockBasicSentinel();
            _reporterMock = new BufferedReporter();
            _pathAdderMock = new Mock<IEnvironmentPath>(MockBehavior.Strict);
            _environmentProvider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
        }

        [Theory]
        [InlineData(false, false, false, false, Never, FirstRun, FirstRun, true, true)]
        [InlineData(true, false, false, false, FirstRun, FirstRun, FirstRun, true, true)]
        [InlineData(false, true, false, false, Never, FirstRun, Never, false, false)]
        [InlineData(true, true, false, false, FirstRun, FirstRun, Never, false, false)]
        [InlineData(false, false, true, false, Never, Never, Never, true, true)]
        [InlineData(true, false, true, false, FirstRun, Never, Never, true, true)]
        [InlineData(false, true, true, false, Never, Never, Never, false, false)]
        [InlineData(true, true, true, false, FirstRun, Never, Never, false, false)]
        [InlineData(false, false, false, true, Never, SecondRun, SecondRun, true, true)]
        [InlineData(true, false, false, true, SecondRun, SecondRun, SecondRun, true, true)]
        [InlineData(false, true, false, true, Never, SecondRun, Never, false, false)]
        [InlineData(true, true, false, true, SecondRun, SecondRun, Never, false, false)]
        [InlineData(false, false, true, true, Never, Never, Never, true, true)]
        [InlineData(true, false, true, true, SecondRun, Never, Never, true, true)]
        [InlineData(false, true, true, true, Never, Never, Never, false, false)]
        [InlineData(true, true, true, true, SecondRun, Never, Never, false, false)]
        public void FlagsCombinationAndAction(
            // Inputs
            bool DOTNET_GENERATE_ASPNET_CERTIFICATE,
            bool DOTNET_CLI_TELEMETRY_OPTOUT,
            bool DOTNET_NOLOGO,
            //   true to simulate install via installer. The first run is during installer,
            //   silent but has sudo permission
            //   false to simulate install via zip/tar.gz
            bool isFirstRunInstallerRun,
            // Outputs
            ActionCalledTime aspnetCertInstalledTimeShouldBeCalledAt,
            ActionCalledTime printFirstTimeWelcomeMessageShouldBeCalledAt,
            ActionCalledTime printTelemetryMessageShouldBeCalledAt,
            bool telemetryFirstRunShouldBeEnabled,
            bool telemetrySecondRunShouldBeEnabled
            )
        {
            ResetObjectState();

            _environmentProvider
                .Setup(p => p.GetEnvironmentVariableAsBool("DOTNET_GENERATE_ASPNET_CERTIFICATE", It.IsAny<bool>()))
                .Returns(DOTNET_GENERATE_ASPNET_CERTIFICATE);
            _environmentProvider
                .Setup(p => p.GetEnvironmentVariableAsBool("DOTNET_CLI_TELEMETRY_OPTOUT", It.IsAny<bool>()))
                .Returns(DOTNET_CLI_TELEMETRY_OPTOUT);
            _environmentProvider
                .Setup(p => p.GetEnvironmentVariableAsBool("DOTNET_NOLOGO", It.IsAny<bool>()))
                .Returns(DOTNET_NOLOGO);
            _environmentProvider
                .Setup(p => p.GetEnvironmentVariableAsBool("DOTNET_ADD_GLOBAL_TOOLS_TO_PATH", It.IsAny<bool>()))
                .Returns(true);
            _pathAdderMock.Setup(p => p.AddPackageExecutablePathToUserPath()).Verifiable();
            // box a bool so it will be captured by reference in closure
            object generateAspNetCoreDevelopmentCertificateCalled = false;
            _aspNetCoreCertificateGeneratorMock
                .Setup(_ => _.GenerateAspNetCoreDevelopmentCertificate())
                .Callback(() => generateAspNetCoreDevelopmentCertificateCalled = true).Verifiable();

            var aspnetCertInstalledTime
                = new FirstRunExperienceAction(
                    () => (bool)generateAspNetCoreDevelopmentCertificateCalled,
                    "aspnetCertInstalledTime");
            var printFirstTimeWelcomeMessage
                = new FirstRunExperienceAction(
                    () => _reporterMock.Lines.Contains(string.Format(
                    Configurer.LocalizableStrings.FirstTimeMessageWelcome,
                    DotnetFirstTimeUseConfigurer.DeriveDotnetVersionFromProductVersion(Product.Version),
                    Product.Version))
                            && _reporterMock.Lines.Contains(LocalizableStrings.FirstTimeMessageMoreInformation),
                    "printFirstTimeWelcomeMessage");
            var printTelemetryMessage
                = new FirstRunExperienceAction(
                    () => _reporterMock.Lines.Contains(LocalizableStrings.TelemetryMessage),
                    "printTelemetryMessage");

            List<FirstRunExperienceAction> firstRunExperienceActions
                = new List<FirstRunExperienceAction>() {
                    aspnetCertInstalledTime,
                    printFirstTimeWelcomeMessage,
                    printTelemetryMessage };

            // First run
            var telemetryFirstRun = RunConfigUsingMocks(isFirstRunInstallerRun);

            firstRunExperienceActions.ForEach(a => a.EvaluateAfterFirstRun());

            // Second run
            var telemetrySecondRun = RunConfigUsingMocks(false);

            firstRunExperienceActions.ForEach(a => a.EvaluateAfterSecondRun());

            // Assertion
            aspnetCertInstalledTime.Assert(aspnetCertInstalledTimeShouldBeCalledAt);
            printFirstTimeWelcomeMessage.Assert(printFirstTimeWelcomeMessageShouldBeCalledAt);
            printTelemetryMessage.Assert(printTelemetryMessageShouldBeCalledAt);
            telemetryFirstRun.Enabled.Should().Be(telemetryFirstRunShouldBeEnabled);
            telemetrySecondRun.Enabled.Should().Be(telemetrySecondRunShouldBeEnabled);
        }

        private class FirstRunExperienceAction
        {
            public ActionCalledTime ActionCalledTime { get; private set; }
            public string Name { get; }

            private readonly Func<bool> _tellTheActionIsRun;

            public FirstRunExperienceAction(Func<bool> tellTheActionIsRun, string name)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("message", nameof(name));
                }

                _tellTheActionIsRun
                    = tellTheActionIsRun ?? throw new ArgumentNullException(nameof(tellTheActionIsRun));
                Name = name;
                ActionCalledTime = Never;
            }

            public void EvaluateAfterFirstRun()
            {
                if (_tellTheActionIsRun())
                {
                    ActionCalledTime = FirstRun;
                }
            }

            public void EvaluateAfterSecondRun()
            {
                if (ActionCalledTime != ActionCalledTime.FirstRun && _tellTheActionIsRun())
                {
                    this.ActionCalledTime = ActionCalledTime.SecondRun;
                }
            }

            public void Assert(ActionCalledTime expectedActionCalledTime)
            {
                ActionCalledTime
                    .Should()
                    .Be(expectedActionCalledTime,
                        $"{Name} should be called at {expectedActionCalledTime.ToString("g")} " +
                        $"but find {ActionCalledTime.ToString("g")}");
            }
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

        private Telemetry RunConfigUsingMocks(bool isInstallerRun)
        {
            // Assume the following objects set up are in sync with production behavior.
            // subject to future refactoring to de-dup with production code.

            var _environmentProviderObject = _environmentProvider.Object;
            bool generateAspNetCertificate =
                 _environmentProviderObject.GetEnvironmentVariableAsBool("DOTNET_GENERATE_ASPNET_CERTIFICATE", true);
            bool telemetryOptout =
                _environmentProviderObject.GetEnvironmentVariableAsBool("DOTNET_CLI_TELEMETRY_OPTOUT", false);
            bool addGlobalToolsToPath =
                _environmentProviderObject.GetEnvironmentVariableAsBool("DOTNET_ADD_GLOBAL_TOOLS_TO_PATH", defaultValue: true);
            bool nologo =
                _environmentProviderObject.GetEnvironmentVariableAsBool("DOTNET_NOLOGO", defaultValue: false);

            IAspNetCertificateSentinel aspNetCertificateSentinel;
            IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel;
            IFileSentinel toolPathSentinel;

            if (isInstallerRun)
            {
                aspNetCertificateSentinel = new NoOpAspNetCertificateSentinel();
                firstTimeUseNoticeSentinel = new NoOpFirstTimeUseNoticeSentinel();
                toolPathSentinel = new NoOpFileSentinel(exists: false);
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
                     telemetryOptout: telemetryOptout,
                     addGlobalToolsToPath: addGlobalToolsToPath,
                     nologo: nologo
                 ),
                 reporter: _reporterMock,
                 cliFallbackFolderPath: CliFallbackFolderPath,
                 pathAdder: _pathAdderMock.Object);

            configurer.Configure();

            return new Telemetry(firstTimeUseNoticeSentinel,
                "test",
                environmentProvider: _environmentProviderObject,
                senderCount: 0);
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
