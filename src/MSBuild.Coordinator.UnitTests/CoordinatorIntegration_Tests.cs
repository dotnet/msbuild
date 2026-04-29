// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Coordinator;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Coordinator.UnitTests;

public class CoordinatorIntegration_Tests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly TestEnvironment _env;

    public CoordinatorIntegration_Tests(ITestOutputHelper output)
    {
        _output = output;
        _env = TestEnvironment.Create(output);
    }

    public void Dispose() => _env.Dispose();

    [Fact]
    public void ParallelBuilds_BothSucceedWithCoordinator()
    {
        string pipeName = $"msbuild-coordinator-test-{Guid.NewGuid():N}";

        // Enable the coordinator with an isolated pipe name and a small budget.
        _env.SetEnvironmentVariable(Traits.UseCoordinatorEnvVarName, "1");
        _env.SetEnvironmentVariable(Traits.CoordinatorPipeNameEnvVarName, pipeName);
        _env.SetEnvironmentVariable(Traits.CoordinatorNodeBudgetEnvVarName, "4");
        _env.SetEnvironmentVariable(Traits.CoordinatorShutdownTimeoutEnvVarName, "5000");

        // Enable comm tracing so we can verify coordinator participation.
        _env.SetEnvironmentVariable("MSBUILDDEBUGCOMM", "1");

        string projectContents = """
            <Project>
              <Target Name="Build">
                <Message Text="Hello from $(MSBuildProjectFile)" Importance="High" />
              </Target>
            </Project>
            """;

        TransientTestFile project1 = _env.CreateFile("project1.proj", projectContents);
        TransientTestFile project2 = _env.CreateFile("project2.proj", projectContents);

        // Launch two builds in parallel, each requesting 8 nodes.
        // The coordinator has a budget of 4, so the total granted across both
        // builds should be constrained. The first build to connect gets up to 4;
        // the second waits until nodes are available or gets the remainder.
        Task<(string output, bool success)> build1Task = Task.Run(() =>
        {
            string output = RunnerUtilities.ExecBootstrapedMSBuild(
                $"\"{project1.Path}\" /m:8 /v:n",
                out bool success,
                outputHelper: _output,
                timeoutMilliseconds: 60_000);
            return (output, success);
        });

        Task<(string output, bool success)> build2Task = Task.Run(() =>
        {
            string output = RunnerUtilities.ExecBootstrapedMSBuild(
                $"\"{project2.Path}\" /m:8 /v:n",
                out bool success,
                outputHelper: _output,
                timeoutMilliseconds: 60_000);
            return (output, success);
        });

        Task.WaitAll(build1Task, build2Task);

        var (output1, success1) = build1Task.Result;
        var (output2, success2) = build2Task.Result;

        _output.WriteLine("=== Build 1 Output ===");
        _output.WriteLine(output1);
        _output.WriteLine("=== Build 2 Output ===");
        _output.WriteLine(output2);

        // Both builds should succeed. The coordinator should not break anything.
        success1.ShouldBeTrue("Build 1 failed");
        success2.ShouldBeTrue("Build 2 failed");

        // Verify both builds actually completed their target.
        output1.ShouldContain("Hello from project1.proj");
        output2.ShouldContain("Hello from project2.proj");
    }

    [Fact]
    public void SingleBuild_CoordinatorCapsMaxNodeCount()
    {
        string pipeName = $"msbuild-coordinator-test-{Guid.NewGuid():N}";

        // Budget of 2, but the build requests 16.
        _env.SetEnvironmentVariable(Traits.UseCoordinatorEnvVarName, "1");
        _env.SetEnvironmentVariable(Traits.CoordinatorPipeNameEnvVarName, pipeName);
        _env.SetEnvironmentVariable(Traits.CoordinatorNodeBudgetEnvVarName, "2");
        _env.SetEnvironmentVariable(Traits.CoordinatorShutdownTimeoutEnvVarName, "5000");
        _env.SetEnvironmentVariable("MSBUILDDEBUGCOMM", "1");

        // This project logs the effective MaxNodeCount via the reserved property.
        string projectContents = """
            <Project>
              <Target Name="Build">
                <Message Text="MaxNodeCount=$(MSBuildNodeCount)" Importance="High" />
              </Target>
            </Project>
            """;

        TransientTestFile project = _env.CreateFile("captest.proj", projectContents);

        string output = RunnerUtilities.ExecBootstrapedMSBuild(
            $"\"{project.Path}\" /m:16 /v:n",
            out bool success,
            outputHelper: _output,
            timeoutMilliseconds: 60_000);

        _output.WriteLine(output);

        success.ShouldBeTrue("Build failed");

        // The coordinator should have capped the node count to 2.
        output.ShouldContain("MaxNodeCount=2");
    }
}
