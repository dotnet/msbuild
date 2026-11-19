// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Execution;
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
        string assembly = UseTaskAssembly(env, clr4);
        string directory = env.CreateFolder().Path;
        string usingTask = UsingTask(assembly, clr4);
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

    private static string UseTaskAssembly(TestEnvironment env, bool clr4)
    {
        string assembly = typeof(TaskHostYieldProbe).Assembly.Location;
        if (!clr4)
        {
            return assembly;
        }

        assembly = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(assembly)!, "..", "net472", Path.ChangeExtension(Path.GetFileName(assembly), ".exe")));
        File.Exists(assembly).ShouldBeTrue("The desktop test tasks must be built before testing cross-runtime yielding.");
        env.SetEnvironmentVariable("VSINSTALLDIR", Path.Combine(RunnerUtilities.BootstrapRootPath, "net472"));
        env.SetEnvironmentVariable("VisualStudioVersion", MSBuildConstants.CurrentVisualStudioVersion);
        return assembly;
    }

    private static string UsingTask(string assembly, bool clr4) => $"""
        <UsingTask TaskName="{typeof(TaskHostYieldProbe).FullName}" AssemblyFile="{assembly}" TaskFactory="TaskHostFactory" {(clr4 ? """Runtime="CLR4" Architecture="x86" """ : string.Empty)} />
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedBuildCallbacksFromYieldedAndReacquiredTasksShareTheTaskHost(bool multiThreaded)
        => RunNestedCallbacks(multiThreaded, clr4: false);

#if NET
    [WindowsOnlyFact]
    public void DotNetMSBuildNestedClr4CallbacksFromYieldedTasksKeepExplicitLaunchSettings()
        => RunNestedCallbacks(multiThreaded: false, clr4: true);
#endif

    private void RunNestedCallbacks(bool multiThreaded, bool clr4)
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        env.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
        env.SetEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "0");
        string assembly = UseTaskAssembly(env, clr4);
        string directory = env.CreateFolder().Path;
        string[] levels = ["L3", "L2", "L1", "L0"];
        for (int i = 0; i < levels.Length; i++)
        {
            // L3 builds its child after it reacquires; L2 and L1 build their children while yielded. The blocked
            // ancestors and the yielded descendant are all task contexts in the same TaskHost, and every level
            // uses the same explicit host launch settings.
            string child = i + 1 < levels.Length ? $"{levels[i + 1]}.proj" : string.Empty;
            File.WriteAllText(Path.Combine(directory, $"{levels[i]}.proj"), $"""
                <Project>
                  {UsingTask(assembly, clr4)}
                  <Target Name="Build" Returns="$(ProbeResult)">
                    <TaskHostYieldProbe Directory="{directory}" Role="{levels[i]}" Child="{child}" BuildChildWhileYielded="{i is 1 or 2}">
                      <Output TaskParameter="Result" PropertyName="ProbeResult" />
                    </TaskHostYieldProbe>
                  </Target>
                </Project>
                """);
        }

        string binlog = Path.Combine(directory, "nested.binlog");
        string log = RunnerUtilities.ExecBootstrapedMSBuild(
            $"\"{Path.Combine(directory, "L3.proj")}\" -m:1 -mt:{multiThreaded} -nr:false -v:minimal -bl:\"{binlog}\"",
            out bool success, outputHelper: output, timeoutMilliseconds: 120_000);

        success.ShouldBeTrue(log);
        int pid = ReadPid("L3");
        foreach (string level in levels)
        {
            ReadPid(level).ShouldBe(pid, "Nested callback builds must run their tasks in the blocked task's TaskHost.");
            log.ShouldContain($"REACQUIRED {level}");
        }

        Dictionary<string, string> loggedProjects = [];
        Dictionary<string, string> results = [];
        BinaryLogReplayEventSource replay = new();
        replay.MessageRaised += (_, e) =>
        {
            if (e.Message?.StartsWith("LOGGED ", StringComparison.Ordinal) == true)
            {
                loggedProjects[e.Message.Substring("LOGGED ".Length)] = Path.GetFileNameWithoutExtension(e.ProjectFile);
            }
            else if (e.Message?.StartsWith("RESULT ", StringComparison.Ordinal) == true)
            {
                string[] parts = e.Message.Substring("RESULT ".Length).Split(' ');
                results[parts[0]] = parts[1];
            }
        };
        replay.Replay(binlog);
        for (int i = 0; i < levels.Length; i++)
        {
            loggedProjects.ShouldContainKeyAndValue(levels[i], levels[i], "Each nested level's messages must be attributed to its own project.");
            results.ShouldContainKeyAndValue(levels[i], string.Join("/", levels.Skip(i)), "Each level must receive its own child's target outputs.");
        }

        env.WithTransientProcess(pid);

        int ReadPid(string role) => int.Parse(File.ReadAllText(Path.Combine(directory, $"{role}.pid")), CultureInfo.InvariantCulture);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("kill")]
    public void FaultWhileBlockedInReacquireDoesNotStrandTheBuild(string fault)
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        env.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
        env.SetEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "0");

        // With three cores, the follow-up task gets all of them only if the faulted task's core was returned.
        env.SetEnvironmentVariable("MSBUILDCORELIMIT", "3");
        string directory = env.CreateFolder().Path;
        string usingTask = UsingTask(typeof(TaskHostYieldProbe).Assembly.Location, clr4: false);
        foreach (string role in s_roles)
        {
            File.WriteAllText(Path.Combine(directory, $"{role}.proj"), $"""
                <Project>
                  {usingTask}
                  <Target Name="Build">
                    <TaskHostYieldProbe Directory="{directory}" Role="{role}" Operation="reacquire-fault" />
                  </Target>
                </Project>
                """);
        }

        string root = Path.Combine(directory, "Root.proj");
        File.WriteAllText(root, $"""
            <Project>
              {usingTask}
              <Target Name="Build">
                <MSBuild Projects="A.proj;B.proj;C.proj" BuildInParallel="true" ContinueOnError="true" />
                <TaskHostYieldProbe Directory="{directory}" Role="Next" Operation="cores" />
              </Target>
            </Project>
            """);
        string binlog = Path.Combine(directory, "fault.binlog");

        string log = RunnerUtilities.ExecBootstrapedMSBuild(
            $"\"{root}\" -m:2 -nr:false -v:minimal -bl:\"{binlog}\" -logger:{typeof(TaskHostYieldFaultLogger).FullName},\"{typeof(TaskHostYieldFaultLogger).Assembly.Location}\";{fault}",
            out bool success, outputHelper: output, timeoutMilliseconds: 120_000);

        // ContinueOnError lets a killed host's errors leave the build successful; a canceled build fails.
        success.ShouldBe(fault == "kill", log);
        Dictionary<string, int> pids = s_roles.ToDictionary(role => role, ReadPid);
        foreach (int pid in pids.Values.Distinct())
        {
            env.WithTransientProcess(pid);
        }

        Dictionary<string, string> messages = [];
        Dictionary<string, List<string>> errors = [];
        BinaryLogReplayEventSource replay = new();
        replay.MessageRaised += (_, e) =>
        {
            string[] parts = e.Message?.Split(' ') ?? [];
            if (parts.Length >= 2 && parts[0] is "HOLDING" or "CANCELED" or "REACQUIRING" or "REACQUIRED" or "CORES")
            {
                messages[$"{parts[0]} {parts[1]}"] = $"{Path.GetFileNameWithoutExtension(e.ProjectFile)} {e.Message}";
            }
            else if (e.Message?.StartsWith("REACQUIRE FAILED ", StringComparison.Ordinal) == true)
            {
                messages[$"FAILED {parts[2]}"] = $"{Path.GetFileNameWithoutExtension(e.ProjectFile)} {e.Message}";
            }
        };
        replay.ErrorRaised += (_, e) =>
        {
            string project = Path.GetFileNameWithoutExtension(e.ProjectFile);
            if (!errors.TryGetValue(project, out List<string>? list))
            {
                errors[project] = list = [];
            }

            list.Add(e.Message ?? string.Empty);
        };
        replay.Replay(binlog);

        // Every task chose the same roles, and each task's messages are attributed to its own project.
        string holder = s_roles.Single(role => messages.ContainsKey($"HOLDING {role}"));
        string victim = s_roles.Single(role => messages.ContainsKey($"REACQUIRING {role}"));
        string other = s_roles.Single(role => role != holder && role != victim);
        pids[victim].ShouldBe(pids[holder], "The victim waits for the node, and so the TaskHost, that the holder executes in.");
        messages[$"HOLDING {holder}"].ShouldStartWith($"{holder} ");
        messages[$"REACQUIRING {victim}"].ShouldStartWith($"{victim} ");
        messages[$"REACQUIRED {other}"].ShouldStartWith($"{other} ");
        messages.ShouldNotContainKey($"REACQUIRED {victim}", "Reacquire must not return without the node.");
        errors.ShouldNotContainKey(other, "The task that completed before the fault must not see it.");

        if (fault == "cancel")
        {
            // The cancel reached each task in the shared TaskHost: the holder through its own Cancel, and the
            // victim through its failed Reacquire, which waited for the fault.
            messages[$"CANCELED {holder}"].ShouldStartWith($"{holder} ");
            messages.ShouldNotContainKey($"CANCELED {victim}");
            string[] failure = messages[$"FAILED {victim}"].Split(' ');
            failure[0].ShouldBe(victim);

            // Engine exception types are not constructible in the TaskHost, so the abort keeps its remote type name.
            failure[4].ShouldBe("GenericBuildTransferredException");
            int.Parse(failure[5], CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(500, "Reacquire must block until the build is canceled.");
            File.ReadAllText(Path.Combine(directory, $"{victim}.failed")).ShouldContain("Microsoft.Build.Exceptions.BuildAbortedException->");
            messages.ShouldNotContainKey("CORES Next", "Canceling the build stops the follow-up task.");
        }
        else
        {
            // Both tasks in the killed TaskHost fail, and the scheduler still runs the follow-up task in a new
            // TaskHost with every core available, so no registration, node, or core is stranded.
            messages.ShouldNotContainKey($"CANCELED {holder}");
            messages.ShouldNotContainKey($"FAILED {victim}");
            errors.ShouldContainKey(holder);
            errors.ShouldContainKey(victim);
            messages["CORES Next"].ShouldBe("Root CORES Next 3");
            ReadPid("Next").ShouldNotBe(pids[victim]);
            env.WithTransientProcess(ReadPid("Next"));
        }

        foreach (int pid in pids.Values.Distinct())
        {
            try
            {
                using Process host = Process.GetProcessById(pid);
                host.WaitForExit(10_000).ShouldBeTrue("TaskHosts must exit after the fault when node reuse is disabled.");
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
    [InlineData("build-while-yielded", false)]
    [InlineData("throw", false)]
    [InlineData("double-yield", false)]
    [InlineData("reacquire-first", false)]
    [InlineData("wrong-thread", false)]
    [InlineData("complete", true)]
    [InlineData("build-while-yielded", true)]
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
              <Target Name="Nested">
                <Message Text="NESTED WHILE YIELDED" Importance="High" />
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

        if (operation == "build-while-yielded")
        {
            log.ShouldContain("NESTED WHILE YIELDED");
        }
    }
}

public sealed class TaskHostYieldProbe : Microsoft.Build.Utilities.Task, ICancelableTask
{
    private readonly ManualResetEventSlim _canceled = new();

    public string Directory { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string WaitFor { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string Child { get; set; } = string.Empty;
    public bool BuildChildWhileYielded { get; set; }

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

        if (Operation == "reacquire-fault")
        {
            return RunReacquireFault(engine);
        }

        if (Operation == "cores")
        {
            File.WriteAllText(PidFile(Role), EnvironmentUtilities.CurrentProcessId.ToString(CultureInfo.InvariantCulture));
            IBuildEngine9 engine9 = (IBuildEngine9)BuildEngine;
            int cores = engine9.RequestCores(3);
            Log.LogMessage(MessageImportance.High, $"CORES {Role} {cores}");
            engine9.ReleaseCores(cores);
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

        if (Operation == "build-while-yielded")
        {
            // Before TaskHost yielding, Yield was a no-op here, so building while "yielded" worked. It still must.
            BuildEngine.BuildProjectFile(BuildEngine.ProjectFileOfTaskNode, ["Nested"], null, null).ShouldBeTrue();
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

        string? childResult = Child.Length != 0 && BuildChildWhileYielded ? BuildChild() : null;
        engine.Reacquire();
        Log.LogMessage(MessageImportance.High, $"REACQUIRED {Role}");
        Result = Role;
        if (Child.Length != 0)
        {
            Result = $"{Role}/{childResult ?? BuildChild()}";
        }

        if (Directory.Length != 0)
        {
            Log.LogMessage(MessageImportance.High, $"RESULT {Role} {Result}");
        }

        return true;
    }

    public void Cancel() => _canceled.Set();

    private string BuildChild()
    {
        Hashtable outputs = new();
        BuildEngine.BuildProjectFile(Path.Combine(Directory, Child), ["Build"], null, outputs).ShouldBeTrue();
        return ((ITaskItem[])outputs["Build"]!).Single().ItemSpec;
    }

    private string PidFile(string role) => Path.Combine(Directory, $"{role}.pid");

    private string MarkerFile(string role, string state) => Path.Combine(Directory, $"{role}.{state}");

    private static void WaitForFile(string path) =>
        SpinWait.SpinUntil(() => File.Exists(path), TimeSpan.FromSeconds(60)).ShouldBeTrue($"Timed out waiting for {path}.");

    /// <summary>
    /// Makes one of three yielded tasks wait in Reacquire while another task executes on its node, so the test
    /// can cancel the build or kill the TaskHost at that point.
    /// </summary>
    private bool RunReacquireFault(IBuildEngine3 engine)
    {
        engine.Yield();
        int pid = EnvironmentUtilities.CurrentProcessId;
        File.WriteAllText(PidFile(Role), pid.ToString(CultureInfo.InvariantCulture));
        string[] roles = ["A", "B", "C"];
        foreach (string role in roles)
        {
            WaitForFile(PidFile(role));
        }

        // Each scheduler node has one shared TaskHost, so with two nodes at least two of the three yielded tasks
        // share a process and a node. All tasks pick the same pair: the holder executes on the node, then the
        // victim tries to reacquire it.
        int[] pids = [.. roles.Select(role => int.Parse(File.ReadAllText(PidFile(role)), CultureInfo.InvariantCulture))];
        (int holder, int victim) = pids[0] == pids[1] ? (0, 1) : pids[0] == pids[2] ? (0, 2) : (1, 2);
        string other = roles[3 - holder - victim];

        if (Role == other)
        {
            engine.Reacquire();
            Log.LogMessage(MessageImportance.High, $"REACQUIRED {Role}");
            File.WriteAllText(MarkerFile(Role, "done"), string.Empty);
            Result = Role;
            return true;
        }

        if (Role == roles[holder])
        {
            // Let the other task finish first so that only the victim waits for this node.
            WaitForFile(MarkerFile(other, "done"));
            engine.Reacquire();
            Log.LogMessage(MessageImportance.High, $"HOLDING {Role}");
            File.WriteAllText(MarkerFile(Role, "holding"), string.Empty);
            _canceled.Wait(TimeSpan.FromSeconds(60)).ShouldBeTrue("The holder must be canceled or killed.");
            Log.LogMessage(MessageImportance.High, $"CANCELED {Role}");
            return true;
        }

        WaitForFile(MarkerFile(roles[holder], "holding"));

        // The granted core must be returned to the scheduler however the task ends.
        ((IBuildEngine9)BuildEngine).RequestCores(2).ShouldBeGreaterThanOrEqualTo(1);
        Log.LogMessage(MessageImportance.High, $"REACQUIRING {Role} {pid}");
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            engine.Reacquire();
        }
        catch (Exception ex)
        {
            string failure = $"{ex.GetType().Name} {stopwatch.ElapsedMilliseconds}";
            File.WriteAllText(MarkerFile(Role, "failed"), $"{failure}{Environment.NewLine}{ex}");
            Log.LogMessage(MessageImportance.High, $"REACQUIRE FAILED {Role} {failure}");
            return false;
        }

        Log.LogMessage(MessageImportance.High, $"REACQUIRED {Role}");
        return true;
    }
}

/// <summary>
/// Cancels the build or kills the TaskHost when a task reports that it is about to wait in Reacquire.
/// </summary>
public sealed class TaskHostYieldFaultLogger : ILogger
{
    private IEventSource? _eventSource;
    private int _faulted;

    public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;
    public string? Parameters { get; set; }

    public void Initialize(IEventSource eventSource)
    {
        _eventSource = eventSource;
        eventSource.MessageRaised += OnMessage;
    }

    private void OnMessage(object sender, BuildMessageEventArgs args)
    {
        if (args.Message?.StartsWith("REACQUIRING ", StringComparison.Ordinal) != true || Interlocked.Exchange(ref _faulted, 1) != 0)
        {
            return;
        }

        int pid = int.Parse(args.Message.Substring(args.Message.LastIndexOf(' ') + 1), CultureInfo.InvariantCulture);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            // Give the Reacquire request time to reach the scheduler; the holder keeps the node until the fault.
            Thread.Sleep(1_000);
            if (Parameters == "kill")
            {
                using Process host = Process.GetProcessById(pid);
                host.Kill();
            }
            else
            {
                BuildManager.DefaultBuildManager.CancelAllSubmissions();
            }
        });
    }

    public void Shutdown()
    {
        if (_eventSource is not null)
        {
            _eventSource.MessageRaised -= OnMessage;
        }
    }
}
