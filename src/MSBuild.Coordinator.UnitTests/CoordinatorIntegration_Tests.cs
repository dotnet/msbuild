// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Constants = Microsoft.Build.Framework.Coordinator.Constants;

namespace Microsoft.Build.Coordinator.UnitTests;

public class CoordinatorIntegration_Tests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task ParallelBuilds_BothSucceedWithCoordinator()
    {
        // Enable the coordinator with an isolated pipe name and a small budget.
        using var helper = new CoordinatorTestHelper(outputHelper, nodeBudget: 4);

        string projectContents = """
            <Project>
              <Target Name="Build">
                <Message Text="Hello from $(MSBuildProjectFile)" Importance="High" />
              </Target>
            </Project>
            """;

        TransientTestFile project1 = helper.CreateFile("project1.proj", projectContents);
        TransientTestFile project2 = helper.CreateFile("project2.proj", projectContents);

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

    [Fact]
    public async Task SingleBuild_CoordinatorCapsMaxNodeCount()
    {
        // Budget of 2, but the build requests 16.
        using var helper = new CoordinatorTestHelper(outputHelper, nodeBudget: 2);

        // This project logs the effective MaxNodeCount via the reserved property.
        string projectContents = """
            <Project>
              <Target Name="Build">
                <Message Text="MaxNodeCount=$(MSBuildNodeCount)" Importance="High" />
              </Target>
            </Project>
            """;

        TransientTestFile project = helper.CreateFile("captest.proj", projectContents);

        var (success, buildOutput) = await RunnerUtilities.ExecBootstrappedMSBuildAsync(
            $"\"{project.Path}\" /m:16 /v:n",
            outputHelper: outputHelper,
            timeoutMilliseconds: 60_000);

        outputHelper.WriteLine(buildOutput);

        success.ShouldBeTrue("Build failed");

        // The coordinator should have capped the node count to 2.
        buildOutput.ShouldContain("MaxNodeCount=2");
    }

    private sealed class CoordinatorTestHelper : IDisposable
    {
        public TestEnvironment TestEnvironment { get; }

        private readonly string _debugLogPath;

        public CoordinatorTestHelper(ITestOutputHelper outputHelper, int? nodeBudget = null)
        {
            TestEnvironment = TestEnvironment.Create(outputHelper);

            string pipeName = $"msbuild-coordinator-test-{Guid.NewGuid():N}";
            TestEnvironment.SetEnvironmentVariable(Traits.UseCoordinatorEnvVarName, "1");
            TestEnvironment.SetEnvironmentVariable(Constants.PipeNameEnvVarName, pipeName);

            if (nodeBudget is int nodeBudgetValue)
            {
                TestEnvironment.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, nodeBudgetValue.ToString());
            }

            TestEnvironment.SetEnvironmentVariable(Constants.ShutdownTimeoutEnvVarName, "100");

            _debugLogPath = Path.Combine(Path.GetTempPath(), $"msbuild-coordinator-test-debug-{Guid.NewGuid():N}");
            TestEnvironment.SetEnvironmentVariable("MSBUILDDEBUGCOMM", "1");
            TestEnvironment.SetEnvironmentVariable("MSBUILDDEBUGPATH", _debugLogPath);

            FrameworkDebugUtils.SetDebugPath();
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
            FileUtilities.DeleteDirectoryNoThrow(_debugLogPath, recursive: true);
        }

        public TransientTestFile CreateFile(string fileName, string contents)
            => TestEnvironment.CreateFile(fileName, contents);
    }
}
