// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging;

internal sealed class BuildEventTracker
{
    internal readonly record struct BuildStartedSnapshot(DateTime Timestamp);

    internal readonly record struct BuildFinishedSnapshot(DateTime Timestamp, TimeSpan Duration, bool Succeeded);

    internal readonly record struct ProjectStartedSnapshot(
        int ProjectContextId,
        int NodeId,
        int EvaluationId,
        string? ProjectFile,
        string? TargetNames,
        string? EvaluationProjectFile,
        string? TargetFramework,
        string? RuntimeIdentifier);

    private IEventSource? _eventSource;

    internal event Action<BuildStartedSnapshot>? BuildStartedTracked;

    internal event Action<BuildFinishedSnapshot>? BuildFinishedTracked;

    internal event Action<ProjectStartedSnapshot>? ProjectStartedTracked;

    internal event Action<ProjectFinishedEventArgs>? ProjectFinishedTracked;

    internal event Action<TargetStartedEventArgs>? TargetStartedTracked;

    internal event Action<TargetFinishedEventArgs>? TargetFinishedTracked;

    internal event Action<TaskStartedEventArgs>? TaskStartedTracked;

    internal event Action<TaskFinishedEventArgs>? TaskFinishedTracked;

    internal event Action<BuildStatusEventArgs>? StatusEventTracked;

    internal event Action<BuildMessageEventArgs>? MessageTracked;

    internal event Action<BuildWarningEventArgs>? WarningTracked;

    internal event Action<BuildErrorEventArgs>? ErrorTracked;

    private DateTime _buildStartTime;

    internal DateTime BuildStartTime { get; private set; }

    /// <summary>
    /// A wrapper over the project context ID passed to us in <see cref="IEventSource"/> logger events.
    /// </summary>
    internal record struct ProjectContext(int Id)
    {
        public ProjectContext(BuildEventContext context)
            : this(context.ProjectContextId)
        {
        }
    }

    /// <summary>
    /// A wrapper over the evaluation context ID passed to us in <see cref="IEventSource"/> logger events.
    /// </summary>
    internal record struct EvalContext(int Id)
    {
        public EvalContext(BuildEventContext context)
            : this(context.EvaluationId)
        {
        }
    }

    /// <summary>
    /// Tracks the status of all relevant projects seen so far.
    /// </summary>
    /// <remarks>
    /// Keyed by an ID that gets passed to logger callbacks, this allows us to quickly look up the corresponding project.
    /// </remarks>
    private readonly Dictionary<ProjectContext, TerminalProjectInfo> _projects = [];

    private readonly Dictionary<EvalContext, EvalProjectInfo> _projectEvaluations = [];

    /// <summary>
    /// Tracks the work currently being done by build nodes. Null means the node is not doing any work worth reporting.
    /// </summary>
    /// <remarks>
    /// There is no locking around access to this data structure despite it being accessed concurrently by multiple threads.
    /// However, reads and writes to locations in an array is atomic, so locking is not required.
    /// </remarks>
    private TerminalNodeStatus?[] _nodes = Array.Empty<TerminalNodeStatus>();

    public void Attach(IEventSource eventSource)
    {
        ArgumentNullException.ThrowIfNull(eventSource);

        _eventSource = eventSource;

        eventSource.BuildStarted += OnBuildStarted;
        eventSource.BuildFinished += OnBuildFinished;
        eventSource.ProjectStarted += OnProjectStarted;
        eventSource.ProjectFinished += OnProjectFinished;
        eventSource.TargetStarted += OnTargetStarted;
        eventSource.TargetFinished += OnTargetFinished;
        eventSource.TaskStarted += OnTaskStarted;
        eventSource.TaskFinished += OnTaskFinished;
        eventSource.StatusEventRaised += OnStatusEventRaised;
        eventSource.MessageRaised += OnMessageRaised;
        eventSource.WarningRaised += OnWarningRaised;
        eventSource.ErrorRaised += OnErrorRaised;
    }

    internal void Detach()
    {
        if (_eventSource is not null)
        {
            _eventSource.BuildStarted -= OnBuildStarted;
            _eventSource.BuildFinished -= OnBuildFinished;
            _eventSource.ProjectStarted -= OnProjectStarted;
            _eventSource.ProjectFinished -= OnProjectFinished;
            _eventSource.TargetStarted -= OnTargetStarted;
            _eventSource.TargetFinished -= OnTargetFinished;
            _eventSource.TaskStarted -= OnTaskStarted;
            _eventSource.TaskFinished -= OnTaskFinished;
            _eventSource.StatusEventRaised -= OnStatusEventRaised;
            _eventSource.MessageRaised -= OnMessageRaised;
            _eventSource.WarningRaised -= OnWarningRaised;
            _eventSource.ErrorRaised -= OnErrorRaised;
            _eventSource = null;
        }
    }

    private void OnBuildStarted(object sender, BuildStartedEventArgs e)
    {
        BuildStartTime = e.Timestamp;
        BuildStartedTracked?.Invoke(new BuildStartedSnapshot(e.Timestamp));
    }

    private void OnBuildFinished(object sender, BuildFinishedEventArgs e)
    {
        BuildFinishedTracked?.Invoke(new BuildFinishedSnapshot(
            e.Timestamp,
            e.Timestamp - _buildStartTime,
            e.Succeeded));
    }

    private void OnProjectStarted(object sender, ProjectStartedEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is null)
        {
            return;
        }

        _projectEvaluations.TryGetValue(new EvalContext(buildEventContext), out EvalProjectInfo evalInfo);

        ProjectStartedTracked?.Invoke(new ProjectStartedSnapshot(
            buildEventContext.ProjectContextId,
            buildEventContext.NodeId,
            buildEventContext.EvaluationId,
            e.ProjectFile,
            e.TargetNames,
            evalInfo.ProjectFile,
            evalInfo.TargetFramework,
            evalInfo.RuntimeIdentifier));
    }

    private void OnProjectFinished(object sender, ProjectFinishedEventArgs e)
    {
        ProjectFinishedTracked?.Invoke(e);
    }

    private void OnTargetStarted(object sender, TargetStartedEventArgs e)
    {
        TargetStartedTracked?.Invoke(e);
    }

    private void OnTargetFinished(object sender, TargetFinishedEventArgs e)
    {
        TargetFinishedTracked?.Invoke(e);
    }

    private void OnTaskStarted(object sender, TaskStartedEventArgs e)
    {
        TaskStartedTracked?.Invoke(e);
    }

    private void OnTaskFinished(object sender, TaskFinishedEventArgs e)
    {
        TaskFinishedTracked?.Invoke(e);
    }

    private void OnStatusEventRaised(object sender, BuildStatusEventArgs e)
    {
        if (e is ProjectEvaluationFinishedEventArgs evalFinish)
        {
            CaptureEvalContext(evalFinish);
        }

        StatusEventTracked?.Invoke(e);
    }

    private void OnMessageRaised(object sender, BuildMessageEventArgs e)
    {
        MessageTracked?.Invoke(e);
    }

    private void OnWarningRaised(object sender, BuildWarningEventArgs e)
    {
        WarningTracked?.Invoke(e);
    }

    private void OnErrorRaised(object sender, BuildErrorEventArgs e)
    {
        ErrorTracked?.Invoke(e);
    }

    public void CaptureEvalContext(ProjectEvaluationFinishedEventArgs evalFinish)
    {
        var buildEventContext = evalFinish.BuildEventContext;
        if (buildEventContext is null)
        {
            return;
        }

        EvalContext c = new(buildEventContext);

        if (!_projectEvaluations.TryGetValue(c, out EvalProjectInfo _))
        {
            string? tfm = null;
            string? rid = null;
            foreach (var property in evalFinish.EnumerateProperties())
            {
                if (tfm is not null && rid is not null)
                {
                    // We already have both properties, no need to continue.
                    break;
                }
                switch (property.Name)
                {
                    case "TargetFramework":
                        tfm = property.Value;
                        break;
                    case "RuntimeIdentifier":
                        rid = property.Value;
                        break;
                }
            }
            var evalInfo = new EvalProjectInfo(new TerminalLogger.EvalContext(buildEventContext), evalFinish.ProjectFile, tfm, rid);
            _projectEvaluations[c] = evalInfo;
        }
    }
}

//  BuildEventTracker 
// Subscribe to events; correlate evaluation/project/target/task contexts; maintain lifecycle state; 
// expose normalized callbacks/snapshots

// TerminalLogger
// Render live node progress, ANSI output, hyperlinks, restore/test summaries, terminal-width behavior     


// Subscribe to events

//  TerminalLogger.Initialize  directly registers its handlers:

//  TerminalLogger.cs:457-468 

// eventSource.BuildStarted += BuildStarted;
// eventSource.BuildFinished += BuildFinished;
// eventSource.ProjectStarted += ProjectStarted;
// eventSource.ProjectFinished += ProjectFinished;
// eventSource.TargetStarted += TargetStarted;
// eventSource.TargetFinished += TargetFinished;
// eventSource.TaskStarted += TaskStarted;
// eventSource.TaskFinished += TaskFinished;
// eventSource.StatusEventRaised += StatusEventRaised;
// eventSource.MessageRaised += MessageRaised;
// eventSource.WarningRaised += WarningRaised;
// eventSource.ErrorRaised += ErrorRaised;

// This subscription logic could move into  BuildEventTracker.Attach(IEventSource) .





// Correlate contexts

// Context keys are defined at:

//  TerminalLogger.cs:57-73 

// internal record struct ProjectContext(int Id);
// internal record struct EvalContext(int Id);

// Tracked state is stored at:

//  TerminalLogger.cs:113-124 

// private readonly Dictionary<ProjectContext, TerminalProjectInfo> _projects = [];
// private readonly Dictionary<EvalContext, EvalProjectInfo> _projectEvaluations = [];
// private TerminalNodeStatus?[] _nodes = [];

// Evaluation-to-project correlation happens through:

// •  CaptureEvalContext : lines  904-937 
// •  ProjectStarted : lines  742-780 

// The evaluation is first stored by  EvaluationId , then retrieved when a project with the corresponding evaluation starts.

// Project-scoped events repeatedly resolve their project through:

// _projects.TryGetValue(
//     new ProjectContext(buildEventContext),
//     out TerminalProjectInfo? project);

// This occurs in target, task, message, warning, and error handlers.

// The current target correlation is limited:  TargetStarted  stores the target name in  project.CurrentTarget . There is not yet a general target/task model keyed by  TargetId  and  TaskId .








// Maintain lifecycle state

// Project creation and timing:

//  TerminalLogger.cs:742-780 

// TerminalProjectInfo projectInfo = new(c, evalInfo, ...);
// _projects[c] = projectInfo;

// Project completion:

//  TerminalLogger.cs:786-805 

// project.Succeeded = e.Succeeded;
// project.Stopwatch.Stop();

// Target state:

//  TerminalLogger.cs:1063-1092 

// project.Stopwatch.Start();
// project.CurrentTarget = targetName;
// UpdateNodeStatus(buildEventContext, nodeStatus);

// Task yielding/resuming is handled at lines  1167-1197 . In particular, the  MSBuild  task temporarily stops project timing and marks the node idle.

// Diagnostic state is maintained by  TerminalProjectInfo :

//  TerminalProjectInfo.cs:91-150 

// public string? CurrentTarget { get; set; }
// public bool Succeeded { get; set; }
// public int ErrorCount { get; private set; }
// public int WarningCount { get; private set; }
// public IReadOnlyList<TerminalBuildMessage>? BuildMessages => _buildMessages;

// public void AddBuildMessage(...)

// Warnings and errors are associated with projects in:

// •  WarningRaised :  TerminalLogger.cs:1363-1394 
// •  ErrorRaised :  TerminalLogger.cs:1452-1468 

// Expose normalized callbacks or snapshots

// This functionality does not currently exist.

//  TerminalLogger  updates its private state and immediately renders from the same handlers. For example,  ProjectFinished  both completes project state and writes terminal output.

// The extraction would need to introduce something like:

// tracker.ProjectStarted += OnProjectStarted;
// tracker.ProjectUpdated += OnProjectUpdated;
// tracker.DiagnosticRaised += OnDiagnosticRaised;
// tracker.ProjectFinished += OnProjectFinished;

// with presentation-neutral models such as:

// ProjectSnapshot
// DiagnosticSnapshot
// TargetSnapshot
// TaskSnapshot

// That is the main new abstraction: converting TerminalLogger’s private mutable state into read-only information consumable by both  TerminalLogger  and  CiLoggerBase .