// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.Engine.UnitTests.BackEnd;

public sealed class TaskHostYield_Tests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OverlappingYieldedTasksUseSeparateProcessesAndReuseIdleHosts(bool multiThreaded)
        => RunOverlappingTasks(multiThreaded, clr4: false);

#if NET
    [WindowsOnlyFact]
    public void DotNetMSBuildOverlappingClr4TasksUseSeparateProcesses()
        => RunOverlappingTasks(multiThreaded: false, clr4: true);
#endif

    private void RunOverlappingTasks(bool multiThreaded, bool clr4)
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        env.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
        env.SetEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "0");
        string assembly = typeof(TaskHostYieldProbe).Assembly.Location;
        if (clr4)
        {
            assembly = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(assembly)!, "..", "net472", Path.ChangeExtension(Path.GetFileName(assembly), ".exe")));
            File.Exists(assembly).ShouldBeTrue("The desktop test tasks must be built before testing cross-runtime yielding.");
            env.SetEnvironmentVariable("VSINSTALLDIR", Path.Combine(RunnerUtilities.BootstrapRootPath, "net472"));
            env.SetEnvironmentVariable("VisualStudioVersion", MSBuildConstants.CurrentVisualStudioVersion);
        }
        string directory = env.CreateFolder().Path;
        string usingTask = $"""
            <UsingTask TaskName="{typeof(TaskHostYieldProbe).FullName}" AssemblyFile="{assembly}" TaskFactory="TaskHostFactory" {(clr4 ? """Runtime="CLR4" Architecture="x86" """ : string.Empty)} />
            """;
        foreach (string role in new[] { "A", "B", "C" })
        {
            File.WriteAllText(Path.Combine(directory, $"{role}.proj"), $"""
                <Project>
                  {usingTask}
                  <Target Name="Build">
                    <TaskHostYieldProbe Directory="{directory}" Role="{role}" WaitFor="A;B;C" />
                  </Target>
                </Project>
                """);
        }

        string root = Path.Combine(directory, "Root.proj");
        File.WriteAllText(root, $"""
            <Project>
              {usingTask}
              <Target Name="Build">
                <MSBuild Projects="A.proj;B.proj;C.proj" BuildInParallel="true" />
                <TaskHostYieldProbe Directory="{directory}" Role="D" />
                <TaskHostYieldProbe Directory="{directory}" Role="E" />
              </Target>
            </Project>
            """);

        string log = RunnerUtilities.ExecBootstrapedMSBuild(
            $"\"{root}\" -m:2 -mt:{multiThreaded} -nr:false -v:minimal",
            out bool success, outputHelper: output, timeoutMilliseconds: 120_000);

        success.ShouldBeTrue(log);
        int a = ReadPid("A");
        int b = ReadPid("B");
        a.ShouldNotBe(b, "Both task intervals overlap while yielded.");
        int c = ReadPid("C");
        c.ShouldNotBe(a);
        c.ShouldNotBe(b, "Three overlapping hosted tasks require more processes than the two scheduler nodes.");
        int d = ReadPid("D");
        // The root may be scheduled on a different node, but its own sequential tasks must share a slot.
        ReadPid("E").ShouldBe(d, "Sequential tasks must reuse an idle host slot.");
        foreach (string role in new[] { "A", "B", "C", "D", "E" })
        {
            log.ShouldContain($"REACQUIRED {role}");
            int pid = ReadPid(role);
            env.WithTransientProcess(pid);
            try
            {
                using Process host = Process.GetProcessById(pid);
                host.WaitForExit(10_000).ShouldBeTrue("All occupied and idle hosts must exit when node reuse is disabled.");
            }
            catch (ArgumentException)
            {
                // The host exited before it could be opened.
            }
        }

        int ReadPid(string role) => int.Parse(File.ReadAllText(Path.Combine(directory, $"{role}.pid")), CultureInfo.InvariantCulture);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("throw")]
    [InlineData("double-yield")]
    [InlineData("reacquire-first")]
    [InlineData("wrong-thread")]
    public void UnbalancedOrInvalidYieldDoesNotStrandTheScheduler(string operation)
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        env.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
        TransientTestFile project = env.CreateFile("Yield.proj", $"""
            <Project>
              <UsingTask TaskName="{typeof(TaskHostYieldProbe).FullName}" AssemblyFile="{typeof(TaskHostYieldProbe).Assembly.Location}" TaskFactory="TaskHostFactory" />
              <Target Name="Build">
                <TaskHostYieldProbe Operation="{operation}" />
                <Message Text="AFTER YIELD" Importance="High" />
              </Target>
            </Project>
            """);
        string log = RunnerUtilities.ExecBootstrapedMSBuild(
            $"\"{project.Path}\" -m:1 -nr:false -v:minimal",
            out bool success, outputHelper: output, timeoutMilliseconds: 30_000);

        success.ShouldBe(operation != "throw", log);
        if (operation != "throw")
        {
            log.ShouldContain("AFTER YIELD");
        }
    }
}

public sealed class TaskHostYieldProbe : Microsoft.Build.Utilities.Task
{
    public string Directory { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string WaitFor { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;

    public override bool Execute()
    {
        IBuildEngine3 engine = (IBuildEngine3)BuildEngine;
        if (Operation == "reacquire-first")
        {
            Should.Throw<Exception>(() => engine.Reacquire());
            return true;
        }

        engine.Yield();
        if (Operation == "complete")
        {
            return true;
        }

        if (Operation == "throw")
        {
            throw new InvalidOperationException("Yield probe failure");
        }

        if (Operation == "double-yield")
        {
            Should.Throw<Exception>(() => engine.Yield());
        }

        if (Operation == "wrong-thread")
        {
            Exception? failure = null;
            Thread other = new(() =>
            {
                try
                {
                    engine.Reacquire();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            other.Start();
            other.Join(10_000).ShouldBeTrue();
            failure.ShouldNotBeNull();
        }

        if (Directory.Length != 0)
        {
            string originalDirectory = Environment.CurrentDirectory;
            const string variable = "MSBUILD_TASKHOST_YIELD_PROBE";
            string? originalValue = Environment.GetEnvironmentVariable(variable);
            Environment.SetEnvironmentVariable(variable, Role);
            try
            {
                File.WriteAllText(Path.Combine(Directory, $"{Role}.pid"), EnvironmentUtilities.CurrentProcessId.ToString(CultureInfo.InvariantCulture));
                if (WaitFor.Length != 0)
                {
                    foreach (string peer in WaitFor.Split(';'))
                    {
                        SpinWait.SpinUntil(() => File.Exists(Path.Combine(Directory, $"{peer}.pid")), TimeSpan.FromSeconds(60))
                            .ShouldBeTrue($"Task {Role} must overlap {peer} while yielded.");
                    }
                }

                Environment.GetEnvironmentVariable(variable).ShouldBe(Role);
                Environment.CurrentDirectory.ShouldBe(originalDirectory);
                ((IBuildEngine9)BuildEngine).RequestCores(1).ShouldBeGreaterThanOrEqualTo(1);
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, originalValue);
            }
        }

        engine.Reacquire();
        Log.LogMessage(MessageImportance.High, $"REACQUIRED {Role}");
        return true;
    }
}
