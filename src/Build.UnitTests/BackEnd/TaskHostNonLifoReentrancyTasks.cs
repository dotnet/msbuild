// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
#if NETFRAMEWORK
using Path = Microsoft.IO.Path;
#endif

#nullable enable

namespace Microsoft.Build.Engine.UnitTests.BackEnd;

// Neither this base nor the nested-build task is marked: -mt must route the latter to a TaskHost.
public abstract class TaskHostNonLifoTracedTask : Microsoft.Build.Utilities.Task
{
    private string _trace = string.Empty;

    [Required]
    public string RunDir { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    protected void Begin()
    {
        if (!Path.IsPathFullyQualified(RunDir))
        {
            throw new ArgumentException("RunDir must be absolute.");
        }

        Directory.CreateDirectory(RunDir);
        _trace = Path.Combine(RunDir, $"{Role}-{EnvironmentUtilities.CurrentProcessId}-{Guid.NewGuid():N}.trace");
        Record("START");
        Record($"RUNTIME corelib={typeof(object).Assembly.GetName().Name}");
    }

    protected void Record(string stage)
    {
        // Sender-side evidence survives even when the task's logging packets are routed incorrectly.
        File.AppendAllText(_trace,
            $"{DateTime.UtcNow:O} role={Role} pid={EnvironmentUtilities.CurrentProcessId} tid={Environment.CurrentManagedThreadId} stage={stage}{Environment.NewLine}");
    }

    protected void Message(string stage)
    {
        Record(stage);
        Log.LogMessage(MessageImportance.High,
            $"PROBE role={Role} pid={EnvironmentUtilities.CurrentProcessId} tid={Environment.CurrentManagedThreadId} stage={stage}");
    }
}

public sealed class TaskHostNonLifoNestedBuild : TaskHostNonLifoTracedTask
{
    [Required]
    public string ChildProject { get; set; } = string.Empty;

    public string ChildTarget { get; set; } = "Build";

    [Output]
    public string CompletedRole { get; set; } = string.Empty;

    [Output]
    public int ProcessId { get; set; }

    public override bool Execute()
    {
        Begin();
        if (!Path.IsPathFullyQualified(ChildProject))
        {
            throw new ArgumentException("ChildProject must be absolute.");
        }

        Message($"CALL_CHILD project={ChildProject} target={ChildTarget}");
        bool success = BuildEngine.BuildProjectFile(ChildProject, [ChildTarget], new Hashtable(), new Hashtable());
        Message($"CHILD_RETURN success={success}");
        if (success)
        {
            CompletedRole = Role;
            ProcessId = EnvironmentUtilities.CurrentProcessId;
        }

        Record($"EXECUTE_RETURN success={success}");
        return success;
    }
}

[MSBuildMultiThreadableTask]
public sealed class TaskHostNonLifoFileGate : TaskHostNonLifoTracedTask
{
    public string Signal { get; set; } = string.Empty;

    public string WaitFor { get; set; } = string.Empty;

    public override bool Execute()
    {
        Begin();
        if ((Signal.Length != 0 && !Path.IsPathFullyQualified(Signal)) ||
            (WaitFor.Length != 0 && !Path.IsPathFullyQualified(WaitFor)))
        {
            throw new ArgumentException("Gate paths must be absolute.");
        }

        Message("YIELD_BEGIN");
        var engine = (IBuildEngine3)BuildEngine;
        engine.Yield();
        bool opened = false;
        try
        {
            Record("YIELDED");
            if (Signal.Length != 0)
            {
                File.WriteAllText(Signal, $"{Role} {EnvironmentUtilities.CurrentProcessId}");
                Record($"SIGNAL path={Signal}");
            }

            // Time only bounds failure. A waiting gate succeeds exclusively on file existence.
            opened = WaitFor.Length == 0 || SpinWait.SpinUntil(() => File.Exists(WaitFor), TimeSpan.FromSeconds(75));
            Record(opened ? "GATE_OPEN" : "GATE_TIMEOUT");
        }
        finally
        {
            Record("REACQUIRE_BEGIN");
            engine.Reacquire();
            Record("REACQUIRED");
        }

        if (!opened)
        {
            Log.LogError($"Gate {Role} timed out waiting for {WaitFor}");
        }

        Message($"EXECUTE_RETURN success={opened}");
        return opened;
    }
}
