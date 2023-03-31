// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging.LiveLogger;

internal sealed class LiveLogger : INodeLogger
{
    private readonly object _lock = new();

    private readonly CancellationTokenSource _cts = new();

    private NodeStatus?[] _nodes = Array.Empty<NodeStatus>();

    private readonly Dictionary<ProjectContext, Project> _notableProjects = new();

    private readonly Dictionary<ProjectContext, (bool Notable, string? Path, string? Targets)> _notabilityByContext = new();

    private readonly Dictionary<ProjectInstance, ProjectContext> _relevantContextByInstance = new();

    private readonly Dictionary<ProjectContext, Stopwatch> _projectTimeCounter = new();

    private ProjectContext? _restoreContext;

    private Thread? _refresher;

    private NodesFrame _currentFrame = new(Array.Empty<NodeStatus>());

    private Encoding? _originalOutputEncoding;

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

        _originalOutputEncoding = Console.OutputEncoding;
        Console.OutputEncoding = Encoding.UTF8;

        _refresher = new Thread(ThreadProc);
        _refresher.Start();
    }

    private void ThreadProc()
    {
        while (!_cts.IsCancellationRequested)
        {
            Thread.Sleep(1_000 / 30); // poor approx of 30Hz

            lock (_lock)
            {
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

        _projectTimeCounter[c] = Stopwatch.StartNew();

        if (e.TargetNames == "Restore")
        {
            _restoreContext = c;
            Console.WriteLine("Restoring");
            return;
        }

        _notabilityByContext[c] = (notable, e.ProjectFile, e.TargetNames);

        var key = new ProjectInstance(buildEventContext);
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
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is null)
        {
            return;
        }

        ProjectContext c = new(buildEventContext);

        if (_restoreContext is ProjectContext restoreContext && c == restoreContext)
        {
            lock (_lock)
            {
                _restoreContext = null;

                double duration = _notableProjects[restoreContext].Stopwatch.Elapsed.TotalSeconds;

                EraseNodes();
                Console.WriteLine($"Restore complete ({duration:F1}s)");
                DisplayNodes();
                return;
            }
        }

        if (_notabilityByContext[c].Notable && _relevantContextByInstance[new ProjectInstance(buildEventContext)] == c)
        {
            lock (_lock)
            {
                EraseNodes();

                Project project = _notableProjects[c];
                double duration = project.Stopwatch.Elapsed.TotalSeconds;
                ReadOnlyMemory<char>? outputPath = project.OutputPath;

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
                    Console.WriteLine($"{e.ProjectFile} \x1b[1mcompleted\x1b[22m ({duration:F1}s) → \x1b]8;;{url}\x1b\\{outputPath}\x1b]8;;\x1b\\");
                }
                else
                {
                    Console.WriteLine($"{e.ProjectFile} \x1b[1mcompleted\x1b[22m ({duration:F1}s)");
                }

                // Print diagnostic output under the Project -> Output line.
                if (project.BuildMessages is not null)
                {
                    foreach (string message in project.BuildMessages)
                    {
                        Console.WriteLine(message);
                    }
                }

                DisplayNodes();
            }
        }
    }

    private void DisplayNodes()
    {
        NodesFrame newFrame = new NodesFrame(_nodes);
        string rendered = newFrame.Render(_currentFrame);

        // Move cursor back to 1st line of nodes
        Console.WriteLine($"\x1b[{_currentFrame.NodesCount + 1}F");
        Console.Write(rendered);

        _currentFrame = newFrame;
    }

    private void EraseNodes()
    {
        if (_currentFrame.NodesCount == 0)
        {
            return;
        }
        Console.WriteLine($"\x1b[{_currentFrame.NodesCount + 1}F");
        Console.Write($"\x1b[0J");
        _currentFrame.Clear();
    }

    private void TargetStarted(object sender, TargetStartedEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is not null)
        {
            _nodes[NodeIndexForContext(buildEventContext)] = new(e.ProjectFile, e.TargetName, _projectTimeCounter[new ProjectContext(buildEventContext)]);
        }
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
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is not null && e.TaskName == "MSBuild")
        {
            // This will yield the node, so preemptively mark it idle
            _nodes[NodeIndexForContext(buildEventContext)] = null;
        }
    }

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

    private void WarningRaised(object sender, BuildWarningEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is not null && _notableProjects.TryGetValue(new ProjectContext(buildEventContext), out Project? project))
        {
            string message = EventArgsFormatting.FormatEventMessage(e, false);
            project.AddBuildMessage($"  \x1b[33;1m⚠ {message}\x1b[m");
        }
    }

    private void ErrorRaised(object sender, BuildErrorEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is not null && _notableProjects.TryGetValue(new ProjectContext(buildEventContext), out Project? project))
        {
            string message = EventArgsFormatting.FormatEventMessage(e, false);
            project.AddBuildMessage($"  \x1b[31;1m❌ {message}\x1b[m");
        }
    }

    public void Shutdown()
    {
        _cts.Cancel();
        _refresher?.Join();

        if (_originalOutputEncoding is not null)
        {
            Console.OutputEncoding = _originalOutputEncoding;
        }
    }

    /// <summary>
    /// Capture states on nodes to be rendered on display.
    /// </summary>
    private class NodesFrame
    {
        private readonly List<string> _nodeStrings = new();
        private StringBuilder _renderBuilder = new();

        public int NodesCount { get; private set; }

        public NodesFrame(NodeStatus?[] nodes)
        {
            Init(nodes);
        }

        public string NodeString(int index)
        {
            if (index >= NodesCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _nodeStrings[index];
        }

        private void Init(NodeStatus?[] nodes)
        {
            int i = 0;
            foreach (NodeStatus? n in nodes)
            {
                if (n is null)
                {
                    continue;
                }
                string str = n.ToString();

                if (i < _nodeStrings.Count)
                {
                    _nodeStrings[i] = str;
                }
                else
                {
                    _nodeStrings.Add(str);
                }
                i++;
            }

            NodesCount = i;
        }

        private ReadOnlySpan<char> FitToWidth(ReadOnlySpan<char> input)
        {
            return input.Slice(0, Math.Min(input.Length, Console.BufferWidth - 1));
        }

        /// <summary>
        /// Render VT100 string to update current to next frame.
        /// </summary>
        public string Render(NodesFrame previousFrame)
        {
            StringBuilder sb = _renderBuilder;
            sb.Clear();

            int i = 0;
            for (; i < NodesCount; i++)
            {
                var needed = FitToWidth(this.NodeString(i));

                // Do we have previous node string to compare with?
                if (previousFrame.NodesCount > i)
                {
                    var previous = FitToWidth(previousFrame.NodeString(i));

                    if (!previous.SequenceEqual(needed))
                    {
                        int commonPrefixLen = previous.CommonPrefixLength(needed);
                        if (commonPrefixLen == 0)
                        {
                            // whole string
                            sb.Append(needed);
                        }
                        else
                        {
                            // set cursor to different char
                            sb.Append($"\x1b[{commonPrefixLen}C");
                            sb.Append(needed.Slice(commonPrefixLen));
                            // Shall we clear rest of line
                            if (needed.Length < previous.Length)
                            {
                                sb.Append($"\x1b[K");
                            }
                        }
                    }
                }
                else
                {
                    // From now on we have to simply WriteLine
                    sb.Append(needed);
                }

                // Next line
                sb.AppendLine();
            }

            // clear no longer used lines
            if (i < previousFrame.NodesCount)
            {
                sb.Append($"\x1b[0J");
            }

            return sb.ToString();
        }

        public void Clear()
        {
            NodesCount = 0;
        }
    }
}

internal record ProjectContext(int Id)
{
    public ProjectContext(BuildEventContext context)
        : this(context.ProjectContextId)
    { }
}

internal record ProjectInstance(int Id)
{
    public ProjectInstance(BuildEventContext context)
        : this(context.ProjectInstanceId)
    { }
}

internal record NodeStatus(string Project, string Target, Stopwatch Stopwatch)
{
    public override string ToString()
    {
        return $"{Project} {Target} ({Stopwatch.Elapsed.TotalSeconds:F1}s)";
    }
}
