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
        settings.HighPriorityReservedNodesIsAuto.ShouldBeFalse();
        settings.MaxNodesPerBuildIsAuto.ShouldBeFalse();
        settings.IsAutoStrictPolicyActive.ShouldBeFalse();
        settings.ShutdownTimeoutMs.ShouldBe(456);
        settings.ConnectionTimeoutMs.ShouldBe(654);
        settings.ProcessId.ShouldBe(43210);
    }

    [Fact]
    public void CoordinatorSettings_CustomNodePolicyValues_AreClampedToBudget()
    {
        CoordinatorSettings settings = CoordinatorSettings.Default with
        {
            TotalNodeBudget = 8,
            HighPriorityReservedNodes = 100,
            MaxNodesPerBuild = 100,
        };

        settings.HighPriorityReservedNodes.ShouldBe(7);
        settings.MaxNodesPerBuild.ShouldBe(8);
        settings.HighPriorityReservedNodesIsAuto.ShouldBeFalse();
        settings.MaxNodesPerBuildIsAuto.ShouldBeFalse();
        settings.IsAutoStrictPolicyActive.ShouldBeFalse();
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
    [InlineData(4, 0, 0)]
    [InlineData(7, 0, 0)]
    [InlineData(8, 4, 4)]
    [InlineData(10, 4, 4)]
    [InlineData(16, 4, 4)]
    public void CoordinatorSettings_FromEnvironment_AutoComputesStrictPolicyDefaults(int totalBudget, int expectedReservedNodes, int expectedMaxNodesPerBuild)
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, totalBudget.ToString());
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, null);
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, null);

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.TotalNodeBudget.ShouldBe(totalBudget);
        settings.HighPriorityReservedNodes.ShouldBe(expectedReservedNodes);
        settings.MaxNodesPerBuild.ShouldBe(expectedMaxNodesPerBuild);
        settings.HighPriorityReservedNodesIsAuto.ShouldBeTrue();
        settings.MaxNodesPerBuildIsAuto.ShouldBeTrue();
        settings.IsAutoStrictPolicyActive.ShouldBe(expectedReservedNodes > 0 || expectedMaxNodesPerBuild > 0);
        (settings.AutoStrictPolicyOptOutMessage is not null).ShouldBe(settings.IsAutoStrictPolicyActive);
        if (settings.AutoStrictPolicyOptOutMessage is { } autoStrictPolicyOptOutMessage)
        {
            autoStrictPolicyOptOutMessage.ShouldContain(Constants.HighPriorityReservedNodesEnvVarName);
            autoStrictPolicyOptOutMessage.ShouldContain(Constants.MaxNodesPerBuildEnvVarName);
        }
    }

    [Theory]
    [InlineData("-1", 1, 0, 0, false)]
    [InlineData("-1", 7, 0, 0, false)]
    [InlineData("-1", 8, 4, 4, true)]
    [InlineData("-42", 16, 4, 4, true)]
    public void CoordinatorSettings_FromEnvironment_NegativeStrictPolicyValuesUseAuto(
        string envValue,
        int totalBudget,
        int expectedReservedNodes,
        int expectedMaxNodesPerBuild,
        bool expectedAutoStrictPolicyActive)
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, totalBudget.ToString());
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, envValue);
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, envValue);

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(expectedReservedNodes);
        settings.MaxNodesPerBuild.ShouldBe(expectedMaxNodesPerBuild);
        settings.HighPriorityReservedNodesIsAuto.ShouldBeTrue();
        settings.MaxNodesPerBuildIsAuto.ShouldBeTrue();
        settings.IsAutoStrictPolicyActive.ShouldBe(expectedAutoStrictPolicyActive);
        (settings.AutoStrictPolicyOptOutMessage is not null).ShouldBe(expectedAutoStrictPolicyActive);
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_InvalidStrictPolicyValuesUseAuto()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "16");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, "not-an-int");
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, "not-an-int");

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(4);
        settings.MaxNodesPerBuild.ShouldBe(4);
        settings.HighPriorityReservedNodesIsAuto.ShouldBeTrue();
        settings.MaxNodesPerBuildIsAuto.ShouldBeTrue();
        settings.IsAutoStrictPolicyActive.ShouldBeTrue();
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_ClampsExplicitStrictPolicyOverridesToBudget()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "8");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, "100");
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, "100");

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(7);
        settings.MaxNodesPerBuild.ShouldBe(8);
        settings.HighPriorityReservedNodesIsAuto.ShouldBeFalse();
        settings.MaxNodesPerBuildIsAuto.ShouldBeFalse();
        settings.IsAutoStrictPolicyActive.ShouldBeFalse();
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_ExplicitReservationOnlyPreservesAutoMaxNodesPerBuild()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "16");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, "2");
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, null);

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(2);
        settings.MaxNodesPerBuild.ShouldBe(4);
        settings.HighPriorityReservedNodesIsAuto.ShouldBeFalse();
        settings.MaxNodesPerBuildIsAuto.ShouldBeTrue();
        settings.IsAutoStrictPolicyActive.ShouldBeTrue();
        settings.AutoStrictPolicyOptOutMessage.ShouldNotBeNull();
        settings.AutoStrictPolicyOptOutMessage.ShouldNotContain(Constants.HighPriorityReservedNodesEnvVarName);
        settings.AutoStrictPolicyOptOutMessage.ShouldContain(Constants.MaxNodesPerBuildEnvVarName);
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_ExplicitMaxNodesPerBuildOnlyPreservesAutoReservation()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "16");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, null);
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, "2");

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(4);
        settings.MaxNodesPerBuild.ShouldBe(2);
        settings.HighPriorityReservedNodesIsAuto.ShouldBeTrue();
        settings.MaxNodesPerBuildIsAuto.ShouldBeFalse();
        settings.IsAutoStrictPolicyActive.ShouldBeTrue();
        settings.AutoStrictPolicyOptOutMessage.ShouldNotBeNull();
        settings.AutoStrictPolicyOptOutMessage.ShouldContain(Constants.HighPriorityReservedNodesEnvVarName);
        settings.AutoStrictPolicyOptOutMessage.ShouldNotContain(Constants.MaxNodesPerBuildEnvVarName);
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_AutoReservationOnlyPreservesAutoMaxNodesPerBuild()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "16");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, "-1");
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, null);

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(4);
        settings.MaxNodesPerBuild.ShouldBe(4);
        settings.HighPriorityReservedNodesIsAuto.ShouldBeTrue();
        settings.MaxNodesPerBuildIsAuto.ShouldBeTrue();
        settings.IsAutoStrictPolicyActive.ShouldBeTrue();
        settings.AutoStrictPolicyOptOutMessage.ShouldNotBeNull();
        settings.AutoStrictPolicyOptOutMessage.ShouldContain(Constants.HighPriorityReservedNodesEnvVarName);
        settings.AutoStrictPolicyOptOutMessage.ShouldContain(Constants.MaxNodesPerBuildEnvVarName);
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_AutoMaxNodesPerBuildOnlyPreservesAutoReservation()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "16");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, null);
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, "-1");

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(4);
        settings.MaxNodesPerBuild.ShouldBe(4);
        settings.HighPriorityReservedNodesIsAuto.ShouldBeTrue();
        settings.MaxNodesPerBuildIsAuto.ShouldBeTrue();
        settings.IsAutoStrictPolicyActive.ShouldBeTrue();
        settings.AutoStrictPolicyOptOutMessage.ShouldNotBeNull();
        settings.AutoStrictPolicyOptOutMessage.ShouldContain(Constants.HighPriorityReservedNodesEnvVarName);
        settings.AutoStrictPolicyOptOutMessage.ShouldContain(Constants.MaxNodesPerBuildEnvVarName);
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_ZeroDisablesStrictPolicy()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "16");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, "0");
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, "0");

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(0);
        settings.MaxNodesPerBuild.ShouldBe(0);
        settings.HighPriorityReservedNodesIsAuto.ShouldBeFalse();
        settings.MaxNodesPerBuildIsAuto.ShouldBeFalse();
        settings.IsAutoStrictPolicyActive.ShouldBeFalse();
        settings.AutoStrictPolicyOptOutMessage.ShouldBeNull();
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_ZeroReservationOnlyPreservesAutoMaxNodesPerBuild()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "16");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, "0");
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, null);

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(0);
        settings.MaxNodesPerBuild.ShouldBe(4);
        settings.HighPriorityReservedNodesIsAuto.ShouldBeFalse();
        settings.MaxNodesPerBuildIsAuto.ShouldBeTrue();
        settings.IsAutoStrictPolicyActive.ShouldBeTrue();
        settings.AutoStrictPolicyOptOutMessage.ShouldNotBeNull();
        settings.AutoStrictPolicyOptOutMessage.ShouldNotContain(Constants.HighPriorityReservedNodesEnvVarName);
        settings.AutoStrictPolicyOptOutMessage.ShouldContain(Constants.MaxNodesPerBuildEnvVarName);
    }

    [Fact]
    public void CoordinatorSettings_FromEnvironment_ZeroMaxNodesPerBuildOnlyPreservesAutoReservation()
    {
        using TestEnvironment env = TestEnvironment.Create(output);

        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "16");
        env.SetEnvironmentVariable(Constants.HighPriorityReservedNodesEnvVarName, null);
        env.SetEnvironmentVariable(Constants.MaxNodesPerBuildEnvVarName, "0");

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.HighPriorityReservedNodes.ShouldBe(4);
        settings.MaxNodesPerBuild.ShouldBe(0);
        settings.HighPriorityReservedNodesIsAuto.ShouldBeTrue();
        settings.MaxNodesPerBuildIsAuto.ShouldBeFalse();
        settings.IsAutoStrictPolicyActive.ShouldBeTrue();
        settings.AutoStrictPolicyOptOutMessage.ShouldNotBeNull();
        settings.AutoStrictPolicyOptOutMessage.ShouldContain(Constants.HighPriorityReservedNodesEnvVarName);
        settings.AutoStrictPolicyOptOutMessage.ShouldNotContain(Constants.MaxNodesPerBuildEnvVarName);
    }
}
