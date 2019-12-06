using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
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

            Telemetry.Telemetry.CurrentSessionId = null; // reset static session id modified by telemetry constructor
            telemetry = new Telemetry.Telemetry(new MockFirstTimeUseNoticeSentinel(sentinelExists));

            MSBuildForwardingApp msBuildForwardingApp = new MSBuildForwardingApp(Enumerable.Empty<string>());

            object forwardingAppWithoutLogging = msBuildForwardingApp
                .GetType()
                .GetField("_forwardingAppWithoutLogging", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(msBuildForwardingApp);

            FieldInfo forwardingAppFieldInfo = forwardingAppWithoutLogging
                .GetType()
                .GetField("_forwardingApp", BindingFlags.Instance | BindingFlags.NonPublic);

            object forwardingApp = forwardingAppFieldInfo?.GetValue(forwardingAppWithoutLogging);

            FieldInfo allArgsFieldinfo = forwardingApp?
                .GetType()
                .GetField("_allArgs", BindingFlags.Instance | BindingFlags.NonPublic);

            return allArgsFieldinfo?.GetValue(forwardingApp) as string[];
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
