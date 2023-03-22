// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Logging.LiveLogger;

internal sealed class LiveLogger : INodeLogger
{
    private readonly object _lock = new();

    private readonly CancellationTokenSource _cts = new();

    private NodeStatus[] _nodes;

    private readonly Dictionary<ProjectContext, Project> _notableProjects = new();

    private readonly Dictionary<ProjectContext, (bool Notable, string Path, string Targets)> _notabilityByContext = new();

    private readonly Dictionary<ProjectInstance, ProjectContext> _relevantContextByInstance = new();

    private readonly Dictionary<ProjectContext, Stopwatch> _projectTimeCounter = new();

    private int _usedNodes = 0;

    private ProjectContext _restoreContext;

    private Thread _refresher;

    public LoggerVerbosity Verbosity { get => LoggerVerbosity.Minimal; set { } }
    public string Parameters { get => ""; set { } }

    /// <summary>
    /// List of events the logger needs as parameters to the <see cref="ConfigurableForwardingLogger"/>.
    /// </summary>
    /// <remarks>
    /// If LiveLogger runs as a distributed logger, MSBuild out-of-proc nodes might filter the events that will go to the main node using an instance of <see cref="ConfigurableForwardingLogger"/> with the following parameters.
    /// </remarks>
    public static readonly string[] ConfigurableForwardingLoggerParameters =
    {
            "BUILDSTARTEDEVENT",
            "BUILDFINISHEDEVENT",
            "PROJECTSTARTEDEVENT",
            "PROJECTFINISHEDEVENT",
            "TARGETSTARTEDEVENT",
            "TARGETFINISHEDEVENT",
            "TASKSTARTEDEVENT",
            "HIGHMESSAGEEVENT",
            "WARNINGEVENT",
            "ERROREVENT"
    };

    public void Initialize(IEventSource eventSource, int nodeCount)
    {
        _nodes = new NodeStatus[nodeCount];

        Initialize(eventSource);
    }

    public void Initialize(IEventSource eventSource)
    {
        // Debugger.Launch();

        eventSource.BuildStarted += new BuildStartedEventHandler(BuildStarted);
        eventSource.BuildFinished += new BuildFinishedEventHandler(BuildFinished);
        eventSource.ProjectStarted += new ProjectStartedEventHandler(ProjectStarted);
        eventSource.ProjectFinished += new ProjectFinishedEventHandler(ProjectFinished);
        eventSource.TargetStarted += new TargetStartedEventHandler(TargetStarted);
        eventSource.TargetFinished += new TargetFinishedEventHandler(TargetFinished);
        eventSource.TaskStarted += new TaskStartedEventHandler(TaskStarted);

        eventSource.MessageRaised += new BuildMessageEventHandler(MessageRaised);
        eventSource.WarningRaised += new BuildWarningEventHandler(WarningRaised);
        eventSource.ErrorRaised += new BuildErrorEventHandler(ErrorRaised);

        _refresher = new(ThreadProc);
        _refresher.Start();
    }

    private void ThreadProc()
    {
        while (!_cts.IsCancellationRequested)
        {
            Thread.Sleep(1_000 / 30); // poor approx of 30Hz
            lock (_lock)
            {
                EraseNodes();
                DisplayNodes();
            }
        }

        EraseNodes();
    }

    private void BuildStarted(object sender, BuildStartedEventArgs e)
    {
    }

    private void BuildFinished(object sender, BuildFinishedEventArgs e)
    {
    }

    private void ProjectStarted(object sender, ProjectStartedEventArgs e)
    {
        bool notable = IsNotableProject(e);

        ProjectContext c = new ProjectContext(e);

        if (notable)
        {
            _notableProjects[c] = new();
        }

        _projectTimeCounter[c] = Stopwatch.StartNew();

        if (e.TargetNames == "Restore")
        {
            _restoreContext = c;
            Console.WriteLine("Restoring");
            return;
        }

        _notabilityByContext[c] = (notable, e.ProjectFile, e.TargetNames);

        var key = new ProjectInstance(e);
        if (!_relevantContextByInstance.ContainsKey(key))
        {
            _relevantContextByInstance.Add(key, c);
        }
    }

    private bool IsNotableProject(ProjectStartedEventArgs e)
    {
        if (_restoreContext is not null)
        {
            return false;
        }

        return e.TargetNames switch
        {
            "" or "Restore" => true,
            "GetTargetFrameworks" or "GetTargetFrameworks" or "GetNativeManifest" or "GetCopyToOutputDirectoryItems" => false,
            _ => true,
        };
    }

    private void ProjectFinished(object sender, ProjectFinishedEventArgs e)
    {
        ProjectContext c = new(e);

        if (_restoreContext is ProjectContext restoreContext && c == restoreContext)
        {
            lock (_lock)
            {

                _restoreContext = null;

                double duration = _notableProjects[restoreContext].Stopwatch.Elapsed.TotalSeconds;

                EraseNodes();
                Console.WriteLine($"\x1b[{_usedNodes + 1}F");
                Console.Write($"\x1b[0J");
                Console.WriteLine($"Restore complete ({duration:F1}s)");
                DisplayNodes();
                return;
            }
        }

        if (_notabilityByContext[c].Notable && _relevantContextByInstance[new ProjectInstance(e)] == c)
        {
            lock (_lock)
            {
                EraseNodes();

                double duration = _notableProjects[c].Stopwatch.Elapsed.TotalSeconds;

                Console.WriteLine($"{e.ProjectFile} \x1b[1mcompleted\x1b[22m ({duration:F1}s)");
                DisplayNodes();
            }
        }
    }

    private void DisplayNodes()
    {
        lock (_lock)
        {
            int i = 0;
            foreach (NodeStatus n in _nodes)
            {
                if (n is null)
                {
                    continue;
                }
                Console.WriteLine(FitToWidth(n.ToString()));
                i++;
            }

            _usedNodes = i;
        }
    }

    private string FitToWidth(string input)
    {
        return input.Substring(0, Math.Min(input.Length, Console.BufferWidth - 1));
    }

    private void EraseNodes()
    {
        lock (_lock)
        {
            if (_usedNodes == 0)
            {
                return;
            }
            Console.WriteLine($"\x1b[{_usedNodes + 1}F");
            Console.Write($"\x1b[0J");
        }
    }

    private void TargetStarted(object sender, TargetStartedEventArgs e)
    {
        _nodes[NodeIndexForContext(e.BuildEventContext)] = new(e.ProjectFile, e.TargetName, _projectTimeCounter[new ProjectContext(e)]);
    }

    private int NodeIndexForContext(BuildEventContext context)
    {
        return context.NodeId - 1;
    }

    private void TargetFinished(object sender, TargetFinishedEventArgs e)
    {
    }

    private void TaskStarted(object sender, TaskStartedEventArgs e)
    {
        if (e.TaskName == "MSBuild")
        {
            // This will yield the node, so preemptively mark it idle
            _nodes[NodeIndexForContext(e.BuildEventContext)] = null;
        }
    }

    private void MessageRaised(object sender, BuildMessageEventArgs e)
    {
    }

    private void WarningRaised(object sender, BuildWarningEventArgs e)
    {
        throw new NotImplementedException();
    }

    private void ErrorRaised(object sender, BuildErrorEventArgs e)
    {
        throw new NotImplementedException();
    }

    public void Shutdown()
    {
        _cts.Cancel();
        _refresher?.Join();
    }
}

internal record ProjectContext(int Id)
{
    public ProjectContext(BuildEventContext context)
        : this(context.ProjectContextId)
    { }

    public ProjectContext(BuildEventArgs e)
        : this(e.BuildEventContext)
    { }
}

internal record ProjectInstance(int Id)
{
    public ProjectInstance(BuildEventContext context)
        : this(context.ProjectInstanceId)
    { }

    public ProjectInstance(BuildEventArgs e)
        : this(e.BuildEventContext)
    { }
}

internal record NodeStatus(string Project, string Target, Stopwatch Stopwatch)
{
    public override string ToString()
    {
        return $"{Project} {Target} ({Stopwatch.Elapsed.TotalSeconds:F1}s)";
    }
}

internal record ProjectReferenceUniqueness(ProjectInstance Instance, string TargetList);
