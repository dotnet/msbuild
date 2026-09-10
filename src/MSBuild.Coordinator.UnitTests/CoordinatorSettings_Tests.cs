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
            ShutdownTimeoutMs = 456,
            ConnectionTimeoutMs = 654,
            ProcessId = 43210,
        };

        settings.PipeName.ShouldContain("custom-pipe");
        settings.HeartbeatIntervalMs.ShouldBe(123);
        settings.MissedHeartbeatsThreshold.ShouldBe(4);
        settings.TotalNodeBudget.ShouldBe(7);
        settings.ShutdownTimeoutMs.ShouldBe(456);
        settings.ConnectionTimeoutMs.ShouldBe(654);
        settings.ProcessId.ShouldBe(43210);
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
        env.SetEnvironmentVariable(Constants.ShutdownTimeoutEnvVarName, "9876");

        CoordinatorSettings settings = CoordinatorSettings.FromEnvironment();

        settings.PipeName.ShouldContain("coordinator-env-test-pipe");
        settings.HeartbeatIntervalMs.ShouldBe(1234);
        settings.MissedHeartbeatsThreshold.ShouldBe(CoordinatorSettings.DefaultMissedHeartbeatsThreshold);
        settings.TotalNodeBudget.ShouldBe(7);
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
}
