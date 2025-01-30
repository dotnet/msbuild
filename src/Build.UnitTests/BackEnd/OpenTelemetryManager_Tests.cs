// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using Xunit;
using Shouldly;
using Xunit.Abstractions;
using Microsoft.Build.UnitTests.Shared;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.Framework.Telemetry.Tests
{
    /// <summary>
    /// Ensures tests run serially so environment variables and the singleton do not interfere with parallel test runs.
    /// </summary>
    [Collection("OpenTelemetryManagerTests")]
    public class OpenTelemetryManagerTests : IDisposable
    {
        private readonly ITestOutputHelper _output;

        // TestEnvironment automatically restores environment variables on Dispose
        private readonly TestEnvironment _env;

        private const string TelemetryFxOptoutEnvVarName = "MSBUILD_TELEMETRY_OPTOUT";
        private const string DotnetOptOut = "DOTNET_CLI_TELEMETRY_OPTOUT";
        private const string TelemetrySampleRateOverrideEnvVarName = "MSBUILD_TELEMETRY_SAMPLE_RATE";

        public OpenTelemetryManagerTests(ITestOutputHelper output)
        {
            _output = output;
            _env = TestEnvironment.Create(_output);

            // Reset the manager state at the start of each test.
            ResetManagerState();
        }

        public void Dispose()
        {
            // Dispose TestEnvironment to restore any environment variables, etc.
            _env.Dispose();

            // Reset again in case the test created new references or manipulated the singleton after environment cleanup.
            ResetManagerState();
        }

        [Theory]
        [InlineData(DotnetOptOut, "true")]
        [InlineData(TelemetryFxOptoutEnvVarName, "true")]
        [InlineData(DotnetOptOut, "1")]
        [InlineData(TelemetryFxOptoutEnvVarName, "1")]
        public void Initialize_ShouldSetStateToOptOut_WhenOptOutEnvVarIsTrue(string optoutVar, string value)
        {
            // Arrange
            _env.SetEnvironmentVariable(optoutVar, value);

            // Act
            OpenTelemetryManager.Instance.Initialize(isStandalone: false);

            // Assert
            OpenTelemetryManager.Instance.IsActive().ShouldBeFalse();
        }

#if NET
        [Fact]
        public void Initialize_ShouldSetStateToUnsampled_WhenNoOverrideOnNetCore()
        {
            _env.SetEnvironmentVariable(TelemetrySampleRateOverrideEnvVarName, null);

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
            _env.SetEnvironmentVariable(TelemetrySampleRateOverrideEnvVarName, "1.0");

            // Act
            OpenTelemetryManager.Instance.Initialize(isStandalone: standalone);

            // Assert
            OpenTelemetryManager.Instance.IsActive().ShouldBeTrue();
            OpenTelemetryManager.Instance.DefaultActivitySource.ShouldNotBeNull();
        }

        [Fact]
        public void Initialize_ShouldNoOp_WhenCalledMultipleTimes()
        {
            _env.SetEnvironmentVariable(DotnetOptOut, "true");
            OpenTelemetryManager.Instance.Initialize(isStandalone: true);
            var state1 = OpenTelemetryManager.Instance.IsActive();

            _env.SetEnvironmentVariable(DotnetOptOut, null);
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
