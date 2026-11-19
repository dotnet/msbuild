// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.Engine.UnitTests.BackEnd;

public sealed class TaskHostYield_Tests(ITestOutputHelper output)
{
    private static readonly string[] s_roles = ["A", "B", "C"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OverlappingYieldedTasksShareTaskHostProcesses(bool multiThreaded)
        => RunOverlappingTasks(multiThreaded, clr4: false);

#if NET
    [WindowsOnlyFact]
    public void DotNetMSBuildOverlappingClr4TasksShareTaskHostProcesses()
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
        foreach (string role in s_roles)
        {
            // Each task checks that its own output, not a concurrently yielded task's, came back.
            File.WriteAllText(Path.Combine(directory, $"{role}.proj"), $"""
                <Project>
                  {usingTask}
                  <Target Name="Build">
                    <TaskHostYieldProbe Directory="{directory}" Role="{role}" WaitFor="A;B;C">
                      <Output TaskParameter="Result" PropertyName="ProbeResult" />
                    </TaskHostYieldProbe>
                    <Error Condition="'$(ProbeResult)' != '{role}'" Text="Task {role} received result '$(ProbeResult)'." />
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
        string binlog = Path.Combine(directory, "yield.binlog");

        string log = RunnerUtilities.ExecBootstrapedMSBuild(
            $"\"{root}\" -m:2 -mt:{multiThreaded} -nr:false -v:minimal -bl:\"{binlog}\"",
            out bool success, outputHelper: output, timeoutMilliseconds: 120_000);

        success.ShouldBeTrue(log);
        int[] overlapping = [.. s_roles.Select(ReadPid)];

        // The three task intervals all overlap, and only two scheduler nodes own TaskHosts, so at
        // least two of the overlapping tasks ran in the same TaskHost process.
        overlapping.Distinct().Count().ShouldBeLessThanOrEqualTo(2, "Overlapping yielded tasks must share TaskHost processes.");
        ReadPid("E").ShouldBe(ReadPid("D"), "Sequential tasks must reuse the idle TaskHost.");

        Dictionary<string, string> loggedProjects = [];
        BinaryLogReplayEventSource replay = new();
        replay.MessageRaised += (_, e) =>
        {
            if (e.Message?.StartsWith("LOGGED ", StringComparison.Ordinal) == true)
            {
                loggedProjects[e.Message.Substring("LOGGED ".Length)] = Path.GetFileNameWithoutExtension(e.ProjectFile);
            }
        };
        replay.Replay(binlog);
        foreach (string role in s_roles)
        {
            log.ShouldContain($"REACQUIRED {role}");
            loggedProjects.ShouldContainKeyAndValue(role, role, "A yielded task's log messages must be attributed to its own project.");
        }

        foreach (string role in new[] { "A", "B", "C", "D", "E" })
        {
            int pid = ReadPid(role);
            env.WithTransientProcess(pid);
            try
            {
                using System.Diagnostics.Process host = System.Diagnostics.Process.GetProcessById(pid);
                host.WaitForExit(10_000).ShouldBeTrue("Shared TaskHosts must exit when node reuse is disabled.");
            }
            catch (ArgumentException)
            {
                // The host exited before it could be opened.
            }
        }

        int ReadPid(string role) => int.Parse(File.ReadAllText(Path.Combine(directory, $"{role}.pid")), CultureInfo.InvariantCulture);
    }

    [Theory]
    [InlineData("complete", false)]
    [InlineData("throw", false)]
    [InlineData("double-yield", false)]
    [InlineData("reacquire-first", false)]
    [InlineData("wrong-thread", false)]
    [InlineData("complete", true)]
    public void UnbalancedOrInvalidYieldDoesNotStrandTheScheduler(string operation, bool disableWave)
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        env.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", disableWave ? "18.14" : null);
        TransientTestFile project = env.CreateFile("Yield.proj", $"""
            <Project>
              <UsingTask TaskName="{typeof(TaskHostYieldProbe).FullName}" AssemblyFile="{typeof(TaskHostYieldProbe).Assembly.Location}" TaskFactory="TaskHostFactory" />
              <Target Name="Build">
                <TaskHostYieldProbe Operation="{operation}" />
                <TaskHostYieldProbe Role="Next" />
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
            log.ShouldContain("REACQUIRED Next");
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

    [Output]
    public string Result { get; set; } = string.Empty;

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
            // Yielded tasks share the process, so they must not depend on process-wide environment state.
            File.WriteAllText(Path.Combine(Directory, $"{Role}.pid"), EnvironmentUtilities.CurrentProcessId.ToString(CultureInfo.InvariantCulture));
            if (WaitFor.Length != 0)
            {
                foreach (string peer in WaitFor.Split(';'))
                {
                    SpinWait.SpinUntil(() => File.Exists(Path.Combine(Directory, $"{peer}.pid")), TimeSpan.FromSeconds(60))
                        .ShouldBeTrue($"Task {Role} must overlap {peer} while yielded.");
                }
            }

            // Every overlapping task logs and calls back while the others are yielded in the same host.
            Log.LogMessage(MessageImportance.High, $"LOGGED {Role}");
            ((IBuildEngine9)BuildEngine).RequestCores(1).ShouldBeGreaterThanOrEqualTo(1);
        }

        engine.Reacquire();
        Log.LogMessage(MessageImportance.High, $"REACQUIRED {Role}");
        Result = Role;
        return true;
    }
}
