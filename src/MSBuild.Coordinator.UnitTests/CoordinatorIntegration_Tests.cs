// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
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

    [ActiveIssue("https://github.com/dotnet/msbuild/issues/14516")]
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NestedBuild_InheritsCoordinatorGrant_DoesNotDeadlock(bool multiThreaded, bool taskHost)
    {
        using var helper = new CoordinatorTestHelper(outputHelper, nodeBudget: 1);

        TransientTestFile childProject = helper.CreateFile(
            "nested.proj",
            """
            <Project>
              <Target Name="Build">
                <Message Text="NestedMaxNodeCount=$(MSBuildNodeCount)" Importance="High" />
              </Target>
            </Project>
            """);

        string taskFactory = taskHost ? """TaskFactory="TaskHostFactory" """ : string.Empty;
        TransientTestFile rootProject = helper.CreateFile(
            "root.proj",
            $"""
            <Project>
              <UsingTask TaskName="Exec" AssemblyFile="$([System.IO.Path]::Combine('$(MSBuildToolsPath)', 'Microsoft.Build.Tasks.Core.dll'))" {taskFactory}/>
              <Target Name="Build">
                <Exec Command="$(NestedMSBuildCommand) &quot;$(ChildProject)&quot; /m:8 /nr:false /p:UseSharedCompilation=false /v:n" />
              </Target>
            </Project>
            """);

        var (success, buildOutput) = await RunnerUtilities.ExecBootstrappedMSBuildAsync(
            $"\"{rootProject.Path}\" /m:1 /mt:{multiThreaded} /nr:false /v:n /p:NestedMSBuildCommand=\"{RunnerUtilities.BootstrapMSBuildCommand}\" /p:ChildProject=\"{childProject.Path}\"",
            outputHelper: outputHelper,
            timeoutMilliseconds: 60_000);

        outputHelper.WriteLine(buildOutput);

        success.ShouldBeTrue("Build failed");
        buildOutput.ShouldContain("NestedMaxNodeCount=1");
        AssertNestedGrantReceived(buildOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaticGraphRestore_SharesCoordinatorGrant(bool multiThreaded)
    {
        using var helper = new CoordinatorTestHelper(outputHelper, nodeBudget: 1);
        helper.TestEnvironment.SetEnvironmentVariable(
            "DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", Path.Combine(RunnerUtilities.BootstrapRootPath, "core"));

        TransientTestFolder folder = helper.CreateFolder();
        helper.CreateFile(folder, "NuGet.Config", """
            <configuration>
              <packageSources><clear /></packageSources>
            </configuration>
            """);
        TransientTestFile project = helper.CreateFile(folder, "restore.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net11.0</TargetFramework>
              </PropertyGroup>
              <Target Name="RestoreCompleted" AfterTargets="Restore">
                <Message Text="STATIC_GRAPH_RESTORE_COMPLETED" Importance="High" />
              </Target>
            </Project>
            """);

        var (success, buildOutput) = await RunnerUtilities.ExecBootstrappedMSBuildAsync(
            $"\"{project.Path}\" /t:Restore /p:RestoreUseStaticGraphEvaluation=true /mt:{multiThreaded} /m:8 /nr:false /v:n",
            outputHelper: outputHelper,
            timeoutMilliseconds: 60_000);

        success.ShouldBeTrue(buildOutput);
        buildOutput.ShouldContain("STATIC_GRAPH_RESTORE_COMPLETED");
        File.Exists(Path.Combine(folder.Path, "obj", "project.assets.json")).ShouldBeTrue();
        AssertNestedGrantReceived(buildOutput);
    }

    private static void AssertNestedGrantReceived(string buildOutput)
    {
        // With the parent's one-node grant still active, a second grant can only be a nested join.
        string grantMessage = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("CoordinatorNodeGrantReceived", 1);
        int parentGrant = buildOutput.IndexOf(grantMessage, StringComparison.Ordinal);
        parentGrant.ShouldBeGreaterThanOrEqualTo(0);
        buildOutput.IndexOf(grantMessage, parentGrant + grantMessage.Length, StringComparison.Ordinal).ShouldBeGreaterThan(parentGrant);
    }

    [ActiveIssue("https://github.com/dotnet/msbuild/issues/14488")]
    [Fact]
    public async Task NuGetStaticGraphRestore_InheritsCoordinatorGrant_DoesNotDeadlock()
    {
        using var helper = new CoordinatorTestHelper(outputHelper, nodeBudget: 1);

        TransientTestFolder projectFolder = helper.CreateFolder();
        TransientTestFile project = helper.CreateFile(
            projectFolder,
            "StaticGraphRestoreRepro.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net11.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        helper.CreateFile(
            projectFolder,
            "Class1.cs",
            """
            namespace StaticGraphRestoreRepro;

            public class Class1
            {
            }
            """);

        var (success, buildOutput) = await RunnerUtilities.ExecBootstrappedMSBuildAsync(
            $"\"{project.Path}\" /restore /m:16 /p:RestoreUseStaticGraphEvaluation=true /p:RestoreIgnoreFailedSources=true /p:UseSharedCompilation=false /v:n",
            outputHelper: outputHelper,
            timeoutMilliseconds: 60_000);

        outputHelper.WriteLine(buildOutput);

        success.ShouldBeTrue("Build failed");
        buildOutput.ShouldNotContain("Failed to connect to the build coordinator");
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
            TestEnvironment.SetEnvironmentVariable(Constants.GrantIdEnvVarName, null);
            TestEnvironment.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);

            if (nodeBudget is int nodeBudgetValue)
            {
                TestEnvironment.SetEnvironmentVariable(Constants.NodeBudgetEnvVarName, nodeBudgetValue.ToString());
            }

            // Keep this above transient startup/handshake jitter seen in CI, while still
            // allowing coordinator processes to shut down quickly after each test.
            TestEnvironment.SetEnvironmentVariable(Constants.ShutdownTimeoutEnvVarName, "1000");

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

        public TransientTestFile CreateFile(TransientTestFolder folder, string fileName, string contents)
            => TestEnvironment.CreateFile(folder, fileName, contents);

        public TransientTestFile CreateFile(string fileName, string contents)
            => TestEnvironment.CreateFile(fileName, contents);

        public TransientTestFolder CreateFolder()
            => TestEnvironment.CreateFolder();
    }
}
