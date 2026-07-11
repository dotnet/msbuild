// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Constants = Microsoft.Build.Framework.Coordinator.Constants;

namespace Microsoft.Build.Coordinator.UnitTests;

[TestClass]
public class CoordinatorIntegration_Tests(TestContext outputHelper)
{
    [MSBuildTestMethod]
    public async Task ParallelBuilds_BothSucceedWithCoordinator()
    {
        string pipeName = $"msbuild-coordinator-test-{Guid.NewGuid():N}";

        // Enable the coordinator with an isolated pipe name and a small budget.
        using var env = TestEnvironment.Create(outputHelper);

        env.SetEnvironmentVariable(Traits.UseCoordinatorEnvVarName, "1");
        env.SetEnvironmentVariable(Constants.PipeNameEnvVarName, pipeName);
        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "4");
        env.SetEnvironmentVariable(Constants.ShutdownTimeoutEnvVarName, "5000");

        // Enable comm tracing so we can verify coordinator participation.
        env.SetEnvironmentVariable("MSBUILDDEBUGCOMM", "1");

        string projectContents = """
            <Project>
              <Target Name="Build">
                <Message Text="Hello from $(MSBuildProjectFile)" Importance="High" />
              </Target>
            </Project>
            """;

        TransientTestFile project1 = env.CreateFile("project1.proj", projectContents);
        TransientTestFile project2 = env.CreateFile("project2.proj", projectContents);

        // Launch two builds in parallel, each requesting 8 nodes.
        // The coordinator has a budget of 4, so the total granted across both
        // builds should be constrained. The first build to connect gets up to 4;
        // the second waits until nodes are available or gets the remainder.
        var build1Task = RunnerUtilities.ExecBootstrappedMSBuildAsync(
            $"\"{project1.Path}\" /m:8 /v:n",
            outputHelper: outputHelper,
            timeoutMilliseconds: 60_000);

        var build2Task = RunnerUtilities.ExecBootstrappedMSBuildAsync(
            $"\"{project2.Path}\" /m:8 /v:n",
            outputHelper: outputHelper,
            timeoutMilliseconds: 60_000);

        Task.WaitAll(build1Task, build2Task);

        var (success1, buildOutput1) = await build1Task;
        var (success2, buildOutput2) = await build2Task;

        outputHelper.WriteLine("=== Build 1 Output ===");
        outputHelper.WriteLine(buildOutput1);
        outputHelper.WriteLine("=== Build 2 Output ===");
        outputHelper.WriteLine(buildOutput2);

        // Both builds should succeed. The coordinator should not break anything.
        success1.ShouldBeTrue("Build 1 failed");
        success2.ShouldBeTrue("Build 2 failed");

        // Verify both builds actually completed their target.
        buildOutput1.ShouldContain("Hello from project1.proj");
        buildOutput2.ShouldContain("Hello from project2.proj");
    }

    [MSBuildTestMethod]
    public async Task SingleBuild_CoordinatorCapsMaxNodeCount()
    {
        string pipeName = $"msbuild-coordinator-test-{Guid.NewGuid():N}";

        // Budget of 2, but the build requests 16.
        using var env = TestEnvironment.Create(outputHelper);

        env.SetEnvironmentVariable(Traits.UseCoordinatorEnvVarName, "1");
        env.SetEnvironmentVariable(Constants.PipeNameEnvVarName, pipeName);
        env.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, "2");
        env.SetEnvironmentVariable(Constants.ShutdownTimeoutEnvVarName, "5000");
        env.SetEnvironmentVariable("MSBUILDDEBUGCOMM", "1");

        // This project logs the effective MaxNodeCount via the reserved property.
        string projectContents = """
            <Project>
              <Target Name="Build">
                <Message Text="MaxNodeCount=$(MSBuildNodeCount)" Importance="High" />
              </Target>
            </Project>
            """;

        TransientTestFile project = env.CreateFile("captest.proj", projectContents);

        var (success, buildOutput) = await RunnerUtilities.ExecBootstrappedMSBuildAsync(
            $"\"{project.Path}\" /m:16 /v:n",
            outputHelper: outputHelper,
            timeoutMilliseconds: 60_000);

        outputHelper.WriteLine(buildOutput);

        success.ShouldBeTrue("Build failed");

        // The coordinator should have capped the node count to 2.
        buildOutput.ShouldContain("MaxNodeCount=2");
    }
}
