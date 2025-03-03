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

        private const string TelemetryFxOptoutEnvVarName = "MSBUILD_TELEMETRY_OPTOUT";
        private const string DotnetOptOut = "DOTNET_CLI_TELEMETRY_OPTOUT";
        private const string TelemetrySampleRateOverrideEnvVarName = "MSBUILD_TELEMETRY_SAMPLE_RATE";
        private const string VS1714TelemetryOptInEnvVarName = "MSBUILD_TELEMETRY_OPTIN";

        private string? preTestFxOptout;
        private string? preTestDotnetOptout;
        private string? preTestSampleRate;
        private string? preTestVS1714TelemetryOptIn;

        public OpenTelemetryManagerTests()
        {
            // control environment state before each test
            SaveEnvVars();
            ResetManagerState();
            ResetEnvVars();
        }

        private void SaveEnvVars()
        {
            preTestFxOptout = Environment.GetEnvironmentVariable(TelemetryFxOptoutEnvVarName);
            preTestDotnetOptout = Environment.GetEnvironmentVariable(DotnetOptOut);
            preTestSampleRate = Environment.GetEnvironmentVariable(TelemetrySampleRateOverrideEnvVarName);
            preTestVS1714TelemetryOptIn = Environment.GetEnvironmentVariable(VS1714TelemetryOptInEnvVarName);
        }

        private void RestoreEnvVars()
        {
            Environment.SetEnvironmentVariable(TelemetryFxOptoutEnvVarName, preTestFxOptout);
            Environment.SetEnvironmentVariable(DotnetOptOut, preTestDotnetOptout);
            Environment.SetEnvironmentVariable(TelemetrySampleRateOverrideEnvVarName, preTestSampleRate);
            Environment.SetEnvironmentVariable(VS1714TelemetryOptInEnvVarName, preTestVS1714TelemetryOptIn);
        }

        private void ResetEnvVars()
        {
            Environment.SetEnvironmentVariable(DotnetOptOut, null);
            Environment.SetEnvironmentVariable(TelemetryFxOptoutEnvVarName, null);
            Environment.SetEnvironmentVariable(TelemetrySampleRateOverrideEnvVarName, null);
            Environment.SetEnvironmentVariable(VS1714TelemetryOptInEnvVarName, null);
        }

        public void Dispose()
        {
            RestoreEnvVars();
        }

        [Theory]
        [InlineData(DotnetOptOut, "true")]
        [InlineData(TelemetryFxOptoutEnvVarName, "true")]
        [InlineData(DotnetOptOut, "1")]
        [InlineData(TelemetryFxOptoutEnvVarName, "1")]
        public void Initialize_ShouldSetStateToOptOut_WhenOptOutEnvVarIsTrue(string optoutVar, string value)
        {
            // Arrange
            Environment.SetEnvironmentVariable(optoutVar, value);

            // Act
            OpenTelemetryManager.Instance.Initialize(isStandalone: false);

            // Assert
            OpenTelemetryManager.Instance.IsActive().ShouldBeFalse();
        }

#if NET
        [Fact]
        public void Initialize_ShouldSetStateToUnsampled_WhenNoOverrideOnNetCore()
        {
            Environment.SetEnvironmentVariable(TelemetrySampleRateOverrideEnvVarName, null);

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
            Environment.SetEnvironmentVariable(VS1714TelemetryOptInEnvVarName, "1");
            Environment.SetEnvironmentVariable(TelemetrySampleRateOverrideEnvVarName, "1.0");

            // Act
            OpenTelemetryManager.Instance.Initialize(isStandalone: standalone);

            // Assert
            OpenTelemetryManager.Instance.IsActive().ShouldBeTrue();
            OpenTelemetryManager.Instance.DefaultActivitySource.ShouldNotBeNull();
        }

        [Fact]
        public void Initialize_ShouldNoOp_WhenCalledMultipleTimes()
        {
            Environment.SetEnvironmentVariable(DotnetOptOut, "true");
            OpenTelemetryManager.Instance.Initialize(isStandalone: true);
            var state1 = OpenTelemetryManager.Instance.IsActive();

            Environment.SetEnvironmentVariable(DotnetOptOut, null);
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
