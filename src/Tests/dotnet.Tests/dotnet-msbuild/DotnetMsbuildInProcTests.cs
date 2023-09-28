// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.MSBuild;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class DotnetMsbuildInProcTests : SdkTest
    {
        public DotnetMsbuildInProcTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void WhenTelemetryIsEnabledTheLoggerIsAddedToTheCommandLine()
        {
            Telemetry.Telemetry telemetry;
            string[] allArgs = GetArgsForMSBuild(() => true, out telemetry);
            // telemetry will still be disabled if environment variable is set
            if (telemetry.Enabled)
            {
                allArgs.Should().NotBeNull();

                allArgs.Should().Contain(
                    value => value.IndexOf("-distributedlogger", StringComparison.OrdinalIgnoreCase) >= 0,
                    "The MSBuild logger argument should be specified when telemetry is enabled.");
            }
        }

        [Fact]
        public void WhenTelemetryIsDisabledTheLoggerIsNotAddedToTheCommandLine()
        {
            string[] allArgs = GetArgsForMSBuild(() => false);

            allArgs.Should().NotBeNull();

            allArgs.Should().NotContain(
                value => value.IndexOf("-logger", StringComparison.OrdinalIgnoreCase) >= 0,
                $"The MSBuild logger argument should not be specified when telemetry is disabled.");
        }

        private string[] GetArgsForMSBuild(Func<bool> sentinelExists)
        {
            Telemetry.Telemetry telemetry;
            return GetArgsForMSBuild(sentinelExists, out telemetry);
        }

        private string[] GetArgsForMSBuild(Func<bool> sentinelExists, out Telemetry.Telemetry telemetry)
        {

            Telemetry.Telemetry.DisableForTests(); // reset static session id modified by telemetry constructor
            telemetry = new Telemetry.Telemetry(new MockFirstTimeUseNoticeSentinel(sentinelExists));

            MSBuildForwardingApp msBuildForwardingApp = new(Enumerable.Empty<string>());

            var forwardingAppWithoutLogging = msBuildForwardingApp
                .GetType()
                .GetField("_forwardingAppWithoutLogging", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(msBuildForwardingApp) as Cli.Utils.MSBuildForwardingAppWithoutLogging;

            return forwardingAppWithoutLogging?.GetAllArguments();
        }
    }
    public sealed class MockFirstTimeUseNoticeSentinel : IFirstTimeUseNoticeSentinel
    {
        private readonly Func<bool> _exists;

        public bool UnauthorizedAccess => true;

        public MockFirstTimeUseNoticeSentinel(Func<bool> exists = null)
        {
            _exists = exists ?? (() => true);
        }

        public void Dispose()
        {
        }

        public bool InProgressSentinelAlreadyExists() => false;

        public bool Exists() => _exists();

        public void CreateIfNotExists()
        {
        }
    }
}
