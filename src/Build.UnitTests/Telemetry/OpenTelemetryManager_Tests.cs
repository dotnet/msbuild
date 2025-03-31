// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using Xunit;
using Shouldly;
using Xunit.Abstractions;
using Microsoft.Build.UnitTests.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Framework.Telemetry;

namespace Microsoft.Build.Engine.UnitTests.Telemetry
{
    // Putting the tests to a collection ensures tests run serially by default, that's needed to isolate the manager singleton state and env vars in some telemetry tests.
    [Collection("OpenTelemetryManagerTests")]
    public class OpenTelemetryManagerTests : IDisposable
    {

        private const string TelemetryFxOptoutEnvVarName = "MSBUILD_TELEMETRY_OPTOUT";
        private const string DotnetOptOut = "DOTNET_CLI_TELEMETRY_OPTOUT";
        private const string TelemetrySampleRateOverrideEnvVarName = "MSBUILD_TELEMETRY_SAMPLE_RATE";
        private const string VS1714TelemetryOptInEnvVarName = "MSBUILD_TELEMETRY_OPTIN";

        public OpenTelemetryManagerTests()
        {
            ResetManagerState();
        }

        public void Dispose()
        {
        }

        [Theory]
        [InlineData(DotnetOptOut, "true")]
        [InlineData(TelemetryFxOptoutEnvVarName, "true")]
        [InlineData(DotnetOptOut, "1")]
        [InlineData(TelemetryFxOptoutEnvVarName, "1")]
        public void Initialize_ShouldSetStateToOptOut_WhenOptOutEnvVarIsTrue(string optoutVar, string value)
        {
            // Arrange
            using TestEnvironment environment = TestEnvironment.Create();
            environment.SetEnvironmentVariable(optoutVar, value);

            // Act
            OpenTelemetryManager.Instance.Initialize(isStandalone: false);

            // Assert
            OpenTelemetryManager.Instance.IsActive().ShouldBeFalse();
        }

#if NETCOREAPP
        [Fact]
        public void Initialize_ShouldSetStateToUnsampled_WhenNoOverrideOnNetCore()
        {
            using TestEnvironment environment = TestEnvironment.Create();
            environment.SetEnvironmentVariable(TelemetrySampleRateOverrideEnvVarName, null);
            environment.SetEnvironmentVariable(DotnetOptOut, null);

            OpenTelemetryManager.Instance.Initialize(isStandalone: false);

            // If no override on .NET, we expect no Active ActivitySource
            OpenTelemetryManager.Instance.DefaultActivitySource.ShouldBeNull();
        }
#endif

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Initialize_ShouldSetSampleRateOverride_AndCreateActivitySource_WhenRandomBelowOverride(bool standalone)
        {
            // Arrange
            using TestEnvironment environment = TestEnvironment.Create();
            environment.SetEnvironmentVariable(VS1714TelemetryOptInEnvVarName, "1");
            environment.SetEnvironmentVariable(TelemetrySampleRateOverrideEnvVarName, "1.0");
            environment.SetEnvironmentVariable(DotnetOptOut, null);

            // Act
            OpenTelemetryManager.Instance.Initialize(isStandalone: standalone);

            // Assert
            OpenTelemetryManager.Instance.IsActive().ShouldBeTrue();
            OpenTelemetryManager.Instance.DefaultActivitySource.ShouldNotBeNull();
        }

        [Fact]
        public void Initialize_ShouldNoOp_WhenCalledMultipleTimes()
        {
            using TestEnvironment environment = TestEnvironment.Create();
            environment.SetEnvironmentVariable(DotnetOptOut, "true");
            OpenTelemetryManager.Instance.Initialize(isStandalone: true);
            var state1 = OpenTelemetryManager.Instance.IsActive();

            environment.SetEnvironmentVariable(DotnetOptOut, null);
            OpenTelemetryManager.Instance.Initialize(isStandalone: true);
            var state2 = OpenTelemetryManager.Instance.IsActive();

            // Because the manager is already initialized, second call is a no-op
            state1.ShouldBe(false);
            state2.ShouldBe(false);
        }

        /* Helper methods */

        /// <summary>
        /// Resets the singleton manager to a known uninitialized state so each test is isolated.
        /// </summary>
        private void ResetManagerState()
        {
            var instance = OpenTelemetryManager.Instance;

            // 1. Reset the private _telemetryState field
            var telemetryStateField = typeof(OpenTelemetryManager)
                .GetField("_telemetryState", BindingFlags.NonPublic | BindingFlags.Instance);
            telemetryStateField?.SetValue(instance, OpenTelemetryManager.TelemetryState.Uninitialized);

            // 2. Null out the DefaultActivitySource property
            var defaultSourceProp = typeof(OpenTelemetryManager)
                .GetProperty(nameof(OpenTelemetryManager.DefaultActivitySource),
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            defaultSourceProp?.SetValue(instance, null);
        }
    }
}
