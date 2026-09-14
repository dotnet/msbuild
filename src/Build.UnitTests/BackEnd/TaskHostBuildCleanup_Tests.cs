// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests;

public sealed class TaskHostBuildCleanup_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void EndBuildWaitsForDisposalOnlyWhenReusing(bool reuse, bool useWorker, bool crashDuringCleanup)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
        env.SetEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "0");
        env.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", null);
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
        env.SetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT", Guid.NewGuid().ToString("N"));
        string assembly = typeof(TaskHostCleanupProbe).Assembly.Location;
        string gate = Path.Combine(env.CreateFolder().Path, "disposal");
        TransientTestFile inner = env.CreateFile("cleanup-inner.proj", $"""
            <Project>
              <UsingTask TaskName="RegisterGatedTaskObject" AssemblyFile="{assembly}" />
              <Target Name="Build" Returns="$(TaskHostPid)">
                <RegisterGatedTaskObject Gate="$(Gate)" RegisterObject="$(RegisterObject)">
                  <Output TaskParameter="Pid" PropertyName="TaskHostPid" />
                </RegisterGatedTaskObject>
              </Target>
            </Project>
            """);
        TransientTestFile outer = env.CreateFile("cleanup.proj", $"""
            <Project>
              <UsingTask TaskName="TaskHostCleanupProbe" AssemblyFile="{assembly}" />
              <Target Name="Build">
                <TaskHostCleanupProbe Project="{inner.Path}" Gate="{gate}"
                                      Reuse="{reuse}" UseWorker="{useWorker}" CrashDuringCleanup="{crashDuringCleanup}" />
              </Target>
            </Project>
            """);

        string log = RunnerUtilities.ExecBootstrapedMSBuild(
            $"\"{outer.Path}\" -m:1 -mt:false -nr:false -low:true",
            out bool success, outputHelper: _output, timeoutMilliseconds: 90_000);
        success.ShouldBeTrue(log);
        log.ShouldContain("CleanupPolicyVerified");
    }
}

public sealed class TaskHostCleanupProbe : Microsoft.Build.Utilities.Task
{
    [Required]
    public string Project { get; set; } = null!;

    [Required]
    public string Gate { get; set; } = null!;

    public bool Reuse { get; set; }
    public bool UseWorker { get; set; }
    public bool CrashDuringCleanup { get; set; }

    public override bool Execute()
    {
        // TestEnvironment would reset the default BuildManager that is executing this task.
        string? originalForceOutOfProc = Environment.GetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC");
        if (UseWorker)
        {
            Environment.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", "1");
        }

        using BuildManager manager = new();
        if (UseWorker)
        {
            ((IBuildComponentHost)manager).RegisterFactory(
                BuildComponentType.OutOfProcNodeProvider, _ => new HostingLifetimeProbe.RetainingWorkerProvider());
        }
        MockLogger logger = new();
        BuildParameters parameters = new()
        {
            MultiThreaded = !UseWorker,
            DisableInProcNode = UseWorker,
            MaxNodeCount = 1,
            EnableNodeReuse = Reuse,
            LowPriority = true,
            Loggers = [logger]
        };
        Task? endBuild = null;
        Process? child = null;
        bool buildStarted = false;
        bool shutdownRequested = false;
        try
        {
            manager.BeginBuild(parameters);
            buildStarted = true;
            BuildResult result = manager.PendBuildRequest(CreateRequest(registerObject: true)).Execute();
            result.OverallResult.ShouldBe(BuildResultCode.Success, logger.FullLog);
            child = Process.GetProcessById(int.Parse(result.ResultsByTarget["Build"].Items[0].ItemSpec, CultureInfo.InvariantCulture));
            endBuild = Task.Run(manager.EndBuild);

            SpinWait.SpinUntil(() => File.Exists(Gate + ".entered") || endBuild.IsFaulted, 30_000).ShouldBeTrue();
            File.Exists(Gate + ".entered").ShouldBeTrue(logger.FullLog);
            if (Reuse)
            {
                endBuild.Wait(200).ShouldBeFalse("EndBuild must wait for the retained sidecar's disposal");
            }
            else
            {
                endBuild.Wait(10_000).ShouldBeTrue("a retiring sidecar must dispose off the caller's critical path");
            }
            File.Exists(Gate + ".done").ShouldBeFalse();
            child.HasExited.ShouldBeFalse();

            if (CrashDuringCleanup)
            {
                child.Kill();
            }
            else
            {
                File.WriteAllText(Gate + ".release", "release");
                SpinWait.SpinUntil(() => File.Exists(Gate + ".done"), 10_000).ShouldBeTrue();
            }
            endBuild.Wait(10_000).ShouldBeTrue("connection loss must also release the cleanup wait");
            buildStarted = false;

            if (Reuse && !CrashDuringCleanup)
            {
                BuildResult next = manager.Build(parameters, CreateRequest(registerObject: false));
                next.OverallResult.ShouldBe(BuildResultCode.Success, logger.FullLog);
                next.ResultsByTarget["Build"].Items[0].ItemSpec.ShouldBe(child.Id.ToString(CultureInfo.InvariantCulture));
            }

            manager.ShutdownAllNodes();
            shutdownRequested = true;
            manager.Dispose();
            child.WaitForExit(10_000).ShouldBeTrue("the sidecar must still exit after asynchronous retirement");
            Log.LogMessage(MessageImportance.High, "CleanupPolicyVerified Reuse={0} Worker={1} Crash={2}", Reuse, UseWorker, CrashDuringCleanup);
            return true;
        }
        finally
        {
            File.WriteAllText(Gate + ".release", "release");
            try
            {
                if (endBuild is not null)
                {
                    endBuild.Wait(30_000).ShouldBeTrue();
                }
                else if (buildStarted)
                {
                    manager.EndBuild();
                }
                if (!shutdownRequested)
                {
                    manager.ShutdownAllNodes();
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", originalForceOutOfProc);
                if (child is not null)
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
    }

    private BuildRequestData CreateRequest(bool registerObject) => new(
        Project,
        new Dictionary<string, string?>
        {
            ["Gate"] = Gate,
            ["RegisterObject"] = registerObject.ToString()
        },
        null,
        ["Build"],
        null);
}

public sealed class RegisterGatedTaskObject : Microsoft.Build.Utilities.Task
{
    [Required]
    public string Gate { get; set; } = null!;

    public bool RegisterObject { get; set; }

    [Output]
    public int Pid { get; set; }

    public override bool Execute()
    {
        Pid = EnvironmentUtilities.CurrentProcessId;
        if (RegisterObject)
        {
#pragma warning disable CA2000 // MSBuild disposes the registered object.
            ((IBuildEngine4)BuildEngine).RegisterTaskObject(
                Gate, new GatedDisposal(Gate), RegisteredTaskObjectLifetime.Build, allowEarlyCollection: false);
#pragma warning restore CA2000
        }
        return true;
    }

    private sealed class GatedDisposal(string gate) : IDisposable
    {
        public void Dispose()
        {
            File.WriteAllText(gate + ".entered", "entered");
            SpinWait.SpinUntil(() => File.Exists(gate + ".release"), 60_000).ShouldBeTrue();
            File.WriteAllText(gate + ".done", "done");
        }
    }
}
