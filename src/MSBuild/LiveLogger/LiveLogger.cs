// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging.LiveLogger;

/// <summary>
/// A logger which updates the console output "live" during the build.
/// </summary>
/// <remarks>
/// Uses ANSI/VT100 control codes to erase and overwrite lines as the build is progressing.
/// </remarks>
internal sealed class LiveLogger : INodeLogger
{
    /// <summary>
    /// A wrapper over the project context ID passed to us in <see cref="IEventSource"/> logger events.
    /// </summary>
    internal record struct ProjectContext(int Id)
    {
        public ProjectContext(BuildEventContext context)
            : this(context.ProjectContextId)
        { }
    }

    /// <summary>
    /// Encapsulates the per-node data shown in live node output.
    /// </summary>
    internal record NodeStatus(string Project, string Target, Stopwatch Stopwatch)
    {
        public override string ToString()
        {
            return $"{Project} {Target} ({Stopwatch.Elapsed.TotalSeconds:F1}s)";
        }
    }

    /// <summary>
    /// Protects access to state shared between the logger callbacks and the rendering thread.
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// A cancellation token to signal the rendering thread that it should exit.
    /// </summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Tracks the work currently being done by build nodes. Null means the node is not doing any work worth reporting.
    /// </summary>
    private NodeStatus?[] _nodes = Array.Empty<NodeStatus>();

    /// <summary>
    /// Strings representing per-node console output. The output is buffered here to make the refresh loop as fast
    /// as possible and to avoid console I/O if the desired output hasn't changed.
    /// </summary>
    /// <remarks>
    /// Roman, this may need to be rethought.
    /// </remarks>
    private readonly List<string> _nodeStringBuffer = new();

    /// <summary>
    /// Tracks the status of all interesting projects seen so far.
    /// </summary>
    /// <remarks>
    /// Keyed by an ID that gets passed to logger callbacks, this allows us to quickly look up the corresponding project.
    /// A project build is deemed "notable" if its initial targets don't contain targets usually called for internal
    /// purposes, <seealso cref="IsNotableProject(ProjectStartedEventArgs)"/>.
    /// </remarks>
    private readonly Dictionary<ProjectContext, Project> _notableProjects = new();

    /// <summary>
    /// Number of live rows currently displaying node status.
    /// </summary>
    private int _usedNodes = 0;

    /// <summary>
    /// The project build context corresponding to the <c>Restore</c> initial target, or null if the build is currently
    /// bot restoring.
    /// </summary>
    private ProjectContext? _restoreContext;

    /// <summary>
    /// The thread that performs periodic refresh of the console output.
    /// </summary>
    private Thread? _refresher;

    /// <summary>
    /// The <see cref="Terminal"/> to write console output to.
    /// </summary>
    private ITerminal Terminal { get; }

    /// <inheritdoc/>
    public LoggerVerbosity Verbosity { get => LoggerVerbosity.Minimal; set { } }

    /// <inheritdoc/>
    public string Parameters { get => ""; set { } }

    /// <summary>
    /// List of events the logger needs as parameters to the <see cref="ConfigurableForwardingLogger"/>.
    /// </summary>
    /// <remarks>
    /// If LiveLogger runs as a distributed logger, MSBuild out-of-proc nodes might filter the events that will go to the main
    /// node using an instance of <see cref="ConfigurableForwardingLogger"/> with the following parameters.
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

    /// <summary>
    /// Default constructor, used by the MSBuild logger infra.
    /// </summary>
    public LiveLogger()
    {
        Terminal = new Terminal();
    }

    /// <summary>
    /// Internal constructor accepting a custom <see cref="ITerminal"/> for testing.
    /// </summary>
    internal LiveLogger(ITerminal terminal)
    {
        Terminal = terminal;
    }

    /// <inheritdoc/>
    public void Initialize(IEventSource eventSource, int nodeCount)
    {
        _nodes = new NodeStatus[nodeCount];

        Initialize(eventSource);
    }

    /// <inheritdoc/>
    public void Initialize(IEventSource eventSource)
    {
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

        _refresher = new Thread(ThreadProc);
        _refresher.Start();
    }

    /// <summary>
    /// The <see cref="_refresher"/> thread proc.
    /// </summary>
    private void ThreadProc()
    {
        while (!_cts.IsCancellationRequested)
        {
            Thread.Sleep(1_000 / 30); // poor approx of 30Hz

            lock (_lock)
            {
                if (UpdateNodeStringBuffer())
                {
                    Terminal.BeginUpdate();
                    try
                    {
                        EraseNodes();
                        DisplayNodes();
                    }
                    finally
                    {
                        Terminal.EndUpdate();
                    }
                }
            }
        }

        EraseNodes();
    }

    /// <summary>
    /// The <see cref="IEventSource.BuildStarted"/> callback. Unused.
    /// </summary>
    private void BuildStarted(object sender, BuildStartedEventArgs e)
    {
    }

    /// <summary>
    /// The <see cref="IEventSource.BuildFinished"/> callback. Unused.
    /// </summary>
    private void BuildFinished(object sender, BuildFinishedEventArgs e)
    {
    }

    /// <summary>
    /// The <see cref="IEventSource.ProjectStarted"/> callback.
    /// </summary>
    private void ProjectStarted(object sender, ProjectStartedEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is null)
        {
            return;
        }

        bool notable = IsNotableProject(e);

        ProjectContext c = new ProjectContext(buildEventContext);

        if (notable)
        {
            _notableProjects[c] = new();
        }

        if (e.TargetNames == "Restore")
        {
            _restoreContext = c;
            Terminal.WriteLine("Restoring");
            return;
        }
    }

    /// <summary>
    /// A helper to determine if a given project build is to be considered notable.
    /// </summary>
    /// <param name="e">The <see cref="ProjectStartedEventArgs"/> corresponding to the project.</param>
    /// <returns>True if the project is notable, false otherwise.</returns>
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

    /// <summary>
    /// The <see cref="IEventSource.ProjectFinished"/> callback.
    /// </summary>
    private void ProjectFinished(object sender, ProjectFinishedEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is null)
        {
            return;
        }

        ProjectContext c = new(buildEventContext);

        // First check if we're done restoring.
        if (_restoreContext is ProjectContext restoreContext && c == restoreContext)
        {
            lock (_lock)
            {
                _restoreContext = null;

                Stopwatch projectStopwatch = _notableProjects[restoreContext].Stopwatch;
                double duration = projectStopwatch.Elapsed.TotalSeconds;
                projectStopwatch.Stop();

                UpdateNodeStringBuffer();

                Terminal.BeginUpdate();
                try
                {
                    EraseNodes();
                    Terminal.WriteLine($"\x1b[{_usedNodes + 1}F");
                    Terminal.Write($"\x1b[0J");
                    Terminal.WriteLine($"Restore complete ({duration:F1}s)");
                    DisplayNodes();
                }
                finally
                {
                    Terminal.EndUpdate();
                }
                return;
            }
        }

        // If this was a notable project build, print the output path, time elapsed, and warnings/error.
        if (_notableProjects.ContainsKey(c))
        {
            lock (_lock)
            {
                UpdateNodeStringBuffer();

                Terminal.BeginUpdate();
                try
                {
                    EraseNodes();

                    Project project = _notableProjects[c];
                    double duration = project.Stopwatch.Elapsed.TotalSeconds;
                    ReadOnlyMemory<char>? outputPath = project.OutputPath;

                    if (e.ProjectFile is not null)
                    {
                        Terminal.Write(e.ProjectFile);
                        Terminal.Write(" ");
                    }
                    Terminal.WriteColor(TerminalColor.White, "completed");

                    if (outputPath is not null)
                    {
                        ReadOnlySpan<char> url = outputPath.Value.Span;
                        try
                        {
                            // If possible, make the link point to the containing directory of the output.
                            url = Path.GetDirectoryName(url);
                        }
                        catch
                        { }
                        Terminal.WriteLine($"({duration:F1}s) → \x1b]8;;{url}\x1b\\{outputPath}\x1b]8;;\x1b\\");
                    }
                    else
                    {
                        Terminal.WriteLine($"({duration:F1}s)");
                    }

                    // Print diagnostic output under the Project -> Output line.
                    if (project.BuildMessages is not null)
                    {
                        foreach (BuildMessage buildMessage in project.BuildMessages)
                        {
                            TerminalColor color = buildMessage.Severity switch
                            {
                                MessageSeverity.Warning => TerminalColor.Yellow,
                                MessageSeverity.Error => TerminalColor.Red,
                                _ => TerminalColor.Default,
                            };
                            Terminal.WriteColorLine(color, $"  {buildMessage.Message}");
                        }
                    }

                    DisplayNodes();
                }
                finally
                {
                    Terminal.EndUpdate();
                }
            }
        }
    }

    /// <summary>
    /// Update <see cref="_nodeStringBuffer"/> to match the output produced by <see cref="_nodes"/>.
    /// </summary>
    /// <returns>True if <see cref="_nodeStringBuffer"/> was actually updated, false if it's already up-to-date.</returns>
    /// <remarks>
    /// Callers may use the return value to optimize console output. If the <see cref="_nodeStringBuffer"/> printed last time
    /// is still valid, there is no need to perform console I/O.
    /// </remarks>
    private bool UpdateNodeStringBuffer()
    {
        bool stringBufferWasUpdated = false;

        int i = 0;
        foreach (NodeStatus? n in _nodes)
        {
            if (n is null)
            {
                continue;
            }
            string str = n.ToString();

            if (i < _nodeStringBuffer.Count)
            {
                if (_nodeStringBuffer[i] != str)
                {
                    _nodeStringBuffer[i] = str;
                    stringBufferWasUpdated = true;
                }
            }
            else
            {
                _nodeStringBuffer.Add(str);
                stringBufferWasUpdated = true;
            }
            i++;
        }

        if (i < _nodeStringBuffer.Count)
        {
            _nodeStringBuffer.RemoveRange(i, _nodeStringBuffer.Count - i);
            stringBufferWasUpdated = true;
        }

        return stringBufferWasUpdated;
    }

    /// <summary>
    /// Prints the live node output as contained in <see cref="_nodeStringBuffer"/>.
    /// </summary>
    private void DisplayNodes()
    {
        foreach (string str in _nodeStringBuffer)
        {
            Terminal.WriteLineFitToWidth(str);
        }
        _usedNodes = _nodeStringBuffer.Count;
    }

    /// <summary>
    /// Erases the previously printed live node output.
    /// </summary>
    private void EraseNodes()
    {
        if (_usedNodes == 0)
        {
            return;
        }
        Terminal.WriteLine($"\x1b[{_usedNodes + 1}F");
        Terminal.Write($"\x1b[0J");
    }

    /// <summary>
    /// The <see cref="IEventSource.TargetStarted"/> callback.
    /// </summary>
    private void TargetStarted(object sender, TargetStartedEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is not null && _notableProjects.TryGetValue(new ProjectContext(buildEventContext), out Project? project))
        {
            _nodes[NodeIndexForContext(buildEventContext)] = new(e.ProjectFile, e.TargetName, project.Stopwatch);
        }
    }

    /// <summary>
    /// Returns the <see cref="_nodes"/> index corresponding to the given <see cref="BuildEventContext"/>.
    /// </summary>
    private int NodeIndexForContext(BuildEventContext context)
    {
        // Node IDs reported by the build are 1-based.
        return context.NodeId - 1;
    }

    /// <summary>
    /// The <see cref="IEventSource.TargetFinished"/> callback. Unused.
    /// </summary>
    private void TargetFinished(object sender, TargetFinishedEventArgs e)
    {
    }

    /// <summary>
    /// The <see cref="IEventSource.TaskStarted"/> callback.
    /// </summary>
    private void TaskStarted(object sender, TaskStartedEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is not null && e.TaskName == "MSBuild")
        {
            // This will yield the node, so preemptively mark it idle
            _nodes[NodeIndexForContext(buildEventContext)] = null;
        }
    }

    /// <summary>
    /// The <see cref="IEventSource.MessageRaised"/> callback.
    /// </summary>
    private void MessageRaised(object sender, BuildMessageEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is null)
        {
            return;
        }

        string? message = e.Message;
        if (message is not null && e.Importance == MessageImportance.High)
        {
            // Detect project output path by matching high-importance messages against the "$(MSBuildProjectName) -> ..."
            // pattern used by the CopyFilesToOutputDirectory target.
            int index = message.IndexOf(" -> ");
            if (index > 0)
            {
                var projectFileName = Path.GetFileName(e.ProjectFile.AsSpan());
                if (!projectFileName.IsEmpty &&
                    message.AsSpan().StartsWith(Path.GetFileNameWithoutExtension(projectFileName)) &&
                    _notableProjects.TryGetValue(new ProjectContext(buildEventContext), out Project? project))
                {
                    ReadOnlyMemory<char> outputPath = e.Message.AsMemory().Slice(index + 4);
                    project.OutputPath = outputPath;
                }
            }
        }
    }

    /// <summary>
    /// The <see cref="IEventSource.WarningRaised"/> callback.
    /// </summary>
    private void WarningRaised(object sender, BuildWarningEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is not null && _notableProjects.TryGetValue(new ProjectContext(buildEventContext), out Project? project))
        {
            string message = EventArgsFormatting.FormatEventMessage(e, false);
            project.AddBuildMessage(MessageSeverity.Warning, $"⚠ {message}");
        }
    }

    /// <summary>
    /// The <see cref="IEventSource.ErrorRaised"/> callback.
    /// </summary>
    private void ErrorRaised(object sender, BuildErrorEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is not null && _notableProjects.TryGetValue(new ProjectContext(buildEventContext), out Project? project))
        {
            string message = EventArgsFormatting.FormatEventMessage(e, false);
            project.AddBuildMessage(MessageSeverity.Error, $"❌ {message}");
        }
    }

    /// <inheritdoc/>
    public void Shutdown()
    {
        _cts.Cancel();
        _refresher?.Join();

        Terminal.Dispose();
    }
}
