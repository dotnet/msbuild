// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests;

public sealed class TaskHostOwnerLifetime_Tests(ITestOutputHelper output)
{
    [Fact]
    public void LateCallbackReplyCannotReachReplacementTaskHost()
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        env.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
        env.SetEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "0");
        env.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", null);
        env.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", "1");
        env.SetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT", Guid.NewGuid().ToString("N"));
        string assembly = typeof(SidecarProcessProbe).Assembly.Location;
        TransientTestFile outerPid = env.CreateFile("outer.pid", "");
        TransientTestFile replacementPid = env.CreateFile("replacement.pid", "");
        TransientTestFile finalPid = env.CreateFile("final.pid", "");
        TransientTestFile project = env.CreateFile("nestedCrash.proj", $"""
            <Project>
              <UsingTask TaskName="NestedCrashCallTask" AssemblyFile="{assembly}" />
              <UsingTask TaskName="ExitTaskHostTask" AssemblyFile="{assembly}" />
              <UsingTask TaskName="SidecarProcessProbe" AssemblyFile="{assembly}" />
              <Target Name="Build">
                <NestedCrashCallTask ReportFile="{outerPid.Path}" ContinueOnError="ErrorAndContinue" />
                <SidecarProcessProbe ReportFile="{finalPid.Path}" />
              </Target>
              <Target Name="CrashAndContinue">
                <ExitTaskHostTask ContinueOnError="ErrorAndContinue" />
                <SidecarProcessProbe ReportFile="{replacementPid.Path}" />
              </Target>
            </Project>
            """);

        string log = RunnerUtilities.ExecBootstrapedMSBuild(
            $"\"{project.Path}\" -m:1 -mt:false -nr:true", out bool success, outputHelper: output, timeoutMilliseconds: 60_000);
        success.ShouldBeFalse("the deliberate TaskHost crash fails the build");
        string original = File.ReadAllText(outerPid.Path);
        string replacement = File.ReadAllText(replacementPid.Path);
        string last = File.ReadAllText(finalPid.Path);
        string[] observedPids = [original, replacement, last];
        foreach (string value in observedPids)
        {
            if (int.TryParse(value, out int pid))
            {
                env.WithTransientProcess(pid);
            }
        }
        original.ShouldNotBeEmpty(log);
        replacement.ShouldNotBeEmpty(log);
        replacement.ShouldNotBe(original);
        last.ShouldBe(replacement, "the old callback reply must not kill the replacement TaskHost");
        output.WriteLine($"OriginalTaskHost={original}; ReplacementTaskHost={replacement}; FinalTaskHost={last}");
    }

    [Theory]
    [InlineData("dispose")]
    [InlineData("reuse-off")]
    [InlineData("shutdown-all")]
    [InlineData("worker-shutdown")]
    [InlineData("active-shutdown")]
    [InlineData("active-child-crash")]
    public void HostedSidecarsExitWhenTheirOwnerReleasesThem(string scenario)
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        env.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
        env.SetEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "0");
        env.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", null);
        env.SetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT", Guid.NewGuid().ToString("N"));
        TransientTestFile inner = env.CreateFile("inner.proj", $"""
            <Project>
              <UsingTask TaskName="SidecarProcessProbe" AssemblyFile="{typeof(SidecarProcessProbe).Assembly.Location}" />
              <Target Name="Build" Returns="$(HostPid)">
                <SidecarProcessProbe>
                  <Output TaskParameter="Pid" PropertyName="HostPid" />
                </SidecarProcessProbe>
              </Target>
              <Target Name="Empty" />
              <UsingTask TaskName="SidecarHoldTask" AssemblyFile="{typeof(SidecarHoldTask).Assembly.Location}" />
              <Target Name="Hold">
                <SidecarHoldTask ReadyFile="$(ReadyFile)" ReleaseFile="$(ReleaseFile)" />
              </Target>
            </Project>
            """);
        TransientTestFile outer = env.CreateFile("outer.proj", $"""
            <Project>
              <UsingTask TaskName="{nameof(HostingLifetimeProbe)}" AssemblyFile="{typeof(HostingLifetimeProbe).Assembly.Location}" />
              <Target Name="Build">
                <HostingLifetimeProbe Project="{inner.Path}" Scenario="{scenario}" />
              </Target>
            </Project>
            """);

        string log = RunnerUtilities.ExecBootstrapedMSBuild(
            $"\"{outer.Path}\" -m:1 -mt:false -nr:false", out bool success, outputHelper: output, timeoutMilliseconds: 60_000);
        success.ShouldBeTrue(log);
        log.ShouldContain("OwnerRemainsAlive=True");
        log.ShouldContain("SidecarsExited=3");
    }
}

public sealed class HostingLifetimeProbe : Microsoft.Build.Utilities.Task
{
    [Required]
    public string Project { get; set; } = null!;

    [Required]
    public string Scenario { get; set; } = null!;

    public override bool Execute()
    {
        using Process owner = Process.GetCurrentProcess();
        List<Process> children = [];
        string? originalForceOutOfProc = Environment.GetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC");
        try
        {
            bool useWorker = Scenario == "worker-shutdown";
            if (useWorker)
            {
                Environment.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", "1");
            }

            for (int round = 0; round < 3; round++)
            {
                if (Scenario is "active-shutdown" or "active-child-crash")
                {
                    RunActiveScenario(children);
                    continue;
                }
                if (useWorker)
                {
                RunWorkerScenario(children);
                continue;
                }

                Process child;
                using (BuildManager manager = new())
                {
                MockLogger logger = new();
                BuildParameters parameters = new()
                {
                    MultiThreaded = true,
                    MaxNodeCount = 1,
                    EnableNodeReuse = true,
                    Loggers = [logger]
                };
                BuildResult first = manager.Build(parameters, new BuildRequestData(Project, new Dictionary<string, string?>(), null, ["Build"], null));
                if (first.OverallResult != BuildResultCode.Success)
                {
                    Log.LogError(logger.FullLog);
                    return false;
                }

                int childPid = int.Parse(first.ResultsByTarget["Build"].Items[0].ItemSpec, CultureInfo.InvariantCulture);
                child = Process.GetProcessById(childPid);
                children.Add(child);
                Log.LogMessage(MessageImportance.High, "HostedSidecar Owner={0} Child={1} Scenario={2} Round={3}", owner.Id, childPid, Scenario, round);
                if (childPid == owner.Id || child.HasExited)
                {
                    Log.LogError("The probe did not create a reusable sidecar.");
                    return false;
                }

                BuildResult second = manager.Build(parameters, new BuildRequestData(Project, new Dictionary<string, string?>(), null, ["Build"], null));
                if (second.OverallResult != BuildResultCode.Success || second.ResultsByTarget["Build"].Items[0].ItemSpec != first.ResultsByTarget["Build"].Items[0].ItemSpec)
                {
                    Log.LogError("The owner did not reuse its sidecar.");
                    return false;
                }

                switch (Scenario)
                {
                    case "dispose":
                        break;
                    case "reuse-off":
                        parameters.EnableNodeReuse = false;
                        manager.Build(parameters, new BuildRequestData(Project, new Dictionary<string, string?>(), null, ["Empty"], null))
                            .OverallResult.ShouldBe(BuildResultCode.Success);
                        break;
                    case "shutdown-all":
                    case "worker-shutdown":
                        manager.ShutdownAllNodes();
                        break;
                    default:
                        throw new ArgumentException("Unknown lifetime scenario.", nameof(Scenario));
                }

                if (Scenario != "dispose")
                {
                    child.WaitForExit(10_000).ShouldBeTrue($"sidecar {childPid} survived {Scenario}");
                }
                }

                if (!child.WaitForExit(10_000))
                {
                    Log.LogError("Sidecar {0} survived {1} while owner {2} remained alive.", child.Id, Scenario, owner.Id);
                    return false;
                }
            }

            Log.LogMessage(MessageImportance.High, "OwnerRemainsAlive={0}; SidecarsExited={1}", !owner.HasExited, children.Count);
            return true;
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", originalForceOutOfProc);
            foreach (Process child in children)
            {
                if (!child.HasExited)
                {
                    child.Kill();
                    child.WaitForExit();
                }
                child.Dispose();
            }
        }
    }

    private void RunWorkerScenario(List<Process> children)
    {
        using BuildManager manager = new();
        ((IBuildComponentHost)manager).RegisterFactory(
            BuildComponentType.OutOfProcNodeProvider, _ => new RetainingWorkerProvider());
        MockLogger logger = new();
        BuildParameters parameters = new()
        {
            MultiThreaded = false,
            DisableInProcNode = true,
            MaxNodeCount = 1,
            EnableNodeReuse = true,
            Loggers = [logger]
        };
        Process? worker = null;
        Process? child = null;
        try
        {
            for (int build = 0; build < 2; build++)
            {
                string readyFile = Path.Combine(Path.GetDirectoryName(Project)!, Guid.NewGuid().ToString("N") + ".ready");
                string releaseFile = readyFile + ".release";
                manager.BeginBuild(parameters);
                try
                {
                    BuildSubmission submission = manager.PendBuildRequest(new BuildRequestData(
                        Project, new Dictionary<string, string?> { ["ReadyFile"] = readyFile, ["ReleaseFile"] = releaseFile }, null, ["Hold"], null));
                    submission.ExecuteAsync(null, null);
                    SpinWait.SpinUntil(() => File.Exists(readyFile) || submission.WaitHandle.WaitOne(0), 30_000).ShouldBeTrue();
                    File.Exists(readyFile).ShouldBeTrue("the task must be active before observing its processes");
                    using FileStream readyStream = new(readyFile, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using StreamReader readyReader = new(readyStream);
                    int childPid = int.Parse(readyReader.ReadToEnd(), CultureInfo.InvariantCulture);
                    if (child is null)
                    {
                        child = Process.GetProcessById(childPid);
                        children.Add(child);
                    }
                    else
                    {
                        childPid.ShouldBe(child.Id, "the recreated worker node must retain its sidecar provider");
                    }

                    foreach (Process process in manager.GetWorkerProcesses())
                    {
                        if (worker is null)
                        {
                            worker = Process.GetProcessById(process.Id);
                        }
                        else
                        {
                            process.Id.ShouldBe(worker.Id);
                        }
                    }
                    worker.ShouldNotBeNull();
                    Log.LogMessage(MessageImportance.High, "HostedSidecar Owner={0} Child={1} Scenario=worker-shutdown Build={2}", worker.Id, childPid, build);
                    File.WriteAllText(releaseFile, "release");
                    submission.WaitHandle.WaitOne(30_000).ShouldBeTrue();
                    submission.BuildResult.ShouldNotBeNull().OverallResult.ShouldBe(BuildResultCode.Success);
                }
                finally
                {
                    File.WriteAllText(releaseFile, "release");
                    manager.EndBuild();
                }

                worker.ShouldNotBeNull();
                worker.HasExited.ShouldBeFalse("this scenario pins worker reuse independently of machine-wide process counts");
            }

            manager.ShutdownAllNodes();
            Log.LogMessage(MessageImportance.High, "WorkerRetainedAfterBuild=True");
            worker!.WaitForExit(10_000).ShouldBeTrue();
            child!.WaitForExit(10_000).ShouldBeTrue("the sidecar must exit when its worker exits");
        }
        finally
        {
            if (worker is not null)
            {
                if (!worker.HasExited)
                {
                    worker.Kill();
                    worker.WaitForExit();
                }
                worker.Dispose();
            }
        }
    }

    private void RunActiveScenario(List<Process> children)
    {
        Process? child = null;
        string ready = Path.Combine(Path.GetDirectoryName(Project)!, Guid.NewGuid().ToString("N") + ".ready");
        string release = ready + ".release";
        using (BuildManager manager = new())
        {
            MockLogger logger = new();
            BuildParameters parameters = new() { MultiThreaded = true, MaxNodeCount = 1, EnableNodeReuse = true, Loggers = [logger] };
            manager.BeginBuild(parameters);
            try
            {
                BuildSubmission submission = manager.PendBuildRequest(new BuildRequestData(
                    Project, new Dictionary<string, string?> { ["ReadyFile"] = ready, ["ReleaseFile"] = release }, null, ["Hold"], null));
                submission.ExecuteAsync(null, null);
                SpinWait.SpinUntil(() => File.Exists(ready) || submission.WaitHandle.WaitOne(0), 30_000).ShouldBeTrue();
                using FileStream stream = new(ready, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using StreamReader reader = new(stream);
                child = Process.GetProcessById(int.Parse(reader.ReadToEnd(), CultureInfo.InvariantCulture));
                children.Add(child);
                Log.LogMessage(MessageImportance.High, "ActiveSidecar Child={0} Scenario={1}", child.Id, Scenario);

                if (Scenario == "active-shutdown")
                {
                    manager.ShutdownAllNodes();
                    child.HasExited.ShouldBeFalse();
                    submission.WaitHandle.WaitOne(0).ShouldBeFalse("idle-node shutdown must not interrupt an active task");
                    File.WriteAllText(release, "release");
                }
                else
                {
                    child.Kill();
                    child.WaitForExit();
                }

                submission.WaitHandle.WaitOne(30_000).ShouldBeTrue();
                submission.BuildResult.ShouldNotBeNull().OverallResult.ShouldBe(
                    Scenario == "active-shutdown" ? BuildResultCode.Success : BuildResultCode.Failure);
            }
            finally
            {
                File.WriteAllText(release, "release");
                manager.EndBuild();
            }

            var provider = (NodeProviderOutOfProcTaskHost)((IBuildComponentHost)manager).GetComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);
            provider.TaskHandlerRegistrationCount.ShouldBe(0);
            Log.LogMessage(MessageImportance.High, "ActiveCaseCompleted=True; HandlerRegistrations={0}", provider.TaskHandlerRegistrationCount);
        }

        child.ShouldNotBeNull().WaitForExit(10_000).ShouldBeTrue();
    }

    private sealed class RetainingWorkerProvider : NodeProviderOutOfProc
    {
        protected override int GetNodeReuseThreshold() => int.MaxValue;
    }
}

public sealed class SidecarProcessProbe : Microsoft.Build.Utilities.Task
{
    [Output]
    public int Pid { get; set; }

    public string? ReportFile { get; set; }

    public override bool Execute()
    {
        using Process process = Process.GetCurrentProcess();
        Pid = process.Id;
        if (ReportFile is not null)
        {
            File.WriteAllText(ReportFile, Pid.ToString(CultureInfo.InvariantCulture));
        }
        return true;
    }
}

public sealed class NestedCrashCallTask : Microsoft.Build.Utilities.Task
{
    public string ReportFile { get; set; } = null!;

    public override bool Execute()
    {
        using Process process = Process.GetCurrentProcess();
        File.WriteAllText(ReportFile, process.Id.ToString(CultureInfo.InvariantCulture));
        return BuildEngine.BuildProjectFile(BuildEngine.ProjectFileOfTaskNode, ["CrashAndContinue"], null, null);
    }
}

public sealed class ExitTaskHostTask : Microsoft.Build.Utilities.Task
{
    public override bool Execute()
    {
        Environment.Exit(23);
        return false;
    }
}

public sealed class SidecarHoldTask : Microsoft.Build.Utilities.Task
{
    [Required]
    public string ReadyFile { get; set; } = null!;

    [Required]
    public string ReleaseFile { get; set; } = null!;

    public override bool Execute()
    {
        using Process process = Process.GetCurrentProcess();
        File.WriteAllText(ReadyFile + ".tmp", process.Id.ToString(CultureInfo.InvariantCulture));
        File.Move(ReadyFile + ".tmp", ReadyFile);
        Stopwatch timer = Stopwatch.StartNew();
        while (!File.Exists(ReleaseFile))
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(45))
            {
                Log.LogError("Timed out waiting for the lifecycle probe to release this task.");
                return false;
            }
            Thread.Sleep(10);
        }
        return true;
    }
}
