// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Coordinator;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Constants = Microsoft.Build.Framework.Coordinator.Constants;

namespace Microsoft.Build.Coordinator.UnitTests;

public class CoordinatorSettings_Tests(ITestOutputHelper output)
{
    [Fact]
    public void CoordinatorSettings_CustomValues_AreUsed()
    {
        CoordinatorSettings settings = CoordinatorSettings.Default with
        {
            PipeName = "custom-pipe",
            HeartbeatIntervalMs = 123,
            MissedHeartbeatsThreshold = 4,
            TotalNodeBudget = 7,
            HighPriorityReservedNodes = 2,
            MaxNodesPerBuild = 3,
            PriorityAgingThreshold = 5,
            ShutdownTimeoutMs = 456,
            ConnectionTimeoutMs = 654,
            ProcessId = 43210,
        };

        settings.PipeName.ShouldContain("custom-pipe");
        settings.HeartbeatIntervalMs.ShouldBe(123);
        settings.MissedHeartbeatsThreshold.ShouldBe(4);
        settings.TotalNodeBudget.ShouldBe(7);
        settings.HighPriorityReservedNodes.ShouldBe(2);
        settings.MaxNodesPerBuild.ShouldBe(3);
        settings.PriorityAgingThreshold.ShouldBe(5);
        settings.UsesDefaultHighPriorityReservedNodes.ShouldBeFalse();
        settings.UsesDefaultMaxNodesPerBuild.ShouldBeFalse();
        settings.MaxNodesPerBuildWhenIdle.ShouldBe(0);
        settings.ShutdownTimeoutMs.ShouldBe(456);
        settings.ConnectionTimeoutMs.ShouldBe(654);
        settings.ProcessId.ShouldBe(43210);
    }

    [Fact]
    public void CoordinatorSettings_CustomReservationAndMaxNodes_AreClampedToBudget()
    {
        CoordinatorSettings settings = CoordinatorSettings.Default with
        {
            TotalNodeBudget = 8,
            HighPriorityReservedNodes = 100,
            MaxNodesPerBuild = 100,
        };

        settings.HighPriorityReservedNodes.ShouldBe(7);
        settings.MaxNodesPerBuild.ShouldBe(8);
        settings.UsesDefaultHighPriorityReservedNodes.ShouldBeFalse();
        settings.UsesDefaultMaxNodesPerBuild.ShouldBeFalse();
        settings.MaxNodesPerBuildWhenIdle.ShouldBe(0);
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_DefaultPipeNameContainsBase()
    {
        string pipeName = CoordinatorSettings.FromEnvironment().PipeName;
        pipeName.ShouldContain(CoordinatorSettings.PipeNameBase);
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_DefaultPipeNameContainsUserName()
    {
        string pipeName = CoordinatorSettings.FromEnvironment().PipeName;
        pipeName.ShouldContain(Environment.UserName);
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_UsesEnvironmentOverrides()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.PipeNameEnvVarName, "coordinator-env-test-pipe");
        env.SetEnvironmentVariable(Constants.HeartbeatIntervalEnvVarName, "1234");
        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "7");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, "2");
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, "3");
        env.SetEnvironmentVariable(Constants.PriorityAgingThresholdEnvVarName, "5");
        env.SetEnvironmentVariable(Constants.ShutdownTimeoutEnvVarName, "9876");

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.PipeName.ShouldContain("coordinator-env-test-pipe");
        settings.HeartbeatIntervalMs.ShouldBe(1234);
        settings.MissedHeartbeatsThreshold.ShouldBe(CoordinatorSettings.DefaultMissedHeartbeatsThreshold);
        settings.TotalNodeBudget.ShouldBe(7);
        settings.HighPriorityReservedNodes.ShouldBe(2);
        settings.MaxNodesPerBuild.ShouldBe(3);
        settings.PriorityAgingThreshold.ShouldBe(5);
        settings.ShutdownTimeoutMs.ShouldBe(9876);
        settings.ConnectionTimeoutMs.ShouldBe(CoordinatorSettings.DefaultConnectionTimeoutMs);
        settings.ProcessId.ShouldBe(EnvironmentUtilities.CurrentProcessId);
    }

    [Fact]
    public void CoordinatorSettings_MutexNames_DifferByPurpose()
    {
        CoordinatorSettings settings = CoordinatorSettings.Default;

        settings.ServerMutexName.ShouldNotBe(settings.LaunchMutexName);
    }

    [Fact]
    public void CoordinatorSettings_MutexNames_DifferByPipeName()
    {
        // The mutex guards a specific pipe, so two differently-named coordinators must not contend.
        CoordinatorSettings first = CoordinatorSettings.Default with { PipeName = "coordinator-pipe-one" };
        CoordinatorSettings second = CoordinatorSettings.Default with { PipeName = "coordinator-pipe-two" };

        first.ServerMutexName.ShouldNotBe(second.ServerMutexName);
    }

    [WindowsOnlyFact]
    public void CoordinatorSettings_DefaultMutexNames_AreUserScopedViaPipeName()
    {
        // Per-user scoping comes from the default pipe name, which the mutex name is derived from.
        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.ServerMutexName.ShouldContain(Environment.UserName);
        settings.LaunchMutexName.ShouldContain(Environment.UserName);
    }

    [WindowsOnlyFact]
    public void CoordinatorSettings_MutexNames_EmbedTheGivenPipeName()
    {
        // The mutex guards a specific pipe, so an explicit pipe name is honored as given. Windows
        // only: the Unix name hashes the pipe name, so it cannot be inspected by substring. The
        // cross-platform guarantee that the name is derived from the pipe name is covered by
        // CoordinatorSettings_MutexNames_DifferByPipeName.
        CoordinatorSettings settings = CoordinatorSettings.Default with { PipeName = "coordinator-explicit-pipe" };

        settings.ServerMutexName.ShouldContain("coordinator-explicit-pipe");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-an-int")]
    public void CoordinatorSettings_FromEnvironment_InvalidPriorityAgingThresholdUsesDefault(string? envValue)
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.PriorityAgingThresholdEnvVarName, envValue);

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.PriorityAgingThreshold.ShouldBe(CoordinatorSettings.DefaultPriorityAgingThreshold);
    }

    [Theory]
    [InlineData(7, 0, 0, 0)]
    [InlineData(8, 4, 4, 8)]
    [InlineData(10, 4, 4, 8)]
    [InlineData(12, 4, 4, 8)]
    [InlineData(15, 4, 4, 8)]
    [InlineData(16, 4, 4, 8)]
    public void CoordinatorSettings_FromEnvironment_ComputesDefaultReservationAndMaxNodes(
        int totalBudget,
        int expectedReservedNodes,
        int expectedMaxNodesPerBuild,
        int expectedMaxNodesPerBuildWhenIdle)
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, totalBudget.ToString());
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, null);
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, null);

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.TotalNodeBudget.ShouldBe(totalBudget);
        settings.HighPriorityReservedNodes.ShouldBe(expectedReservedNodes);
        settings.MaxNodesPerBuild.ShouldBe(expectedMaxNodesPerBuild);
        settings.MaxNodesPerBuildWhenIdle.ShouldBe(expectedMaxNodesPerBuildWhenIdle);
        settings.UsesDefaultHighPriorityReservedNodes.ShouldBeTrue();
        settings.UsesDefaultMaxNodesPerBuild.ShouldBeTrue();
        (settings.DefaultNodeSettingsOptOutMessage is not null).ShouldBe(expectedReservedNodes > 0 || expectedMaxNodesPerBuild > 0);
        if (settings.DefaultNodeSettingsOptOutMessage is { } defaultNodeSettingsOptOutMessage)
        {
            defaultNodeSettingsOptOutMessage.ShouldContain(Constants.HighPriorityReservedNodesEnvVarName);
            defaultNodeSettingsOptOutMessage.ShouldContain(Constants.MaxNodesPerBuildEnvVarName);
        }
    }

    [Theory]
    [InlineData("-1", 1, 0, 0, 0, false)]
    [InlineData("-1", 7, 0, 0, 0, false)]
    [InlineData("-1", 8, 4, 4, 8, true)]
    [InlineData("-42", 16, 4, 4, 8, true)]
    public void CoordinatorSettings_FromEnvironment_NegativeReservationAndMaxNodesUseDefaults(
        string envValue,
        int totalBudget,
        int expectedReservedNodes,
        int expectedMaxNodesPerBuild,
        int expectedMaxNodesPerBuildWhenIdle,
        bool expectsDefaultNodeSettingsOptOutMessage)
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, totalBudget.ToString());
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, envValue);
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, envValue);

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(expectedReservedNodes);
        settings.MaxNodesPerBuild.ShouldBe(expectedMaxNodesPerBuild);
        settings.MaxNodesPerBuildWhenIdle.ShouldBe(expectedMaxNodesPerBuildWhenIdle);
        settings.UsesDefaultHighPriorityReservedNodes.ShouldBeTrue();
        settings.UsesDefaultMaxNodesPerBuild.ShouldBeTrue();
        (settings.DefaultNodeSettingsOptOutMessage is not null).ShouldBe(expectsDefaultNodeSettingsOptOutMessage);
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_InvalidReservationAndMaxNodesUseDefaults()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "16");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, "not-an-int");
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, "not-an-int");

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(4);
        settings.MaxNodesPerBuild.ShouldBe(4);
        settings.MaxNodesPerBuildWhenIdle.ShouldBe(8);
        settings.UsesDefaultHighPriorityReservedNodes.ShouldBeTrue();
        settings.UsesDefaultMaxNodesPerBuild.ShouldBeTrue();
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_ClampsExplicitReservationAndMaxNodesToBudget()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "8");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, "100");
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, "100");

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(7);
        settings.MaxNodesPerBuild.ShouldBe(8);
        settings.MaxNodesPerBuildWhenIdle.ShouldBe(0);
        settings.UsesDefaultHighPriorityReservedNodes.ShouldBeFalse();
        settings.UsesDefaultMaxNodesPerBuild.ShouldBeFalse();
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_ExplicitReservationPreservesDefaultMaxNodesPerBuild()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "16");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, "2");
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, null);

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(2);
        settings.MaxNodesPerBuild.ShouldBe(4);
        settings.MaxNodesPerBuildWhenIdle.ShouldBe(8);
        settings.UsesDefaultHighPriorityReservedNodes.ShouldBeFalse();
        settings.UsesDefaultMaxNodesPerBuild.ShouldBeTrue();
        settings.DefaultNodeSettingsOptOutMessage.ShouldNotBeNull();
        settings.DefaultNodeSettingsOptOutMessage.ShouldNotContain(Constants.HighPriorityReservedNodesEnvVarName);
        settings.DefaultNodeSettingsOptOutMessage.ShouldContain(Constants.MaxNodesPerBuildEnvVarName);
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_ExplicitMaxNodesPerBuildPreservesDefaultReservation()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "16");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, null);
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, "2");

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(4);
        settings.MaxNodesPerBuild.ShouldBe(2);
        settings.MaxNodesPerBuildWhenIdle.ShouldBe(0);
        settings.UsesDefaultHighPriorityReservedNodes.ShouldBeTrue();
        settings.UsesDefaultMaxNodesPerBuild.ShouldBeFalse();
        settings.DefaultNodeSettingsOptOutMessage.ShouldNotBeNull();
        settings.DefaultNodeSettingsOptOutMessage.ShouldContain(Constants.HighPriorityReservedNodesEnvVarName);
        settings.DefaultNodeSettingsOptOutMessage.ShouldNotContain(Constants.MaxNodesPerBuildEnvVarName);
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_NegativeReservationPreservesDefaultMaxNodesPerBuild()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "16");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, "-1");
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, null);

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(4);
        settings.MaxNodesPerBuild.ShouldBe(4);
        settings.MaxNodesPerBuildWhenIdle.ShouldBe(8);
        settings.UsesDefaultHighPriorityReservedNodes.ShouldBeTrue();
        settings.UsesDefaultMaxNodesPerBuild.ShouldBeTrue();
        settings.DefaultNodeSettingsOptOutMessage.ShouldNotBeNull();
        settings.DefaultNodeSettingsOptOutMessage.ShouldContain(Constants.HighPriorityReservedNodesEnvVarName);
        settings.DefaultNodeSettingsOptOutMessage.ShouldContain(Constants.MaxNodesPerBuildEnvVarName);
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_NegativeMaxNodesPerBuildPreservesDefaultReservation()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "16");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, null);
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, "-1");

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(4);
        settings.MaxNodesPerBuild.ShouldBe(4);
        settings.MaxNodesPerBuildWhenIdle.ShouldBe(8);
        settings.UsesDefaultHighPriorityReservedNodes.ShouldBeTrue();
        settings.UsesDefaultMaxNodesPerBuild.ShouldBeTrue();
        settings.DefaultNodeSettingsOptOutMessage.ShouldNotBeNull();
        settings.DefaultNodeSettingsOptOutMessage.ShouldContain(Constants.HighPriorityReservedNodesEnvVarName);
        settings.DefaultNodeSettingsOptOutMessage.ShouldContain(Constants.MaxNodesPerBuildEnvVarName);
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_ZeroDisablesReservationAndMaxNodesPerBuild()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "16");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, "0");
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, "0");

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(0);
        settings.MaxNodesPerBuild.ShouldBe(0);
        settings.MaxNodesPerBuildWhenIdle.ShouldBe(0);
        settings.UsesDefaultHighPriorityReservedNodes.ShouldBeFalse();
        settings.UsesDefaultMaxNodesPerBuild.ShouldBeFalse();
        settings.DefaultNodeSettingsOptOutMessage.ShouldBeNull();
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_ZeroReservationPreservesDefaultMaxNodesPerBuild()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "16");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, "0");
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, null);

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(0);
        settings.MaxNodesPerBuild.ShouldBe(4);
        settings.MaxNodesPerBuildWhenIdle.ShouldBe(8);
        settings.UsesDefaultHighPriorityReservedNodes.ShouldBeFalse();
        settings.UsesDefaultMaxNodesPerBuild.ShouldBeTrue();
        settings.DefaultNodeSettingsOptOutMessage.ShouldNotBeNull();
        settings.DefaultNodeSettingsOptOutMessage.ShouldNotContain(Constants.HighPriorityReservedNodesEnvVarName);
        settings.DefaultNodeSettingsOptOutMessage.ShouldContain(Constants.MaxNodesPerBuildEnvVarName);
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_ZeroMaxNodesPerBuildPreservesDefaultReservation()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "16");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, null);
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, "0");

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(4);
        settings.MaxNodesPerBuild.ShouldBe(0);
        settings.MaxNodesPerBuildWhenIdle.ShouldBe(0);
        settings.UsesDefaultHighPriorityReservedNodes.ShouldBeTrue();
        settings.UsesDefaultMaxNodesPerBuild.ShouldBeFalse();
        settings.DefaultNodeSettingsOptOutMessage.ShouldNotBeNull();
        settings.DefaultNodeSettingsOptOutMessage.ShouldContain(Constants.HighPriorityReservedNodesEnvVarName);
        settings.DefaultNodeSettingsOptOutMessage.ShouldNotContain(Constants.MaxNodesPerBuildEnvVarName);
    }
}
