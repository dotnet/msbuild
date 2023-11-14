// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.Text.RegularExpressions;
#if NET7_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
#if NETFRAMEWORK
using Microsoft.IO;
#else
using System.IO;
#endif

namespace Microsoft.Build.Logging.TerminalLogger;

/// <summary>
/// A logger which updates the console output "live" during the build.
/// </summary>
/// <remarks>
/// Uses ANSI/VT100 control codes to erase and overwrite lines as the build is progressing.
/// </remarks>
internal sealed partial class TerminalLogger : INodeLogger
{
    private const string FilePathPattern = " -> ";

#if NET7_0_OR_GREATER
    [StringSyntax(StringSyntaxAttribute.Regex)]
    private const string ImmediateMessagePattern = @"\[CredentialProvider\]|--interactive";
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    [GeneratedRegex(ImmediateMessagePattern, Options)]
    private static partial Regex ImmediateMessageRegex();
#else
    private static readonly string[] _immediateMessageKeywords = { "[CredentialProvider]", "--interactive" };
#endif

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
    internal record NodeStatus(string Project, string? TargetFramework, string Target, Stopwatch Stopwatch)
    {
        public override string ToString()
        {
            string duration = Stopwatch.Elapsed.TotalSeconds.ToString("F1");

            return string.IsNullOrEmpty(TargetFramework)
                ? ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ProjectBuilding_NoTF",
                    Indentation,
                    Project,
                    Target,
                    duration)
                : ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ProjectBuilding_WithTF",
                    Indentation,
                    Project,
                    AnsiCodes.Colorize(TargetFramework, TargetFrameworkColor),
                    Target,
                    duration);
        }
    }

    /// <summary>
    /// The indentation to use for all build output.
    /// </summary>
    private const string Indentation = "  ";

    private const TerminalColor TargetFrameworkColor = TerminalColor.Cyan;

    /// <summary>
    /// Protects access to state shared between the logger callbacks and the rendering thread.
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// A cancellation token to signal the rendering thread that it should exit.
    /// </summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Tracks the status of all relevant projects seen so far.
    /// </summary>
    /// <remarks>
    /// Keyed by an ID that gets passed to logger callbacks, this allows us to quickly look up the corresponding project.
    /// </remarks>
    private readonly Dictionary<ProjectContext, Project> _projects = new();

    /// <summary>
    /// Tracks the work currently being done by build nodes. Null means the node is not doing any work worth reporting.
    /// </summary>
    private NodeStatus?[] _nodes = Array.Empty<NodeStatus>();

    /// <summary>
    /// The timestamp of the <see cref="IEventSource.BuildStarted"/> event.
    /// </summary>
    private DateTime _buildStartTime;

    /// <summary>
    /// The working directory when the build starts, to trim relative output paths.
    /// </summary>
    private readonly string _initialWorkingDirectory = Environment.CurrentDirectory;

    /// <summary>
    /// True if the build has encountered at least one error.
    /// </summary>
    private bool _buildHasErrors;

    /// <summary>
    /// True if the build has encountered at least one warning.
    /// </summary>
    private bool _buildHasWarnings;

    /// <summary>
    /// True if restore failed and this failure has already been reported.
    /// </summary>
    private bool _restoreFailed;

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
    /// What is currently displaying in Nodes section as strings representing per-node console output.
    /// </summary>
    private NodesFrame _currentFrame = new(Array.Empty<NodeStatus>(), 0, 0);

    /// <summary>
    /// The <see cref="Terminal"/> to write console output to.
    /// </summary>
    private ITerminal Terminal { get; }

    /// <summary>
    /// Should the logger's test environment refresh the console output manually instead of using a background thread?
    /// </summary>
    private bool _manualRefresh;

    /// <summary>
    /// List of events the logger needs as parameters to the <see cref="ConfigurableForwardingLogger"/>.
    /// </summary>
    /// <remarks>
    /// If TerminalLogger runs as a distributed logger, MSBuild out-of-proc nodes might filter the events that will go to the main
    /// node using an instance of <see cref="ConfigurableForwardingLogger"/> with the following parameters.
    /// Important: Note that TerminalLogger is special-cased in <see cref="BackEnd.Logging.LoggingService.UpdateMinimumMessageImportance"/>
    /// so changing this list may impact the minimum message importance logging optimization.
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
    /// The two directory separator characters to be passed to methods like <see cref="String.IndexOfAny(char[])"/>.
    /// </summary>
    private static readonly char[] PathSeparators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

    /// <summary>
    /// Default constructor, used by the MSBuild logger infra.
    /// </summary>
    public TerminalLogger()
    {
        Terminal = new Terminal();
    }

    /// <summary>
    /// Internal constructor accepting a custom <see cref="ITerminal"/> for testing.
    /// </summary>
    internal TerminalLogger(ITerminal terminal)
    {
        Terminal = terminal;
        _manualRefresh = true;
    }

    #region INodeLogger implementation

    /// <inheritdoc/>
    public LoggerVerbosity Verbosity { get => LoggerVerbosity.Minimal; set { } }

    /// <inheritdoc/>
    public string Parameters
    {
        get => ""; set { }
    }

    /// <inheritdoc/>
    public void Initialize(IEventSource eventSource, int nodeCount)
    {
        // When MSBUILDNOINPROCNODE enabled, NodeId's reported by build start with 2. We need to reserve an extra spot for this case. 
        _nodes = new NodeStatus[nodeCount + 1];

        Initialize(eventSource);
    }

    /// <inheritdoc/>
    public void Initialize(IEventSource eventSource)
    {
        eventSource.BuildStarted += BuildStarted;
        eventSource.BuildFinished += BuildFinished;
        eventSource.ProjectStarted += ProjectStarted;
        eventSource.ProjectFinished += ProjectFinished;
        eventSource.TargetStarted += TargetStarted;
        eventSource.TargetFinished += TargetFinished;
        eventSource.TaskStarted += TaskStarted;

        eventSource.MessageRaised += MessageRaised;
        eventSource.WarningRaised += WarningRaised;
        eventSource.ErrorRaised += ErrorRaised;

        if (eventSource is IEventSource4 eventSource4)
        {
            eventSource4.IncludeEvaluationPropertiesAndItems();
        }
    }

    /// <inheritdoc/>
    public void Shutdown()
    {
        Terminal.Dispose();
    }

    #endregion

    #region Logger callbacks

    /// <summary>
    /// The <see cref="IEventSource.BuildStarted"/> callback.
    /// </summary>
    private void BuildStarted(object sender, BuildStartedEventArgs e)
    {
        if (!_manualRefresh)
        {
            _refresher = new Thread(ThreadProc);
            _refresher.Start();
        }

        _buildStartTime = e.Timestamp;

        if (Terminal.SupportsProgressReporting)
        {
            Terminal.Write(AnsiCodes.SetProgressIndeterminate);
        }
    }

    /// <summary>
    /// The <see cref="IEventSource.BuildFinished"/> callback.
    /// </summary>
    private void BuildFinished(object sender, BuildFinishedEventArgs e)
    {
        _cts.Cancel();
        _refresher?.Join();

        _projects.Clear();

        Terminal.BeginUpdate();
        try
        {
            string duration = (e.Timestamp - _buildStartTime).TotalSeconds.ToString("F1");
            string buildResult = RenderBuildResult(e.Succeeded, _buildHasErrors, _buildHasWarnings);

            Terminal.WriteLine("");
            if (_restoreFailed)
            {
                Terminal.WriteLine(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("RestoreCompleteWithMessage",
                    buildResult,
                    duration));
            }
            else
            {
                Terminal.WriteLine(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("BuildFinished",
                    buildResult,
                    duration));
            }
        }
        finally
        {
            if (Terminal.SupportsProgressReporting)
            {
                Terminal.Write(AnsiCodes.RemoveProgress);
            }

            Terminal.EndUpdate();
        }

        _buildHasErrors = false;
        _buildHasWarnings = false;
        _restoreFailed = false;
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

        ProjectContext c = new ProjectContext(buildEventContext);

        if (_restoreContext is null)
        {
            if (e.GlobalProperties?.TryGetValue("TargetFramework", out string? targetFramework) != true)
            {
                targetFramework = null;
            }
            _projects[c] = new(targetFramework);
        }

        if (e.TargetNames == "Restore")
        {
            _restoreContext = c;
            _nodes[0] = new NodeStatus(e.ProjectFile!, null, "Restore", _projects[c].Stopwatch);
        }
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

        // Mark node idle until something uses it again
        if (_restoreContext is null)
        {
            UpdateNodeStatus(buildEventContext, null);
        }

        ProjectContext c = new(buildEventContext);

        if (_projects.TryGetValue(c, out Project? project))
        {
            lock (_lock)
            {
                Terminal.BeginUpdate();
                try
                {
                    EraseNodes();

                    string duration = project.Stopwatch.Elapsed.TotalSeconds.ToString("F1");
                    ReadOnlyMemory<char>? outputPath = project.OutputPath;

                    string projectFile = e.ProjectFile is not null ?
                        Path.GetFileNameWithoutExtension(e.ProjectFile) :
                        string.Empty;

                    // Build result. One of 'failed', 'succeeded with warnings', or 'succeeded' depending on the build result and diagnostic messages
                    // reported during build.
                    bool haveErrors = project.BuildMessages?.Exists(m => m.Severity == MessageSeverity.Error) == true;
                    bool haveWarnings = project.BuildMessages?.Exists(m => m.Severity == MessageSeverity.Warning) == true;

                    string buildResult = RenderBuildResult(e.Succeeded, haveErrors, haveWarnings);

                    // Check if we're done restoring.
                    if (c == _restoreContext)
                    {
                        if (e.Succeeded)
                        {
                            if (haveErrors || haveWarnings)
                            {
                                Terminal.WriteLine(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("RestoreCompleteWithMessage",
                                    buildResult,
                                    duration));
                            }
                            else
                            {
                                Terminal.WriteLine(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("RestoreComplete",
                                    duration));
                            }
                        }
                        else
                        {
                            // It will be reported after build finishes.
                            _restoreFailed = true;
                        }

                        _restoreContext = null;
                    }
                    // If this was a notable project build, we print it as completed only if it's produced an output or warnings/error.
                    else if (project.OutputPath is not null || project.BuildMessages is not null)
                    {
                        // Show project build complete and its output

                        if (string.IsNullOrEmpty(project.TargetFramework))
                        {
                            Terminal.Write(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ProjectFinished_NoTF",
                                Indentation,
                                projectFile,
                                buildResult,
                                duration));
                        }
                        else
                        {
                            Terminal.Write(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ProjectFinished_WithTF",
                                Indentation,
                                projectFile,
                                AnsiCodes.Colorize(project.TargetFramework, TargetFrameworkColor),
                                buildResult,
                                duration));
                        }

                        // Print the output path as a link if we have it.
                        if (outputPath is not null)
                        {
                            ReadOnlySpan<char> outputPathSpan = outputPath.Value.Span;
                            ReadOnlySpan<char> url = outputPathSpan;
                            try
                            {
                                // If possible, make the link point to the containing directory of the output.
                                url = Path.GetDirectoryName(url);
                            }
                            catch
                            {
                                // Ignore any GetDirectoryName exceptions.
                            }

                            // Generates file:// schema url string which is better handled by various Terminal clients than raw folder name.
                            string urlString = url.ToString();
                            if (Uri.TryCreate(urlString, UriKind.Absolute, out Uri? uri))
                            {
                                urlString = uri.AbsoluteUri;
                            }

                            // If the output path is under the initial working directory, make the console output relative to that to save space.
                            if (outputPathSpan.StartsWith(_initialWorkingDirectory.AsSpan(), FileUtilities.PathComparison))
                            {
                                if (outputPathSpan.Length > _initialWorkingDirectory.Length
                                    && (outputPathSpan[_initialWorkingDirectory.Length] == Path.DirectorySeparatorChar
                                        || outputPathSpan[_initialWorkingDirectory.Length] == Path.AltDirectorySeparatorChar))
                                {
                                    outputPathSpan = outputPathSpan.Slice(_initialWorkingDirectory.Length + 1);
                                }
                            }

                            Terminal.WriteLine(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ProjectFinished_OutputPath",
                                $"{AnsiCodes.LinkPrefix}{urlString}{AnsiCodes.LinkInfix}{outputPathSpan.ToString()}{AnsiCodes.LinkSuffix}"));
                        }
                        else
                        {
                            Terminal.WriteLine(string.Empty);
                        }
                    }

                    // Print diagnostic output under the Project -> Output line.
                    if (project.BuildMessages is not null)
                    {
                        foreach (BuildMessage buildMessage in project.BuildMessages)
                        {
                            Terminal.WriteLine($"{Indentation}{Indentation}{buildMessage.Message}");
                        }
                    }

                    _buildHasErrors |= haveErrors;
                    _buildHasWarnings |= haveWarnings;

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
    /// The <see cref="IEventSource.TargetStarted"/> callback.
    /// </summary>
    private void TargetStarted(object sender, TargetStartedEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (_restoreContext is null && buildEventContext is not null && _projects.TryGetValue(new ProjectContext(buildEventContext), out Project? project))
        {
            project.Stopwatch.Start();

            string projectFile = Path.GetFileNameWithoutExtension(e.ProjectFile);
            NodeStatus nodeStatus = new(projectFile, project.TargetFramework, e.TargetName, project.Stopwatch);
            UpdateNodeStatus(buildEventContext, nodeStatus);
        }
    }

    private void UpdateNodeStatus(BuildEventContext buildEventContext, NodeStatus? nodeStatus)
    {
        lock (_lock)
        {
            int nodeIndex = NodeIndexForContext(buildEventContext);
            _nodes[nodeIndex] = nodeStatus;
        }
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
        if (_restoreContext is null && buildEventContext is not null && e.TaskName == "MSBuild")
        {
            // This will yield the node, so preemptively mark it idle
            UpdateNodeStatus(buildEventContext, null);

            if (_projects.TryGetValue(new ProjectContext(buildEventContext), out Project? project))
            {
                project.Stopwatch.Stop();
            }
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
            int index = message.IndexOf(FilePathPattern, StringComparison.Ordinal);
            if (index > 0)
            {
                var projectFileName = Path.GetFileName(e.ProjectFile.AsSpan());
                if (!projectFileName.IsEmpty &&
                    message.AsSpan().StartsWith(Path.GetFileNameWithoutExtension(projectFileName)) &&
                    _projects.TryGetValue(new ProjectContext(buildEventContext), out Project? project))
                {
                    ReadOnlyMemory<char> outputPath = e.Message.AsMemory().Slice(index + 4);
                    project.OutputPath = outputPath;
                }
            }

            if (IsImmediateMessage(message))
            {
                RenderImmediateMessage(message);
            }
        }
    }

    /// <summary>
    /// The <see cref="IEventSource.WarningRaised"/> callback.
    /// </summary>
    private void WarningRaised(object sender, BuildWarningEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is not null && _projects.TryGetValue(new ProjectContext(buildEventContext), out Project? project))
        {
            string message = EventArgsFormatting.FormatEventMessage(
                category: AnsiCodes.Colorize("warning", TerminalColor.Yellow),
                subcategory: e.Subcategory,
                message: e.Message,
                code: AnsiCodes.Colorize(e.Code, TerminalColor.Yellow),
                file: HighlightFileName(e.File),
                projectFile: null,
                lineNumber: e.LineNumber,
                endLineNumber: e.EndLineNumber,
                columnNumber: e.ColumnNumber,
                endColumnNumber: e.EndColumnNumber,
                threadId: e.ThreadId,
                logOutputProperties: null);

            if (IsImmediateMessage(message))
            {
                RenderImmediateMessage(message);
            }
            project.AddBuildMessage(MessageSeverity.Warning, message);
        }
    }

    /// <summary>
    /// Detect markers that require special attention from a customer.
    /// </summary>
    /// <param name="message">Raised event.</param>
    /// <returns>true if marker is detected.</returns>
    private bool IsImmediateMessage(string message)
    {
#if NET7_0_OR_GREATER
        return ImmediateMessageRegex().IsMatch(message);
#else
        return _immediateMessageKeywords.Any(imk => message.IndexOf(imk, StringComparison.OrdinalIgnoreCase) >= 0);
#endif
    }

    /// <summary>
    /// The <see cref="IEventSource.ErrorRaised"/> callback.
    /// </summary>
    private void ErrorRaised(object sender, BuildErrorEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is not null && _projects.TryGetValue(new ProjectContext(buildEventContext), out Project? project))
        {
            string message = EventArgsFormatting.FormatEventMessage(
                category: AnsiCodes.Colorize("error", TerminalColor.Red),
                subcategory: e.Subcategory,
                message: e.Message,
                code: AnsiCodes.Colorize(e.Code, TerminalColor.Red),
                file: HighlightFileName(e.File),
                projectFile: null,
                lineNumber: e.LineNumber,
                endLineNumber: e.EndLineNumber,
                columnNumber: e.ColumnNumber,
                endColumnNumber: e.EndColumnNumber,
                threadId: e.ThreadId,
                logOutputProperties: null);

            project.AddBuildMessage(MessageSeverity.Error, message);
        }
    }

#endregion

    #region Refresher thread implementation

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
                DisplayNodes();
            }
        }

        EraseNodes();
    }

    /// <summary>
    /// Render Nodes section.
    /// It shows what all build nodes do.
    /// </summary>
    internal void DisplayNodes()
    {
        NodesFrame newFrame = new NodesFrame(_nodes, width: Terminal.Width, height: Terminal.Height);

        // Do not render delta but clear everything if Terminal width or height have changed.
        if (newFrame.Width != _currentFrame.Width || newFrame.Height != _currentFrame.Height)
        {
            EraseNodes();
        }

        string rendered = newFrame.Render(_currentFrame);

        // Hide the cursor to prevent it from jumping around as we overwrite the live lines.
        Terminal.Write(AnsiCodes.HideCursor);
        try
        {
            // Move cursor back to 1st line of nodes.
            Terminal.WriteLine($"{AnsiCodes.CSI}{_currentFrame.NodesCount + 1}{AnsiCodes.MoveUpToLineStart}");
            Terminal.Write(rendered);
        }
        finally
        {
            Terminal.Write(AnsiCodes.ShowCursor);
        }

        _currentFrame = newFrame;
    }

    /// <summary>
    /// Erases the previously printed live node output.
    /// </summary>
    private void EraseNodes()
    {
        if (_currentFrame.NodesCount == 0)
        {
            return;
        }
        Terminal.WriteLine($"{AnsiCodes.CSI}{_currentFrame.NodesCount + 1}{AnsiCodes.MoveUpToLineStart}");
        Terminal.Write($"{AnsiCodes.CSI}{AnsiCodes.EraseInDisplay}");
        _currentFrame.Clear();
    }

    /// <summary>
    /// Capture states on nodes to be rendered on display.
    /// </summary>
    private sealed class NodesFrame
    {
        private readonly List<string> _nodeStrings = new();
        private readonly StringBuilder _renderBuilder = new();

        public int Width { get; }
        public int Height { get; }
        public int NodesCount { get; private set; }

        public NodesFrame(NodeStatus?[] nodes, int width, int height)
        {
            Width = width;
            Height = height;
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

                // We cant output more than what fits on screen
                // -2 because cursor command F cant reach, in Windows Terminal, very 1st line, and last line is empty caused by very last WriteLine
                if (i >= Height - 2)
                {
                    break;
                }
            }

            NodesCount = i;
        }

        private ReadOnlySpan<char> FitToWidth(ReadOnlySpan<char> input)
        {
            return input.Slice(0, Math.Min(input.Length, Width - 1));
        }

        /// <summary>
        /// Render VT100 string to update from current to next frame.
        /// </summary>
        public string Render(NodesFrame previousFrame)
        {
            StringBuilder sb = _renderBuilder;
            sb.Clear();

            int i = 0;
            for (; i < NodesCount; i++)
            {
                var needed = FitToWidth(NodeString(i).AsSpan());

                // Do we have previous node string to compare with?
                if (previousFrame.NodesCount > i)
                {
                    var previous = FitToWidth(previousFrame.NodeString(i).AsSpan());

                    if (!previous.SequenceEqual(needed))
                    {
                        int commonPrefixLen = previous.CommonPrefixLength(needed);

                        if (commonPrefixLen != 0 && needed.Slice(0, commonPrefixLen).IndexOf('\x1b') == -1)
                        {
                            // no escape codes, so can trivially skip substrings
                            sb.Append($"{AnsiCodes.CSI}{commonPrefixLen}{AnsiCodes.MoveForward}");
                            sb.Append(needed.Slice(commonPrefixLen));
                        }
                        else
                        {
                            sb.Append(needed);
                        }

                        // Shall we clear rest of line
                        if (needed.Length < previous.Length)
                        {
                            sb.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInLine}");
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
                sb.Append($"{AnsiCodes.CSI}{AnsiCodes.EraseInDisplay}");
            }

            return sb.ToString();
        }

        public void Clear()
        {
            NodesCount = 0;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Print a build result summary to the output.
    /// </summary>
    /// <param name="succeeded">True if the build completed with success.</param>
    /// <param name="hasError">True if the build has logged at least one error.</param>
    /// <param name="hasWarning">True if the build has logged at least one warning.</param>
    private string RenderBuildResult(bool succeeded, bool hasError, bool hasWarning)
    {
        if (!succeeded)
        {
            // If the build failed, we print one of three red strings.
            string text = (hasError, hasWarning) switch
            {
                (true, _) => ResourceUtilities.GetResourceString("BuildResult_FailedWithErrors"),
                (false, true) => ResourceUtilities.GetResourceString("BuildResult_FailedWithWarnings"),
                _ => ResourceUtilities.GetResourceString("BuildResult_Failed"),
            };
            return AnsiCodes.Colorize(text, TerminalColor.Red);
        }
        else if (hasWarning)
        {
            return AnsiCodes.Colorize(ResourceUtilities.GetResourceString("BuildResult_SucceededWithWarnings"), TerminalColor.Yellow);
        }
        else
        {
            return AnsiCodes.Colorize(ResourceUtilities.GetResourceString("BuildResult_Succeeded"), TerminalColor.Green);
        }
    }

    /// <summary>
    /// Print a build messages to the output that require special customer's attention.
    /// </summary>
    /// <param name="message">Build message needed to be shown immediately.</param>
    private void RenderImmediateMessage(string message)
    {
        lock (_lock)
        {
            // Calling erase helps to clear the screen before printing the message
            // The immediate output will not overlap with node status reporting
            EraseNodes();
            Terminal.WriteLine(message);
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
    /// Colorizes the filename part of the given path.
    /// </summary>
    private static string? HighlightFileName(string? path)
    {
        if (path == null)
        {
            return null;
        }

        int index = path.LastIndexOfAny(PathSeparators);
        return index >= 0
            ? $"{path.Substring(0, index + 1)}{AnsiCodes.MakeBold(path.Substring(index + 1))}"
            : path;
    }

    #endregion
}
