// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Logging;
using Microsoft.Build.Shared;

#if NET
using System.Buffers;
#endif

#if NETFRAMEWORK
using Microsoft.IO;
#else
using System.IO;
#endif

namespace Microsoft.Build.Logging;

/// <summary>
/// A logger which updates the console output "live" during the build.
/// </summary>
/// <remarks>
/// Uses ANSI/VT100 control codes to erase and overwrite lines as the build is progressing.
/// </remarks>
public sealed partial class TerminalLogger : INodeLogger
{
    private const string FilePathPattern = " -> ";

#if NET
    private static readonly SearchValues<string> _authProviderMessageKeywords = SearchValues.Create(["[CredentialProvider]", "--interactive"], StringComparison.OrdinalIgnoreCase);
#else
    private static readonly string[] _authProviderMessageKeywords = ["[CredentialProvider]", "--interactive"];
#endif

    private static readonly string[] newLineStrings = { "\r\n", "\n" };

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

    private readonly record struct TestSummary(int Total, int Passed, int Skipped, int Failed);

    /// <summary>
    /// The indentation to use for all build output.
    /// </summary>
    internal const string Indentation = "  ";

    internal const string DoubleIndentation = $"{Indentation}{Indentation}";

    internal const string TripleIndentation = $"{Indentation}{Indentation}{Indentation}";

    internal const TerminalColor TargetFrameworkColor = TerminalColor.Cyan;
    internal const TerminalColor RuntimeIdentifierColor = TerminalColor.Magenta;

    internal Func<StopwatchAbstraction>? CreateStopwatch = null;

    /// <summary>
    /// Name of target that identifies the project cache plugin run has just started.
    /// </summary>
    private const string CachePluginStartTarget = "_CachePluginRunStart";

    /// <summary>
    /// Protects access to state shared between the logger callbacks and the rendering thread.
    /// </summary>
    private readonly LockType _lock = new LockType();

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
    private readonly Dictionary<ProjectContext, TerminalProjectInfo> _projects = new();

    private readonly Dictionary<EvalContext, EvalProjectInfo> _evals = new();

    /// <summary>
    /// Tracks the work currently being done by build nodes. Null means the node is not doing any work worth reporting.
    /// </summary>
    /// <remarks>
    /// There is no locking around access to this data structure despite it being accessed concurrently by multiple threads.
    /// However, reads and writes to locations in an array is atomic, so locking is not required.
    /// </remarks>
    private TerminalNodeStatus?[] _nodes = Array.Empty<TerminalNodeStatus>();

    /// <summary>
    /// The timestamp of the <see cref="IEventSource.BuildStarted"/> event.
    /// </summary>
    private DateTime _buildStartTime;

    /// <summary>
    /// The working directory when the build starts, to trim relative output paths.
    /// </summary>
    private readonly string _initialWorkingDirectory = Environment.CurrentDirectory;

    /// <summary>
    /// Number of build errors.
    /// </summary>
    private int _buildErrorsCount;

    /// <summary>
    /// Number of build warnings.
    /// </summary>
    private int _buildWarningsCount;

    /// <summary>
    /// True if restore failed and this failure has already been reported.
    /// </summary>
    private bool _restoreFailed;

    /// <summary>
    /// True if restore happened and finished.
    /// </summary>
    private bool _restoreFinished = false;

    /// <summary>
    /// The project build context corresponding to the <c>Restore</c> initial target, or null if the build is currently
    /// not restoring.
    /// </summary>
    private ProjectContext? _restoreContext;

    /// <summary>
    /// The thread that performs periodic refresh of the console output.
    /// </summary>
    private Thread? _refresher;

    /// <summary>
    /// What is currently displaying in Nodes section as strings representing per-node console output.
    /// </summary>
    private TerminalNodesFrame _currentFrame = new(Array.Empty<TerminalNodeStatus>(), 0, 0);

    /// <summary>
    /// The <see cref="Terminal"/> to write console output to.
    /// </summary>
    private ITerminal Terminal { get; }

    /// <summary>
    /// Should the logger's test environment refresh the console output manually instead of using a background thread?
    /// </summary>
    private bool _manualRefresh;

    /// <summary>
    /// True if we've logged the ".NET SDK is preview" message.
    /// </summary>
    private bool _loggedPreviewMessage;

    /// <summary>
    /// One summary per finished project test run.
    /// </summary>
    private List<TestSummary> _testRunSummaries = new();

    /// <summary>
    /// Name of target that identifies a project that has tests, and that they just started.
    /// </summary>
    private static string _testStartTarget = "_TestRunStart";

    /// <summary>
    /// Time of the oldest observed test target start.
    /// </summary>
    private DateTime? _testStartTime;

    /// <summary>
    /// Time of the most recently observed test target finished.
    /// </summary>
    private DateTime? _testEndTime;

    /// <summary>
    /// Demonstrates whether there exists at least one project which is a cache plugin project.
    /// </summary>
    private bool _hasUsedCache = false;

    /// <summary>
    /// Whether to show TaskCommandLineEventArgs high-priority messages.
    /// </summary>
    private bool _showCommandLine = false;

    /// <summary>
    /// Indicates whether to show the build summary.
    /// </summary>
    private bool? _showSummary;

    /// <summary>
    /// Indicates whether to show the live-updated nodes display.
    /// </summary>
    private bool _showNodesDisplay = true;

    private uint? _originalConsoleMode;

    /// <summary>
    /// Default constructor, used by the MSBuild logger infra.
    /// </summary>
    internal TerminalLogger()
    {
        Terminal = new Terminal();
    }

    internal TerminalLogger(LoggerVerbosity verbosity) : this()
    {
        Verbosity = verbosity;
    }

    /// <summary>
    /// Internal constructor accepting a custom <see cref="ITerminal"/> for testing.
    /// </summary>
    internal TerminalLogger(ITerminal terminal)
    {
        Terminal = terminal;
        _manualRefresh = true;
    }

    /// <summary>
    /// Private constructor invoked by static factory.
    /// </summary>
    internal TerminalLogger(LoggerVerbosity verbosity, uint? originalConsoleMode) : this()
    {
        Verbosity = verbosity;
        _originalConsoleMode = originalConsoleMode;
    }

    /// <summary>
    /// Creates a Terminal logger if possible, or a Console logger.
    /// </summary>
    /// <param name="args">Command line arguments for the logger configuration. Currently, only 'tl|terminallogger' and 'v|verbosity' are supported right now.</param>
    public static ILogger CreateTerminalOrConsoleLogger(string[]? args = null)
    {
        (bool supportsAnsi, bool outputIsScreen, uint? originalConsoleMode) = NativeMethodsShared.QueryIsScreenAndTryEnableAnsiColorCodes();

        return CreateTerminalOrConsoleLogger(args, supportsAnsi, outputIsScreen, originalConsoleMode);
    }

    internal static ILogger CreateTerminalOrConsoleLogger(string[]? args, bool supportsAnsi, bool outputIsScreen, uint? originalConsoleMode)
    {
        LoggerVerbosity verbosity = LoggerVerbosity.Normal;
        string tlEnvVariable = Environment.GetEnvironmentVariable("MSBUILDTERMINALLOGGER") ?? string.Empty;
        string tlArg = string.Empty;
        string? verbosityArg = string.Empty;

        if (args != null)
        {
            string argsString = string.Join(" ", args);

            MatchCollection tlMatches = Regex.Matches(argsString, @"(?:/|-|--)(?:tl|terminallogger):(?'value'on|off|true|false|auto)", RegexOptions.IgnoreCase);
            tlArg = tlMatches.OfType<Match>().LastOrDefault()?.Groups["value"].Value ?? string.Empty;

            MatchCollection verbosityMatches = Regex.Matches(argsString, @"(?:/|-|--)(?:v|verbosity):(?'value'\w+)", RegexOptions.IgnoreCase);
            verbosityArg = verbosityMatches.OfType<Match>().LastOrDefault()?.Groups["value"].Value;
        }

        verbosityArg = verbosityArg?.ToLowerInvariant() switch
        {
            "q" => "quiet",
            "m" => "minimal",
            "n" => "normal",
            "d" => "detailed",
            "diag" => "diagnostic",
            _ => verbosityArg,
        };

        if (Enum.TryParse(verbosityArg, true, out LoggerVerbosity parsedVerbosity))
        {
            verbosity = parsedVerbosity;
        }

        // Command line arguments take precedence over environment variables
        string effectiveValue = !string.IsNullOrEmpty(tlArg) ? tlArg : !string.IsNullOrEmpty(tlEnvVariable) ? tlEnvVariable : "auto";

        bool isForced = IsTerminalLoggerEnabled(effectiveValue);
        bool isDisabled = IsTerminalLoggerDisabled(effectiveValue);

        // if forced, always use the Terminal Logger
        if (isForced)
        {
            return new TerminalLogger(verbosity, originalConsoleMode);
        }

        // If explicitly disabled, always use console logger
        if (isDisabled)
        {
            NativeMethodsShared.RestoreConsoleMode(originalConsoleMode);
            return new ConsoleLogger(verbosity);
        }

        // If not forced and system doesn't support terminal features, fall back to console logger
        if (effectiveValue == "auto" && supportsAnsi && outputIsScreen)
        {
            return new TerminalLogger(verbosity, originalConsoleMode);
        }
        else
        {
            // otherwise the state only allows fallback to console logger
            NativeMethodsShared.RestoreConsoleMode(originalConsoleMode);
            return new ConsoleLogger(verbosity);
        }
    }

    /// <summary>
    /// Checks if the given value indicates TerminalLogger should be enabled/forced.
    /// </summary>
    /// <param name="value">The value to check (from command line or environment variable)</param>
    /// <returns>True if the value indicates TerminalLogger should be enabled</returns>
    private static bool IsTerminalLoggerEnabled(string? value) =>
        value is { Length: > 0 } &&
            (value.Equals("on", StringComparison.InvariantCultureIgnoreCase) ||
             value.Equals("true", StringComparison.InvariantCultureIgnoreCase));

    /// <summary>
    /// Checks if the given value indicates TerminalLogger should be disabled.
    /// </summary>
    /// <param name="value">The value to check (from command line or environment variable)</param>
    /// <returns>True if the value indicates TerminalLogger should be disabled</returns>
    private static bool IsTerminalLoggerDisabled(string? value) =>
        value is { Length: > 0 } &&
            (value.Equals("off", StringComparison.InvariantCultureIgnoreCase) ||
             value.Equals("false", StringComparison.InvariantCultureIgnoreCase));

    #region INodeLogger implementation

    /// <inheritdoc/>
    public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Minimal;

    /// <inheritdoc/>
    public string? Parameters { get; set; } = null;

    /// <inheritdoc/>
    public void Initialize(IEventSource eventSource, int nodeCount)
    {
        // When MSBUILDNOINPROCNODE enabled, NodeId's reported by build start with 2. We need to reserve an extra spot for this case.
        _nodes = new TerminalNodeStatus[nodeCount + 1];

        Initialize(eventSource);
    }

    /// <inheritdoc/>
    public void Initialize(IEventSource eventSource)
    {
        ParseParameters();

        eventSource.BuildStarted += BuildStarted;
        eventSource.BuildFinished += BuildFinished;
        eventSource.ProjectStarted += ProjectStarted;
        eventSource.ProjectFinished += ProjectFinished;
        eventSource.TargetStarted += TargetStarted;
        eventSource.TargetFinished += TargetFinished;
        eventSource.TaskStarted += TaskStarted;
        eventSource.StatusEventRaised += StatusEventRaised;
        eventSource.MessageRaised += MessageRaised;
        eventSource.WarningRaised += WarningRaised;
        eventSource.ErrorRaised += ErrorRaised;

        if (eventSource is IEventSource3 eventSource3)
        {
            eventSource3.IncludeTaskInputs();
        }

        if (eventSource is IEventSource4 eventSource4)
        {
            eventSource4.IncludeEvaluationPropertiesAndItems();
        }
    }


    /// <summary>
    /// Parses out the logger parameters from the Parameters string.
    /// </summary>
    public void ParseParameters()
    {
        foreach (var parameter in LoggerParametersHelper.ParseParameters(Parameters))
        {
            ApplyParameter(parameter.Item1, parameter.Item2);
        }
    }

    /// <summary>
    /// Apply a terminal logger parameter.
    /// parameterValue may be null, if there is no parameter value.
    /// </summary>
    /// <remark>
    /// If verbosity parameter value is not correct, throws an exception. Other incorrect parameter values are disregarded.
    /// </remark>
    private void ApplyParameter(string parameterName, string? parameterValue)
    {
        ErrorUtilities.VerifyThrowArgumentNull(parameterName);

        switch (parameterName.ToUpperInvariant())
        {
            case "V":
            case "VERBOSITY":
                ApplyVerbosityParameter(parameterValue);
                break;
            case "SHOWCOMMANDLINE":
                TryApplyShowCommandLineParameter(parameterValue);
                break;
            case "SUMMARY":
                _showSummary = true;
                break;
            case "NOSUMMARY":
                _showSummary = false;
                break;
            case "DISABLENODEDISPLAY":
                _showNodesDisplay = false;
                break;
        }
    }

    /// <summary>
    /// Apply the verbosity value
    /// </summary>
    private void ApplyVerbosityParameter(string? parameterValue)
    {
        if (parameterValue is not null && LoggerParametersHelper.TryParseVerbosityParameter(parameterValue, out LoggerVerbosity? verbosity))
        {
            Verbosity = (LoggerVerbosity)verbosity;
        }
        else
        {
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string? errorCode, out string? helpKeyword, "InvalidVerbosity", parameterValue);
            throw new LoggerException(message, null, errorCode, helpKeyword);
        }
    }

    /// <summary>
    /// Apply the show command Line value
    /// </summary>
    private bool TryApplyShowCommandLineParameter(string? parameterValue)
    {
        if (String.IsNullOrEmpty(parameterValue))
        {
            _showCommandLine = true;
        }
        else
        {
            return ConversionUtilities.TryConvertStringToBool(parameterValue, out _showCommandLine);
        }

        return true;
    }

    /// <inheritdoc/>
    public void Shutdown()
    {
        NativeMethodsShared.RestoreConsoleMode(_originalConsoleMode);

        _cts.Cancel();
        _refresher?.Join();
        Terminal.Dispose();
        _cts.Dispose();
    }

    public MessageImportance GetMinimumMessageImportance()
    {
        if (Verbosity == LoggerVerbosity.Quiet)
        {
            // If the verbosity is quiet, we don't want to log anything.
            return MessageImportance.High - 1;
        }
        return MessageImportance.High;
    }

    #endregion

    #region Logger callbacks

    /// <summary>
    /// The <see cref="IEventSource.BuildStarted"/> callback.
    /// </summary>
    private void BuildStarted(object sender, BuildStartedEventArgs e)
    {
        if (!_manualRefresh && _showNodesDisplay)
        {
            _refresher = new Thread(ThreadProc);
            _refresher.Name = "Terminal Logger Node Display Refresher";
            _refresher.Start();
        }

        _buildStartTime = e.Timestamp;

        if (Terminal.SupportsProgressReporting && Verbosity != LoggerVerbosity.Quiet)
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

        Terminal.BeginUpdate();
        try
        {
            if (Verbosity > LoggerVerbosity.Quiet)
            {
                string duration = (e.Timestamp - _buildStartTime).TotalSeconds.ToString("F1");
                string buildResult = GetBuildResultString(e.Succeeded, _buildErrorsCount, _buildWarningsCount);

                Terminal.WriteLine("");
                if (_testRunSummaries.Any())
                {
                    var total = _testRunSummaries.Sum(t => t.Total);
                    var failed = _testRunSummaries.Sum(t => t.Failed);
                    var passed = _testRunSummaries.Sum(t => t.Passed);
                    var skipped = _testRunSummaries.Sum(t => t.Skipped);
                    var testDuration = (_testStartTime != null && _testEndTime != null ? (_testEndTime - _testStartTime).Value.TotalSeconds : 0).ToString("F1");

                    var colorizeFailed = failed > 0;
                    var colorizePassed = passed > 0 && _buildErrorsCount == 0 && failed == 0;
                    var colorizeSkipped = skipped > 0 && skipped == total && _buildErrorsCount == 0 && failed == 0;

                    string summaryAndTotalText = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TestSummary_BannerAndTotal", total);
                    string failedText = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TestSummary_Failed", failed);
                    string passedText = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TestSummary_Succeeded", passed);
                    string skippedText = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TestSummary_Skipped", skipped);
                    string durationText = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TestSummary_Duration", testDuration);

                    failedText = colorizeFailed ? AnsiCodes.Colorize(failedText.ToString(), TerminalColor.Red) : failedText;
                    passedText = colorizePassed ? AnsiCodes.Colorize(passedText.ToString(), TerminalColor.Green) : passedText;
                    skippedText = colorizeSkipped ? AnsiCodes.Colorize(skippedText.ToString(), TerminalColor.Yellow) : skippedText;

                    Terminal.WriteLine(string.Join(CultureInfo.CurrentCulture.TextInfo.ListSeparator + " ", summaryAndTotalText, failedText, passedText, skippedText, durationText));
                }

                if (_showSummary == true)
                {
                    RenderBuildSummary();
                }

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
        }
        finally
        {
            if (Terminal.SupportsProgressReporting && Verbosity != LoggerVerbosity.Quiet)
            {
                Terminal.Write(AnsiCodes.RemoveProgress);
            }

            Terminal.EndUpdate();
        }

        _projects.Clear();
        _testRunSummaries.Clear();
        _buildErrorsCount = 0;
        _buildWarningsCount = 0;
        _restoreFailed = false;
        _testStartTime = null;
        _testEndTime = null;
    }

    private void RenderBuildSummary()
    {
        if (_buildErrorsCount == 0 && _buildWarningsCount == 0)
        {
            // No errors/warnings to display.
            return;
        }

        Terminal.WriteLine(ResourceUtilities.GetResourceString("BuildSummary"));

        foreach (TerminalProjectInfo project in _projects.Values.Where(p => p.HasErrorsOrWarnings))
        {
            string duration = project.Stopwatch.ElapsedSeconds.ToString("F1");
            string buildResult = GetBuildResultString(project.Succeeded, project.ErrorCount, project.WarningCount);
            string projectHeader = GetProjectFinishedHeader(project, buildResult, duration);

            Terminal.WriteLine(projectHeader);

            foreach (TerminalBuildMessage buildMessage in project.GetBuildErrorAndWarningMessages())
            {
                Terminal.WriteLine($"{DoubleIndentation}{buildMessage.Message}");
            }
        }

        Terminal.WriteLine(string.Empty);
    }

    private void StatusEventRaised(object sender, BuildStatusEventArgs e)
    {
        switch (e)
        {
            case BuildCanceledEventArgs cancelEvent:
                RenderImmediateMessage(cancelEvent.Message!);
                break;
            case ProjectEvaluationStartedEventArgs _evalStart:
                break;
            case ProjectEvaluationFinishedEventArgs evalFinish:
                CaptureEvalContext(evalFinish);
                break;
        }
    }

    /// <summary>
    /// The <see cref="IEventSource.ProjectStarted"/> callback.
    /// </summary>
    private void ProjectStarted(object sender, ProjectStartedEventArgs e)
    {
        if (e.BuildEventContext is null)
        {
            return;
        }

        ProjectContext c = new(e.BuildEventContext);

        if (_restoreContext is null)
        {
            EvalContext evalContext = new(e.BuildEventContext);
            string? targetFramework = null;
            string? runtimeIdentifier = null;
            if (_evals.TryGetValue(evalContext, out EvalProjectInfo evalInfo))
            {
                targetFramework = evalInfo.TargetFramework;
                runtimeIdentifier = evalInfo.RuntimeIdentifier;
            }
            System.Diagnostics.Debug.Assert(evalInfo != default, "EvalProjectInfo should have been captured before ProjectStarted");

            TerminalProjectInfo projectInfo = new(c, evalInfo, CreateStopwatch?.Invoke());
            _projects[c] = projectInfo;

            // First ever restore in the build is starting.
            if (e.TargetNames == "Restore" && !_restoreFinished)
            {
                _restoreContext = c;
                int nodeIndex = NodeIndexForContext(e.BuildEventContext);
                _nodes[nodeIndex] = new TerminalNodeStatus(e.ProjectFile!, targetFramework, runtimeIdentifier, "Restore", _projects[c].Stopwatch);
            }
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

        // Continue execution and add project summary to the static part of the Console only if verbosity is higher than Quiet.
        if (Verbosity <= LoggerVerbosity.Quiet)
        {
            return;
        }

        ProjectContext c = new(buildEventContext);

        if (_projects.TryGetValue(c, out TerminalProjectInfo? project))
        {
            project.Succeeded = e.Succeeded;
            project.Stopwatch.Stop();
            lock (_lock)
            {
                Terminal.BeginUpdate();
                try
                {
                    EraseNodes();

                    string duration = project.Stopwatch.ElapsedSeconds.ToString("F1");
                    ReadOnlyMemory<char>? outputPath = project.OutputPath;

                    // Build result. One of 'failed', 'succeeded with warnings', or 'succeeded' depending on the build result and diagnostic messages
                    // reported during build.
                    string buildResult = GetBuildResultString(project.Succeeded, project.ErrorCount, project.WarningCount);

                    // Check if we're done restoring.
                    if (c == _restoreContext)
                    {
                        if (e.Succeeded)
                        {
                            if (project.HasErrorsOrWarnings)
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
                        _restoreFinished = true;
                    }
                    // If this was a notable project build, we print it as completed only if it's produced an output or warnings/error.
                    // If this is a test project, print it always, so user can see either a success or failure, otherwise success is hidden
                    // and it is hard to see if project finished, or did not run at all.
                    else if (project.OutputPath is not null || project.BuildMessages is not null || project.IsTestProject)
                    {
                        // Show project build complete and its output
                        string projectFinishedHeader = GetProjectFinishedHeader(project, buildResult, duration);
                        Terminal.Write(projectFinishedHeader);

                        // Print the output path as a link if we have it.
                        if (outputPath is { } outputPathSpan)
                        {
                            (var projectDisplayPath, var urlLink) = DetermineOutputPathToRender(outputPathSpan, _initialWorkingDirectory.AsMemory(), project.SourceRoot);
                            Terminal.WriteLine(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ProjectFinished_OutputPath", CreateLink(urlLink, projectDisplayPath.ToString())));
                        }
                        else
                        {
                            Terminal.WriteLine(string.Empty);
                        }
                    }

                    // Print diagnostic output under the Project -> Output line.
                    if (project.BuildMessages is not null)
                    {
                        foreach (TerminalBuildMessage buildMessage in project.BuildMessages)
                        {
                            Terminal.WriteLine($"{DoubleIndentation}{buildMessage.Message}");
                        }
                    }

                    _buildErrorsCount += project.ErrorCount;
                    _buildWarningsCount += project.WarningCount;

                    if (_showNodesDisplay)
                    {
                        DisplayNodes();
                    }
                }
                finally
                {
                    Terminal.EndUpdate();
                }
            }
        }
    }
    
    private void CaptureEvalContext(ProjectEvaluationFinishedEventArgs evalFinish)
    {
        var buildEventContext = evalFinish.BuildEventContext;
        if (buildEventContext is null)
        {
            return;
        }

        EvalContext c = new(buildEventContext);

        if (!_evals.TryGetValue(c, out EvalProjectInfo _))
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
            var evalInfo = new EvalProjectInfo(c, evalFinish.ProjectFile!, tfm, rid);
            _evals[c] = evalInfo;
        }
    }

    private static (string outputPathToRender, Uri? linkToAssign) DetermineOutputPathToRender(ReadOnlyMemory<char> outputPath, ReadOnlyMemory<char> workingDir, ReadOnlyMemory<char>? sourceRoot)
    {
        ReadOnlySpan<char> outputPathSpan = outputPath.Span;

        // Generates file:// schema url string which is better handled by various Terminal clients than raw folder name.
#if NET
        Uri.TryCreate(new(Path.GetDirectoryName(outputPathSpan)), UriKind.Absolute, out Uri? uri);
#else
        Uri.TryCreate(Path.GetDirectoryName(outputPathSpan.ToString()), UriKind.Absolute, out Uri? uri);
#endif

        // now we compute the path to show the user for this project.
        // some options:
        // * the raw, full output path from the MSBuild logic (OutputPath property)
        // * the output path relative to the initial working directory, if it is under it
        // * the output path relative to the source root, if it is under it

        // full path fallback
        var projectDisplayPathSpan = outputPathSpan;
        var workingDirectorySpan = workingDir.Span;
        // under working dir case
        if (outputPathSpan.StartsWith(workingDirectorySpan, FileUtilities.PathComparison))
        {
            if (outputPathSpan.Length > workingDirectorySpan.Length
                && (outputPathSpan[workingDirectorySpan.Length] == Path.DirectorySeparatorChar
                    || outputPathSpan[workingDirectorySpan.Length] == Path.AltDirectorySeparatorChar))
            {
                projectDisplayPathSpan = outputPathSpan.Slice(workingDirectorySpan.Length + 1);
            }
        }
        // under source root case
        else if (sourceRoot is { Span: var sourceRootSpan })
        {
            var relativePathFromWorkingDirToSourceRoot = Path.GetRelativePath(workingDirectorySpan.ToString(), sourceRootSpan.ToString()).AsSpan();
            if (outputPathSpan.StartsWith(sourceRootSpan, FileUtilities.PathComparison))
            {
                if (outputPathSpan.Length > sourceRootSpan.Length
                    // offsets are -1 here compared to above for ***reasons***
                    && (outputPathSpan[sourceRootSpan.Length - 1] == Path.DirectorySeparatorChar
                        || outputPathSpan[sourceRootSpan.Length - 1] == Path.AltDirectorySeparatorChar))
                {

                    projectDisplayPathSpan = Path.Combine(relativePathFromWorkingDirToSourceRoot.ToString(), outputPathSpan.Slice(sourceRootSpan.Length).ToString()).AsSpan();
                }
            }
        }
#if NET
        return (new(projectDisplayPathSpan), uri);
#else
        return (projectDisplayPathSpan.ToString(), uri);
#endif
    }

    private static string? CreateLink(Uri? uri, string? linkText) =>
        (uri, linkText) switch
        {
            (null, _) => string.IsNullOrEmpty(linkText) ? null : linkText,
            (_, null) => null,
            _ => $"{AnsiCodes.LinkPrefix}{uri}{AnsiCodes.LinkInfix}{linkText}{AnsiCodes.LinkSuffix}",
        };

    private static string GetProjectFinishedHeader(TerminalProjectInfo project, string buildResult, string duration)
    {
        string projectFile = project.ProjectFile is not null ?
            Path.GetFileNameWithoutExtension(project.ProjectFile) :
            string.Empty;

        return (project.TargetFramework, project.RuntimeIdentifier, project.IsTestProject) switch
        {
            (string tfm, null, true) => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TestProjectFinished_WithTF",
                Indentation,
                projectFile,
                AnsiCodes.Colorize(tfm, TargetFrameworkColor),
                buildResult,
                duration),
            (string tfm, null, false) => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ProjectFinished_WithTF",
                Indentation,
                projectFile,
                AnsiCodes.Colorize(tfm, TargetFrameworkColor),
                buildResult,
                duration),
            (null, string rid, true) => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TestProjectFinished_WithTF",
                Indentation,
                projectFile,
                AnsiCodes.Colorize(rid, RuntimeIdentifierColor),
                buildResult,
                duration),
            (null, string rid, false) => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ProjectFinished_WithTF",
                Indentation,
                projectFile,
                AnsiCodes.Colorize(rid, RuntimeIdentifierColor),
                buildResult,
                duration),
            (string tfm, string rid, true) => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TestProjectFinished_WithTFAndRID",
                Indentation,
                projectFile,
                AnsiCodes.Colorize(tfm, TargetFrameworkColor),
                AnsiCodes.Colorize(rid, RuntimeIdentifierColor),
                buildResult,
                duration),
            (string tfm, string rid, false) => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ProjectFinished_WithTFAndRID",
                Indentation,
                projectFile,
                AnsiCodes.Colorize(tfm, TargetFrameworkColor),
                AnsiCodes.Colorize(rid, RuntimeIdentifierColor),
                buildResult,
                duration),
            (null, null, true) => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("TestProjectFinished_NoTF",
                Indentation,
                projectFile,
                buildResult,
                duration),
            (null, null, false) => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ProjectFinished_NoTF",
                Indentation,
                projectFile,
                buildResult,
                duration),
        };
    }

    /// <summary>
    /// The <see cref="IEventSource.TargetStarted"/> callback.
    /// </summary>
    private void TargetStarted(object sender, TargetStartedEventArgs e)
    {
        var buildEventContext = e.BuildEventContext;
        if (_restoreContext is null && buildEventContext is not null && _projects.TryGetValue(new ProjectContext(buildEventContext), out TerminalProjectInfo? project))
        {
            project.Stopwatch.Start();

            string projectFile = Path.GetFileNameWithoutExtension(e.ProjectFile);

            string targetName = e.TargetName;
            if (targetName == CachePluginStartTarget)
            {
                project.IsCachePluginProject = true;
                _hasUsedCache = true;
            }

            if (targetName == _testStartTarget)
            {
                targetName = "Testing";

                // Use the minimal start time, so if we run tests in parallel, we can calculate duration
                // as this start time, minus time when tests finished.
                _testStartTime = _testStartTime == null
                    ? e.Timestamp
                    : e.Timestamp < _testStartTime
                        ? e.Timestamp : _testStartTime;
                project.IsTestProject = true;
            }

            TerminalNodeStatus nodeStatus = new(projectFile, project.TargetFramework, project.RuntimeIdentifier, targetName, project.Stopwatch);
            UpdateNodeStatus(buildEventContext, nodeStatus);
        }
    }

    private void UpdateNodeStatus(BuildEventContext buildEventContext, TerminalNodeStatus? nodeStatus)
    {
        int nodeIndex = NodeIndexForContext(buildEventContext);
        _nodes[nodeIndex] = nodeStatus;
    }

    /// <summary>
    /// The <see cref="IEventSource.TargetFinished"/> callback. Unused.
    /// </summary>
    private void TargetFinished(object sender, TargetFinishedEventArgs e)
    {
        // For cache plugin projects which result in a cache hit, ensure the output path is set
        // to the item spec corresponding to the GetTargetPath target upon completion.
        var buildEventContext = e.BuildEventContext;
        var targetOutputs = e.TargetOutputs;
        if (_restoreContext is not null || buildEventContext is null)
        {
            return;
        }

        if (targetOutputs is not null
                && _hasUsedCache
                && e.TargetName == "GetTargetPath"
                && _projects.TryGetValue(new ProjectContext(buildEventContext), out TerminalProjectInfo? project))
        {
            if (project is not null && project.IsCachePluginProject)
            {
                foreach (ITaskItem output in targetOutputs)
                {
                    project.OutputPath = output.ItemSpec.AsMemory();
                    break;
                }
            }
        }
        else if (targetOutputs is not null
            && e.TargetName == "InitializeSourceRootMappedPaths"
            && _projects.TryGetValue(new ProjectContext(buildEventContext), out project)
            && project.SourceRoot is null)
        {
            project.SourceRoot =
                (targetOutputs as IEnumerable<ITaskItem>)?
                .FirstOrDefault(root => !string.IsNullOrEmpty(root.GetMetadata("SourceControl")))
                ?.ItemSpec.AsMemory();
        }
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

            if (_projects.TryGetValue(new ProjectContext(buildEventContext), out TerminalProjectInfo? project))
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
            var hasProject = _projects.TryGetValue(new ProjectContext(buildEventContext), out TerminalProjectInfo? project);

            // Detect project output path by matching high-importance messages against the "$(MSBuildProjectName) -> ..."
            // pattern used by the CopyFilesToOutputDirectory target.
            int index = message.IndexOf(FilePathPattern, StringComparison.Ordinal);
            if (index > 0)
            {
                var projectFileName = Path.GetFileName(e.ProjectFile.AsSpan());
                if (!projectFileName.IsEmpty &&
                    message.AsSpan().StartsWith(Path.GetFileNameWithoutExtension(projectFileName)) && hasProject)
                {
                    ReadOnlyMemory<char> outputPath = e.Message.AsMemory().Slice(index + 4);
                    project!.OutputPath = outputPath;
                    return;
                }
            }

            // auth provider messages should always be shown to the user.
            if (IsAuthProviderMessage(message))
            {
                RenderImmediateMessage(message);
                return;
            }

            if (Verbosity > LoggerVerbosity.Quiet)
            {
                if (e.Code == "NETSDK1057" && !_loggedPreviewMessage)
                {
                    // ensure we only log the preview message once for the entire build.
                    if (!_loggedPreviewMessage)
                    {
                        // The SDK will log the high-pri "not-a-warning" message NETSDK1057
                        // when it's a preview version up to MaxCPUCount times, but that's
                        // an implementation detail--the user cares about at most one.
                        RenderImmediateMessage(FormatSimpleMessageWithoutFileData(e, DoubleIndentation));
                        _loggedPreviewMessage = true;
                    }
                    return;
                }
            }

            if (hasProject && project!.IsTestProject)
            {
                var node = _nodes[NodeIndexForContext(buildEventContext)];

                // Consumes test update messages produced by VSTest and MSTest runner.
                if (node != null && e is IExtendedBuildEventArgs extendedMessage)
                {
                    switch (extendedMessage.ExtendedType)
                    {
                        case "TLTESTPASSED":
                            {
                                var indicator = extendedMessage.ExtendedMetadata!["localizedResult"]!;
                                var displayName = extendedMessage.ExtendedMetadata!["displayName"]!;

                                var status = new TerminalNodeStatus(node.Project, node.TargetFramework, node.RuntimeIdentifier, TerminalColor.Green, indicator, displayName, project.Stopwatch);
                                UpdateNodeStatus(buildEventContext, status);
                                break;
                            }

                        case "TLTESTSKIPPED":
                            {
                                var indicator = extendedMessage.ExtendedMetadata!["localizedResult"]!;
                                var displayName = extendedMessage.ExtendedMetadata!["displayName"]!;

                                var status = new TerminalNodeStatus(node.Project, node.TargetFramework, node.RuntimeIdentifier, TerminalColor.Yellow, indicator, displayName, project.Stopwatch);
                                UpdateNodeStatus(buildEventContext, status);
                                break;
                            }

                        case "TLTESTFINISH":
                            {
                                // Collect test run summary.
                                if (Verbosity > LoggerVerbosity.Quiet)
                                {
                                    _ = int.TryParse(extendedMessage.ExtendedMetadata!["total"]!, out int total);
                                    _ = int.TryParse(extendedMessage.ExtendedMetadata!["passed"]!, out int passed);
                                    _ = int.TryParse(extendedMessage.ExtendedMetadata!["skipped"]!, out int skipped);
                                    _ = int.TryParse(extendedMessage.ExtendedMetadata!["failed"]!, out int failed);

                                    _testRunSummaries.Add(new TestSummary(total, passed, skipped, failed));

                                    _testEndTime = _testEndTime == null
                                            ? e.Timestamp
                                            : e.Timestamp > _testEndTime
                                                ? e.Timestamp : _testEndTime;
                                }

                                break;
                            }

                        case "TLTESTOUTPUT":
                            {
                                if (e.Message != null && Verbosity > LoggerVerbosity.Quiet)
                                {
                                    RenderImmediateMessage(e.Message);
                                }
                                break;
                            }
                    }
                    return;
                }
            }

            if (Verbosity > LoggerVerbosity.Normal)
            {
                if (e is TaskCommandLineEventArgs && !_showCommandLine)
                {
                    return;
                }

                if (hasProject)
                {
                    project!.AddBuildMessage(TerminalMessageSeverity.Message, FormatInformationalMessage(e));
                }
                else
                {
                    // Display messages reported by MSBuild, even if it's not tracked in _projects collection.
                    RenderImmediateMessage(message);
                }
            }
        }
    }

    private static Uri? GenerateLinkForMessage(BuildMessageEventArgs e)
    {
        if (e.HelpKeyword is not null)
        {
            // generate a default help keyword based link? fw?...
            return GenerateLinkForHelpKeyword(e.HelpKeyword);
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// The <see cref="IEventSource.WarningRaised"/> callback.
    /// </summary>
    private void WarningRaised(object sender, BuildWarningEventArgs e)
    {
        BuildEventContext? buildEventContext = e.BuildEventContext;

        // auth provider messages are 'global' in nature and should be a) immediate reported, and b) not re-reported in the summary.
        if (IsAuthProviderMessage(e.Message))
        {
            RenderImmediateMessage(FormatWarningMessage(e, Indentation));
            return;
        }

        if (buildEventContext is not null
            && _projects.TryGetValue(new ProjectContext(buildEventContext), out TerminalProjectInfo? project)
            && Verbosity > LoggerVerbosity.Quiet)
        {
            // If the warning is not a 'global' auth provider message, but is immediate, we render it immediately
            // but we don't early return so that the project also tracks it.
            if (IsImmediateWarning(e.Code))
            {
                RenderImmediateMessage(FormatWarningMessage(e, Indentation));
            }

            // This is the general case - _most_ warnings are not immediate, so we add them to the project summary
            // and display them in the per-project and final summary.
            project.AddBuildMessage(TerminalMessageSeverity.Warning, FormatWarningMessage(e, TripleIndentation));
        }
        else
        {
            // It is necessary to display warning messages reported by MSBuild,
            // even if it's not tracked in _projects collection or the verbosity is Quiet.
            // The idea here (similar to the implementation in ErrorRaised) is that
            // even in Quiet scenarios we need to show warnings/errors, even if not in
            // full project-tree view
            RenderImmediateMessage(FormatWarningMessage(e, Indentation));
            _buildWarningsCount++;
        }
    }

    private static Uri? GenerateLinkForWarning(BuildWarningEventArgs e)
    {
        if (e.HelpLink is not null && Uri.TryCreate(e.HelpLink, UriKind.Absolute, out Uri? uri))
        {
            return uri;
        }
        else if (e.HelpKeyword is not null)
        {
            // generate a default help keyword based link? fw?...
            return GenerateLinkForHelpKeyword(e.HelpKeyword);
        }
        else
        {
            return null;
        }
    }

    private static Uri GenerateLinkForHelpKeyword(string helpKeyword) => new($"https://go.microsoft.com/fwlink/?LinkId={helpKeyword}");

    /// <summary>
    /// Detect markers that require special attention from a customer.
    /// </summary>
    /// <param name="message">Raised event.</param>
    /// <returns>true if marker is detected.</returns>
    private static bool IsAuthProviderMessage(string? message) =>
#if NET
        message is not null && message.AsSpan().ContainsAny(_authProviderMessageKeywords);
#else
        message is not null && _authProviderMessageKeywords.Any(imk => message.IndexOf(imk, StringComparison.OrdinalIgnoreCase) >= 0);
#endif


    private static bool IsImmediateWarning(string code) => code == "MSB3026";

    private static Uri? GenerateLinkForError(BuildErrorEventArgs e)
    {
        if (e.HelpLink is not null && Uri.TryCreate(e.HelpLink, UriKind.Absolute, out Uri? uri))
        {
            return uri;
        }
        else if (e.HelpKeyword is not null)
        {
            // generate a default help keyword based link? fw?...
            return GenerateLinkForHelpKeyword(e.HelpKeyword);
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// The <see cref="IEventSource.ErrorRaised"/> callback.
    /// </summary>
    private void ErrorRaised(object sender, BuildErrorEventArgs e)
    {
        BuildEventContext? buildEventContext = e.BuildEventContext;

        if (buildEventContext is not null
            && _projects.TryGetValue(new ProjectContext(buildEventContext), out TerminalProjectInfo? project)
            && Verbosity > LoggerVerbosity.Quiet)
        {
            project.AddBuildMessage(TerminalMessageSeverity.Error, FormatErrorMessage(e, TripleIndentation));
        }
        else
        {
            // It is necessary to display error messages reported by MSBuild, even if it's not tracked in _projects collection or the verbosity is Quiet.
            // For nicer formatting, any messages from the engine we strip the file portion from.
            bool hasMSBuildPlaceholderLocation = e.File.Equals("MSBUILD", StringComparison.Ordinal);
            RenderImmediateMessage(FormatErrorMessage(e, Indentation, requireFileAndLinePortion: !hasMSBuildPlaceholderLocation));
            _buildErrorsCount++;
        }
    }

    #endregion

    #region Refresher thread implementation

    /// <summary>
    /// The <see cref="_refresher"/> thread proc.
    /// </summary>
    private void ThreadProc()
    {
        // 1_000 / 30 is a poor approx of 30Hz
        var count = 0;
        while (!_cts.Token.WaitHandle.WaitOne(1_000 / 30))
        {
            count++;
            lock (_lock)
            {
                // Querying the terminal for it's dimensions is expensive, so we only do it every 30 frames e.g. once a second.
                if (count >= 30)
                {
                    count = 0;
                    DisplayNodes();
                }
                else
                {
                    DisplayNodes(false);
                }
            }
        }

        EraseNodes();
    }

    /// <summary>
    /// Render Nodes section.
    /// It shows what all build nodes do.
    /// </summary>
    internal void DisplayNodes(bool updateSize = true)
    {
        var width = updateSize ? Terminal.Width : _currentFrame.Width;
        var height = updateSize ? Terminal.Height : _currentFrame.Height;
        TerminalNodesFrame newFrame = new TerminalNodesFrame(_nodes, width: width, height: height);

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

    #endregion

    #region Helpers

    /// <summary>
    /// Construct a build result summary string.
    /// </summary>
    /// <param name="succeeded">True if the build completed with success.</param>
    /// <param name="countErrors">The number of errors encountered during the build.</param>
    /// <param name="countWarnings">The number of warnings encountered during the build.</param>
    /// <returns>A string representing the build result summary.</returns>
    private static string GetBuildResultString(bool succeeded, int countErrors, int countWarnings)
    {
        if (!succeeded)
        {
            // If the build failed, we print one of three red strings.
            string text = (countErrors > 0, countWarnings > 0) switch
            {
                (true, true) => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("BuildResult_FailedWithErrorsAndWarnings", countErrors, countWarnings),
                (true, _) => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("BuildResult_FailedWithErrors", countErrors),
                (false, true) => ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("BuildResult_FailedWithWarnings", countWarnings),
                _ => ResourceUtilities.GetResourceString("BuildResult_Failed"),
            };
            return AnsiCodes.Colorize(text, TerminalColor.Red);
        }
        else if (countWarnings > 0)
        {
            return AnsiCodes.Colorize(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("BuildResult_SucceededWithWarnings", countWarnings), TerminalColor.Yellow);
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

        int index = path.AsSpan().LastIndexOfAny(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return index >= 0
            ? $"{path.Substring(0, index + 1)}{AnsiCodes.MakeBold(path.Substring(index + 1))}"
            : path;
    }

    private string FormatWarningMessage(BuildWarningEventArgs e, string indent)
    {
        return FormatEventMessage(
                category: AnsiCodes.Colorize("warning", TerminalColor.Yellow),
                subcategory: e.Subcategory,
                message: e.Message,
                code: AnsiCodes.Colorize(CreateLink(GenerateLinkForWarning(e), e.Code), TerminalColor.Yellow),
                file: HighlightFileName(e.File),
                lineNumber: e.LineNumber,
                endLineNumber: e.EndLineNumber,
                columnNumber: e.ColumnNumber,
                endColumnNumber: e.EndColumnNumber,
                indent,
                terminalWidth: Terminal.Width);
    }

    private string FormatInformationalMessage(BuildMessageEventArgs e)
    {
        return FormatEventMessage(
                category: null,
                subcategory: e.Subcategory,
                message: e.Message,
                code: CreateLink(GenerateLinkForMessage(e), e.Code),
                file: HighlightFileName(e.File),
                lineNumber: e.LineNumber,
                endLineNumber: e.EndLineNumber,
                columnNumber: e.ColumnNumber,
                endColumnNumber: e.EndColumnNumber,
                indent: string.Empty,
                terminalWidth: Terminal.Width,
                requireFileAndLinePortion: false);
    }

    /// <summary>
    /// Renders message with just code/category/message data.
    /// No file data is included. This can be used for immediate/one-time
    /// messages that lack a specific project context, such as the .NET
    /// SDK's 'preview version' message, while not removing the code.
    /// </summary>
    private string FormatSimpleMessageWithoutFileData(BuildMessageEventArgs e, string indent)
    {
        return FormatEventMessage(
                category: AnsiCodes.Colorize("info", TerminalColor.Default),
                subcategory: null,
                message: e.Message,
                code: AnsiCodes.Colorize(e.Code, TerminalColor.Default),
                file: null,
                lineNumber: 0,
                endLineNumber: 0,
                columnNumber: 0,
                endColumnNumber: 0,
                indent,
                terminalWidth: Terminal.Width,
                requireFileAndLinePortion: false,
                prependIndentation: true);
    }

    private string FormatErrorMessage(BuildErrorEventArgs e, string indent, bool requireFileAndLinePortion = true)
    {
        return FormatEventMessage(
                category: AnsiCodes.Colorize("error", TerminalColor.Red),
                subcategory: e.Subcategory,
                message: e.Message,
                code: AnsiCodes.Colorize(CreateLink(GenerateLinkForError(e), e.Code), TerminalColor.Red),
                file: HighlightFileName(e.File),
                lineNumber: e.LineNumber,
                endLineNumber: e.EndLineNumber,
                columnNumber: e.ColumnNumber,
                endColumnNumber: e.EndColumnNumber,
                indent,
                terminalWidth: Terminal.Width,
                requireFileAndLinePortion: requireFileAndLinePortion);
    }

    private static string FormatEventMessage(
            string? category,
            string? subcategory,
            string? message,
            string? code,
            string? file,
            int lineNumber,
            int endLineNumber,
            int columnNumber,
            int endColumnNumber,
            string indent,
            int terminalWidth,
            bool requireFileAndLinePortion = true,
            bool prependIndentation = false)
    {
        message ??= string.Empty;
        StringBuilder builder = new(128);

        if (prependIndentation)
        {
            builder.Append(indent);
        }

        if (requireFileAndLinePortion)
        {
            if (string.IsNullOrEmpty(file))
            {
                builder.Append("MSBUILD : ");  // Should not be localized.
            }
            else
            {
                builder.Append(file);

                if (lineNumber == 0)
                {
                    builder.Append(" : ");
                }
                else
                {
                    if (columnNumber == 0)
                    {
                        builder.Append(endLineNumber == 0 ?
                            $"({lineNumber}): " :
                            $"({lineNumber}-{endLineNumber}): ");
                    }
                    else
                    {
                        if (endLineNumber == 0)
                        {
                            builder.Append(endColumnNumber == 0 ?
                                $"({lineNumber},{columnNumber}): " :
                                $"({lineNumber},{columnNumber}-{endColumnNumber}): ");
                        }
                        else
                        {
                            builder.Append(endColumnNumber == 0 ?
                                $"({lineNumber}-{endLineNumber},{columnNumber}): " :
                                $"({lineNumber},{columnNumber},{endLineNumber},{endColumnNumber}): ");
                        }
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(subcategory))
        {
            builder.Append(subcategory);
            builder.Append(' ');
        }

        if (!string.IsNullOrEmpty(category))
        {
            builder.Append(category);
            builder.Append(' ');
        }

        if (!string.IsNullOrEmpty(code))
        {
            builder.Append(code);
            builder.Append(": ");
        }

        // render multi-line message in a special way
        if (message.Contains('\n'))
        {
            // Place the multiline message under the project in case of minimal and higher verbosity.
            string[] lines = message.Split(newLineStrings, StringSplitOptions.None);

            foreach (string line in lines)
            {
                if (indent.Length + line.Length > terminalWidth) // custom wrapping with indentation
                {
                    WrapText(builder, line, terminalWidth, indent);
                }
                else
                {
                    builder.AppendLine();
                    builder.Append(indent);
                    builder.Append(line);
                }
            }
        }
        else
        {
            builder.Append(message);
        }

        return builder.ToString();
    }

    private static void WrapText(StringBuilder sb, string text, int maxLength, string indent)
    {
        int start = 0;
        while (start < text.Length)
        {
            int length = Math.Min(maxLength - indent.Length, text.Length - start);
            sb.AppendLine();
            sb.Append(indent);
            sb.Append(text.AsSpan().Slice(start, length));

            start += length;
        }
    }

    #endregion
}
