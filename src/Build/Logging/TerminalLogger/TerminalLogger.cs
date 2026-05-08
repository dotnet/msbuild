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
public sealed partial class TerminalLogger : ProjectTrackingLoggerBase<EvalProjectInfo, TerminalNodeStatus, TerminalProjectInfo, TerminalBuildData>
{
    private const string FilePathPattern = " -> ";
    private const string MSBuildTaskName = "MSBuild";

#if NET
    private static readonly SearchValues<string> _authProviderMessageKeywords = SearchValues.Create(["[CredentialProvider]", "--interactive"], StringComparison.OrdinalIgnoreCase);
#else
    private static readonly string[] _authProviderMessageKeywords = ["[CredentialProvider]", "--interactive"];
#endif

    private static readonly string[] newLineStrings = { "\r\n", "\n" };

    /// <summary>
    /// Protects access to the stdout - ensures that only one thread writes to the console at a time.
    /// </summary>
    private readonly LockType _renderLock = new();

    private readonly record struct TestSummary(int Total, int Passed, int Skipped, int Failed);

    /// <summary>
    /// The indentation to use for all build output.
    /// </summary>
    internal const string Indentation = "  ";

    internal const string DoubleIndentation = $"{Indentation}{Indentation}";

    internal const string TripleIndentation = $"{Indentation}{Indentation}{Indentation}";

    internal const TerminalColor TargetFrameworkColor = TerminalColor.Cyan;
    internal const TerminalColor RuntimeIdentifierColor = TerminalColor.Magenta;

    internal Func<StopwatchAbstraction>? _createStopwatch = null;

    /// <summary>
    /// Name of target that identifies the project cache plugin run has just started.
    /// </summary>
    private const string CachePluginStartTarget = "_CachePluginRunStart";

    /// <summary>
    /// A cancellation token to signal the rendering thread that it should exit.
    /// </summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// The working directory when the build starts, to trim relative output paths.
    /// </summary>
    private readonly string _initialWorkingDirectory = Environment.CurrentDirectory;

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

    #region  Logger argument parsing and factory methods

    /// <summary>
    /// Creates a Terminal logger if possible, or a Console logger.
    /// </summary>
    /// <param name="args">Command line arguments for the logger configuration. Currently, only 'tl|terminallogger', 'v|verbosity', 'tlp|terminalloggerparameters', and 'clp|consoleloggerparameters' are supported.</param>
    public static ILogger CreateTerminalOrConsoleLogger(string[]? args = null)
    {
        (bool supportsAnsi, bool outputIsScreen, uint? originalConsoleMode) = NativeMethodsShared.QueryIsScreenAndTryEnableAnsiColorCodes();
        var (logger, _) = CreateTerminalOrConsoleLoggerWithForwarding(args, supportsAnsi, outputIsScreen, originalConsoleMode);
        return logger;
    }

    /// <summary>
    /// Creates a Terminal logger if possible, or a Console logger. If the created logger supports remote logging,
    /// also provides a ForwardingLoggerRecord to wrap it for forwarding.
    /// </summary>
    /// <param name="args">Command line arguments for the logger configuration. Currently, only 'tl|terminallogger', 'v|verbosity', 'tlp|terminalloggerparameters', and 'clp|consoleloggerparameters' are supported.</param>
    public static (ILogger, ForwardingLoggerRecord?) CreateTerminalOrConsoleLoggerWithForwarding(string[]? args = null)
    {
        (bool supportsAnsi, bool outputIsScreen, uint? originalConsoleMode) = NativeMethodsShared.QueryIsScreenAndTryEnableAnsiColorCodes();
        var (logger, forwardingLogger) = CreateTerminalOrConsoleLoggerWithForwarding(args, supportsAnsi, outputIsScreen, originalConsoleMode);
        return (logger, forwardingLogger);
    }

    internal static (ILogger, ForwardingLoggerRecord?) CreateTerminalOrConsoleLoggerWithForwarding(string[]? args, bool supportsAnsi, bool outputIsScreen, uint? originalConsoleMode)
    {
        LoggerVerbosity verbosity = LoggerVerbosity.Normal;
        string tlEnvVariable = Environment.GetEnvironmentVariable("MSBUILDTERMINALLOGGER") ?? string.Empty;
        string? tlArg = null;
        List<string> tlpArg = new();
        List<string> clpArg = new();
        string? verbosityArg = null;

        ILogger loggerToReturn;
        ForwardingLoggerRecord? forwardingLogger;

        if (args != null)
        {
            foreach (string arg in args)
            {
                var tlArgMatches = TerminalLoggerArgPattern.Matches(arg);
                if (tlArgMatches.Count > 0)
                {
                    // overriding, last one wins
                    tlArg = tlArgMatches[^1].Groups["value"].Value;
                }

                var verbosityArgMatches = VerbosityArgPattern.Matches(arg);
                if (verbosityArgMatches.Count > 0)
                {
                    // overriding, last one wins
                    verbosityArg = verbosityArgMatches[^1].Groups["value"].Value;
                }

                var tlpMatches = TerminalLoggerParametersArgPattern.Matches(arg);
                if (tlpMatches.Count > 0)
                {
                    // can be multiple, accumulate all
                    tlpArg.AddRange(tlpMatches.OfType<Match>().Select(m => m.Groups["value"].Value).Where(v => !string.IsNullOrEmpty(v)));
                }

                var clpMatches = ConsoleLoggerParametersArgPattern.Matches(arg);
                if (clpMatches.Count > 0)
                {
                    // can be multiple, accumulate all
                    clpArg.AddRange(clpMatches.OfType<Match>().Select(m => m.Groups["value"].Value).Where(v => !string.IsNullOrEmpty(v)));
                }
            }
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
        string effectiveValue =
             (tlArg, tlEnvVariable) switch
             {
                 (not null and not "", _) => tlArg,
                 (_, not null and not "") => tlEnvVariable,
                 _ => "auto",
             };

        bool isForced = IsTerminalLoggerEnabled(effectiveValue);
        bool isDisabled = IsTerminalLoggerDisabled(effectiveValue);
        string tlpArgString = string.Join(";", tlpArg);
        string clpArgString = string.Join(";", clpArg);

        // if forced, always use the Terminal Logger
        if (isForced)
        {
            loggerToReturn = new TerminalLogger(verbosity, originalConsoleMode) { Parameters = tlpArgString };
            forwardingLogger = TerminalLoggerForwardingRecord(loggerToReturn, tlpArgString, verbosity);
        }

        // If explicitly disabled, always use console logger
        else if (isDisabled)
        {
            NativeMethodsShared.RestoreConsoleMode(originalConsoleMode);
            loggerToReturn = new ConsoleLogger(verbosity) { Parameters = clpArgString };
            forwardingLogger = null;
        }

        // If not forced and system doesn't support terminal features, fall back to console logger
        else if (effectiveValue == "auto" && supportsAnsi && outputIsScreen)
        {
            loggerToReturn = new TerminalLogger(verbosity, originalConsoleMode) { Parameters = tlpArgString };
            forwardingLogger = TerminalLoggerForwardingRecord(loggerToReturn, tlpArgString, verbosity);
        }
        else
        {
            // otherwise the state only allows fallback to console logger
            NativeMethodsShared.RestoreConsoleMode(originalConsoleMode);
            loggerToReturn = new ConsoleLogger(verbosity) { Parameters = clpArgString };
            forwardingLogger = null;
        }

        return (loggerToReturn, forwardingLogger);

        static ForwardingLoggerRecord TerminalLoggerForwardingRecord(ILogger loggerToReturn, string? tlpArg, LoggerVerbosity verbosity)
        {
            var tlForwardingType = typeof(ForwardingTerminalLogger);
            LoggerDescription forwardingLoggerDescription = new LoggerDescription(tlForwardingType.FullName, tlForwardingType.Assembly.FullName, null, tlpArg, verbosity);
            return new ForwardingLoggerRecord(loggerToReturn, forwardingLoggerDescription);
        }
    }

    /// <summary>
    /// Checks if the given value indicates TerminalLogger should be enabled/forced.
    /// </summary>
    /// <param name="value">The value to check (from command line or environment variable).</param>
    /// <returns>True if the value indicates TerminalLogger should be enabled.</returns>
    private static bool IsTerminalLoggerEnabled(string? value) =>
        value is { Length: > 0 } &&
            (value.Equals("on", StringComparison.InvariantCultureIgnoreCase) ||
             value.Equals("true", StringComparison.InvariantCultureIgnoreCase));

    /// <summary>
    /// Checks if the given value indicates TerminalLogger should be disabled.
    /// </summary>
    /// <param name="value">The value to check (from command line or environment variable).</param>
    /// <returns>True if the value indicates TerminalLogger should be disabled.</returns>
    private static bool IsTerminalLoggerDisabled(string? value) =>
        value is { Length: > 0 } &&
            (value.Equals("off", StringComparison.InvariantCultureIgnoreCase) ||
             value.Equals("false", StringComparison.InvariantCultureIgnoreCase));

    #endregion

    #region INodeLogger implementation

    /// <inheritdoc/>
    public override LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Minimal;

    /// <inheritdoc/>
    public override string? Parameters { get; set; } = null;

    /// <inheritdoc/>
    public override void Initialize(IEventSource eventSource)
    {
        ParseParameters();
        base.Initialize(eventSource);
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
    public override void Shutdown()
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

    #region ProjectTrackingLoggerBase implementation
    
    public override bool NeedsTaskInputs => true;

    public override bool NeedsEvaluationPropertiesAndItems => true;

    public override bool UsesPerNodeData => true;

    /// <inheritdoc/>
    protected override TerminalBuildData CreateBuildData(BuildStartedEventArgs e)
    {
        return new TerminalBuildData(e.Timestamp);
    }

    /// <inheritdoc/>
    protected override EvalProjectInfo CreateEvalData(ProjectEvaluationFinishedEventArgs e)
    {
        (string? tfm, string? rid) = EnumerateEvalProperties(e.EnumerateProperties());
        return new EvalProjectInfo(e.ProjectFile!, tfm, rid);
    }

    /// <inheritdoc/>
    protected override EvalProjectInfo CreateSyntheticEvalDataForMetaproject(ProjectStartedEventArgs e)
    {
        (string? tfm, string? rid) = EnumerateEvalProperties(e.EnumerateProperties());
        return new EvalProjectInfo(e.ProjectFile!, tfm, rid);
    }

    private (string? tfm, string? rid) EnumerateEvalProperties(IEnumerable<PropertyData> properties)
    {
        string? tfm = null;
        string? rid = null;

        foreach (PropertyData property in properties)
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

        return (tfm, rid);
    }

    /// <inheritdoc/>
    protected override TerminalProjectInfo? CreateProjectData(EvalProjectInfo evalData, TerminalBuildData buildData, ProjectStartedEventArgs e)
    {
        // TL doesn't want to track projects that are part of a restore
        if (buildData.IsRestoring)
        {
            return null;
        }

        return new TerminalProjectInfo(evalData, _createStopwatch?.Invoke());
    }

    private TerminalNodeStatus? CreateNodeData(TargetStartedEventArgs e, TerminalProjectInfo projectData)
    {
        projectData.Stopwatch.Start();
        string projectFile = Path.GetFileNameWithoutExtension(e.ProjectFile);
        string targetName = e.TargetName;
        projectData.CurrentTarget = targetName;

        if (targetName == CachePluginStartTarget)
        {
            projectData.IsCachePluginProject = true;
            _hasUsedCache = true;
        }

        if (targetName == _testStartTarget)
        {
            targetName = "Testing";
            _testStartTime = _testStartTime == null
                ? e.Timestamp
                : e.Timestamp < _testStartTime
                    ? e.Timestamp : _testStartTime;
            projectData.IsTestProject = true;
        }

        return new TerminalNodeStatus(projectFile, projectData.TargetFramework, projectData.RuntimeIdentifier, targetName, projectData.Stopwatch);
    }


    #endregion

    #region Logger event overrides

    /// <inheritdoc/>
    protected override void OnBuildStarted(BuildStartedEventArgs e, TerminalBuildData buildData)
    {
        if (!_manualRefresh && _showNodesDisplay)
        {
            _refresher = new Thread(ThreadProc);
            _refresher.Name = "Terminal Logger Node Display Refresher";
            _refresher.Start();
        }

        if (Terminal.SupportsProgressReporting && Verbosity != LoggerVerbosity.Quiet)
        {
            Terminal.Write(AnsiCodes.SetProgressIndeterminate);
        }
    }

    /// <inheritdoc/>
    protected override void OnBuildFinished(BuildFinishedEventArgs e, IEnumerable<TerminalProjectInfo> projectInfos, TerminalBuildData buildData)
    {
        _cts.Cancel();
        _refresher?.Join();

        Terminal.BeginUpdate();
        try
        {
            if (Verbosity > LoggerVerbosity.Quiet)
            {
                string duration = (e.Timestamp - buildData.BuildStartTime).TotalSeconds.ToString("F1");
                string buildResult = GetBuildResultString(e.Succeeded, buildData.BuildErrorsCount, buildData.BuildWarningsCount);

                Terminal.WriteLine("");
                if (_testRunSummaries.Any())
                {
                    int total = _testRunSummaries.Sum(t => t.Total);
                    int failed = _testRunSummaries.Sum(t => t.Failed);
                    int passed = _testRunSummaries.Sum(t => t.Passed);
                    int skipped = _testRunSummaries.Sum(t => t.Skipped);
                    string testDuration = (_testStartTime != null && _testEndTime != null ? (_testEndTime - _testStartTime).Value.TotalSeconds : 0).ToString("F1");

                    bool colorizeFailed = failed > 0;
                    bool colorizePassed = passed > 0 && buildData.BuildErrorsCount == 0 && failed == 0;
                    bool colorizeSkipped = skipped > 0 && skipped == total && buildData.BuildErrorsCount == 0 && failed == 0;

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
                    RenderBuildSummary(buildData, projectInfos);
                }

                if (buildData?.RestoreFailed == true)
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

        _testRunSummaries.Clear();
        _testStartTime = null;
        _testEndTime = null;
    }

    private void RenderBuildSummary(TerminalBuildData buildData, IEnumerable<TerminalProjectInfo> projectInfos)
    {
        if (buildData.BuildErrorsCount == 0 && buildData.BuildWarningsCount == 0)
        {
            // No errors/warnings to display.
            return;
        }

        Terminal.WriteLine(ResourceUtilities.GetResourceString("BuildSummary"));

        foreach (TerminalProjectInfo project in projectInfos.Where(p => p.HasErrorsOrWarnings))
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

    /// <inheritdoc/>
    protected override void OnBuildCanceled(BuildCanceledEventArgs e)
    {
        RenderImmediateMessage(e.Message!);
    }

    /// <inheritdoc/>
    protected override void OnProjectStarted(ProjectStartedEventArgs e, EvalProjectInfo evalData, TerminalProjectInfo projectData, TerminalBuildData buildData)
    {
        // Handle restore case
        if (!buildData.IsRestoring && e.TargetNames == "Restore" && !buildData.RestoreFinished && e.BuildEventContext is not null)
        {
            buildData.RestoreContext = e.BuildEventContext.ProjectContextId;
            SetActiveNodeStatus(e, new TerminalNodeStatus(e.ProjectFile!, evalData.TargetFramework, evalData.RuntimeIdentifier, "Restore", projectData.Stopwatch));
        }
    }

    /// <inheritdoc/>
    protected override void OnProjectFinished(ProjectFinishedEventArgs e, TerminalProjectInfo? projectData, TerminalBuildData buildData)
    {
        var buildEventContext = e.BuildEventContext;
        if (buildEventContext is null)
        {
            return;
        }

        // Mark node idle until something uses it again
        if (!buildData.IsRestoring)
        {
            YieldNode(e);
        }


        if (projectData != null)
        {
            projectData.Succeeded = e.Succeeded;
            projectData.Stopwatch.Stop();

            // Handle restore completion
            if (buildEventContext.ProjectContextId == buildData.RestoreContext)
            {
                buildData.RestoreContext = null;
                buildData.RestoreFinished = true;
                buildData.RestoreFailed = !e.Succeeded;
                OnRestoreFinished(e, projectData, buildData);
            }

            // In quiet mode, only show projects with errors or warnings.
            if (Verbosity == LoggerVerbosity.Quiet && !projectData.HasErrorsOrWarnings)
            {
                return;
            }

            lock (_renderLock)
            {
                Terminal.BeginUpdate();
                try
                {
                    EraseNodesDisplay();

                    string duration = projectData.Stopwatch.ElapsedSeconds.ToString("F1");
                    ReadOnlyMemory<char>? outputPath = projectData.OutputPath;

                    // Build result. One of 'failed', 'succeeded with warnings', or 'succeeded' depending on the build result and diagnostic messages
                    // reported during build.
                    string buildResult = GetBuildResultString(projectData.Succeeded, projectData.ErrorCount, projectData.WarningCount);

                    // If this was a notable project build, we print it as completed only if it's produced an output or warnings/error.
                    // If this is a test project, print it always, so user can see either a success or failure, otherwise success is hidden
                    // and it is hard to see if project finished, or did not run at all.
                    // In quiet mode, we show the project header if there are errors/warnings (already checked above).
                    if (projectData.OutputPath is not null || projectData.BuildMessages is not null || projectData.IsTestProject)
                    {
                        // Show project build complete and its output
                        string projectFinishedHeader = GetProjectFinishedHeader(projectData, buildResult, duration);
                        Terminal.Write(projectFinishedHeader);

                        // Print the output path as a link if we have it.
                        if (outputPath is { } outputPathSpan)
                        {
                            (string projectDisplayPath, Uri? urlLink) = DetermineOutputPathToRender(outputPathSpan, _initialWorkingDirectory.AsMemory(), projectData.SourceRoot);
                            Terminal.WriteLine(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ProjectFinished_OutputPath", CreateLink(urlLink, projectDisplayPath.ToString())));
                        }
                        else
                        {
                            Terminal.WriteLine(string.Empty);
                        }
                    }

                    // Print diagnostic output under the Project -> Output line.
                    if (projectData.BuildMessages is not null)
                    {
                        foreach (TerminalBuildMessage buildMessage in projectData.BuildMessages)
                        {
                            Terminal.WriteLine($"{DoubleIndentation}{buildMessage.Message}");
                        }
                    }

                    // Track errors and warnings in build data
                    if (buildData != null)
                    {
                        buildData.BuildErrorsCount += projectData.ErrorCount;
                        buildData.BuildWarningsCount += projectData.WarningCount;
                    }

                    if (_showNodesDisplay && Verbosity > LoggerVerbosity.Quiet)
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

    private void OnRestoreFinished(ProjectFinishedEventArgs e, TerminalProjectInfo? projectData, TerminalBuildData buildData)
    {
        if (Verbosity > LoggerVerbosity.Quiet && projectData != null)
        {
            string duration = projectData.Stopwatch.ElapsedSeconds.ToString("F1");
            string buildResult = GetBuildResultString(projectData.Succeeded, projectData.ErrorCount, projectData.WarningCount);

            if (e.Succeeded)
            {
                if (projectData.HasErrorsOrWarnings)
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
        ReadOnlySpan<char> projectDisplayPathSpan = outputPathSpan;
        ReadOnlySpan<char> workingDirectorySpan = workingDir.Span;
        
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
            ReadOnlySpan<char> relativePathFromWorkingDirToSourceRoot = Path.GetRelativePath(workingDirectorySpan.ToString(), sourceRootSpan.ToString()).AsSpan();
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

    /// <inheritdoc/>
    protected override void OnTargetStarted(TargetStartedEventArgs e, TerminalProjectInfo projectData, TerminalBuildData buildData)
    {
        if (!buildData.IsRestoring)
        {
            TerminalNodeStatus? nodeData = CreateNodeData(e, projectData);
            if (nodeData != null)
            {
                SetActiveNodeStatus(e, nodeData);
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnTargetFinished(TargetFinishedEventArgs e, TerminalProjectInfo? projectData, TerminalBuildData buildData)
    {
        // For cache plugin projects which result in a cache hit, ensure the output path is set
        // to the item spec corresponding to the GetTargetPath target upon completion.
        var targetOutputs = e.TargetOutputs;
        if (projectData is null || targetOutputs is null)
        {
            return;
        }

        if (_hasUsedCache && e.TargetName == "GetTargetPath" && projectData.IsCachePluginProject)
        {
            foreach (ITaskItem output in targetOutputs)
            {
                projectData.OutputPath = output.ItemSpec.AsMemory();
                break;
            }
        }
        else if (e.TargetName == "InitializeSourceRootMappedPaths" && projectData.SourceRoot is null)
        {
            projectData.SourceRoot =
                (targetOutputs as IEnumerable<ITaskItem>)?
                .FirstOrDefault(root => !string.IsNullOrEmpty(root.GetMetadata("SourceControl")))
                ?.ItemSpec.AsMemory();
        }
    }

    /// <inheritdoc/>
    protected override void OnTaskStarted(TaskStartedEventArgs e, TerminalProjectInfo projectData, TerminalBuildData buildData)
    {
        if (!buildData.IsRestoring && e.BuildEventContext is not null && e.TaskName == MSBuildTaskName)
        {
            // This will yield the node, so preemptively mark it idle
            YieldNode(e);

            projectData.Stopwatch.Stop();
        }
    }

    /// <inheritdoc/>
    protected override void OnTaskFinished(TaskFinishedEventArgs e, TerminalProjectInfo projectData, TerminalBuildData buildData)
    {
        var buildEventContext = e.BuildEventContext;
        if (!buildData.IsRestoring && buildEventContext is not null && e.TaskName == MSBuildTaskName)
        {
            projectData.Stopwatch.Start();

            string projectFile = Path.GetFileNameWithoutExtension(e.ProjectFile);
            string targetName = projectData.CurrentTarget ?? "";
            TerminalNodeStatus nodeStatus = new(projectFile, projectData.TargetFramework, projectData.RuntimeIdentifier, GetDisplayTargetName(targetName), projectData.Stopwatch);
            SetActiveNodeStatus(e, nodeStatus);
        }
    }

    /// <inheritdoc/>
    protected override void OnMessageRaised(BuildMessageEventArgs e, TerminalProjectInfo? projectData, TerminalBuildData buildData)
    {
        string? message = e.Message;

        if (message is not null && e.Importance == MessageImportance.High)
        {
            var hasProject = projectData != null;

            // Detect project output path by matching high-importance messages against the "$(MSBuildProjectName) -> ..."
            // pattern used by the CopyFilesToOutputDirectory target.
            int index = message.IndexOf(FilePathPattern, StringComparison.Ordinal);
            if (index > 0)
            {
                var projectFileName = Path.GetFileName(e.ProjectFile.AsSpan());
                if (!projectFileName.IsEmpty &&
                    message.AsSpan().StartsWith(Path.GetFileNameWithoutExtension(projectFileName)) && hasProject && projectData != null)
                {
                    ReadOnlyMemory<char> outputPath = e.Message.AsMemory().Slice(index + 4);
                    projectData.OutputPath = outputPath;
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

            if (hasProject && projectData != null && projectData.IsTestProject)
            {
                var node = GetNodeDataForEvent(e);
                // Consumes test update messages produced by VSTest and MSTest runner.
                if (e is IExtendedBuildEventArgs extendedMessage)
                {
                    switch (extendedMessage.ExtendedType)
                    {
                        case "TLTESTPASSED":
                            {
                                if (node != default)
                                {
                                    string indicator = extendedMessage.ExtendedMetadata!["localizedResult"]!;
                                    string displayName = extendedMessage.ExtendedMetadata!["displayName"]!;

                                    TerminalNodeStatus? status = new TerminalNodeStatus(node.Project, node.TargetFramework, node.RuntimeIdentifier, TerminalColor.Green, indicator, displayName, projectData.Stopwatch);
                                    if (e.BuildEventContext != null)
                                    {
                                        SetActiveNodeStatus(e, status);
                                    }
                                }

                                break;
                            }

                        case "TLTESTSKIPPED":
                            {
                                if (node != default)
                                {
                                    string indicator = extendedMessage.ExtendedMetadata!["localizedResult"]!;
                                    string displayName = extendedMessage.ExtendedMetadata!["displayName"]!;
                                    TerminalNodeStatus? status = new TerminalNodeStatus(node.Project, node.TargetFramework, node.RuntimeIdentifier, TerminalColor.Yellow, indicator, displayName, projectData.Stopwatch);
                                    if (e.BuildEventContext != null)
                                    {
                                        SetActiveNodeStatus(e, status);
                                    }
                                }
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

                if (hasProject && projectData != null)
                {
                    projectData.AddBuildMessage(TerminalMessageSeverity.Message, FormatInformationalMessage(e));
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

    /// <inheritdoc/>
    protected override void OnWarningRaised(BuildWarningEventArgs e, TerminalProjectInfo? projectData, TerminalBuildData buildData)
    {
        // auth provider messages are 'global' in nature and should be a) immediate reported, and b) not re-reported in the summary.
        if (IsAuthProviderMessage(e.Message))
        {
            RenderImmediateMessage(FormatWarningMessage(e, Indentation));
            return;
        }

        if (e.BuildEventContext is not null
            && projectData != null)
        {
            // If the warning is not a 'global' auth provider message, but is immediate, we render it immediately
            // but we don't early return so that the project also tracks it.
            if (IsImmediateWarning(e.Code) && Verbosity > LoggerVerbosity.Quiet)
            {
                RenderImmediateMessage(FormatWarningMessage(e, Indentation));
            }

            // This is the general case - _most_ warnings are not immediate, so we add them to the project summary
            // and display them in the per-project and final summary.
            // In quiet mode, we still accumulate so they can be shown in project-grouped form later.
            projectData.AddBuildMessage(TerminalMessageSeverity.Warning, FormatWarningMessage(e, TripleIndentation));
        }
        else
        {
            // It is necessary to display warning messages reported by MSBuild,
            // even if it's not tracked in projects collection or the verbosity is Quiet.
            // The idea here (similar to the implementation in ErrorRaised) is that
            // even in Quiet scenarios we need to show warnings/errors, even if not in
            // full project-tree view
            RenderImmediateMessage(FormatWarningMessage(e, Indentation));
            buildData.BuildWarningsCount++;
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

    /// <inheritdoc/>
    protected override void OnErrorRaised(BuildErrorEventArgs e, TerminalProjectInfo? projectData, TerminalBuildData buildData)
    {
        if (projectData != null)
        {
            // Always accumulate errors in the project, even in quiet mode, so they can be shown
            // in project-grouped form later.
            projectData.AddBuildMessage(TerminalMessageSeverity.Error, FormatErrorMessage(e, TripleIndentation));
        }
        else
        {
            // It is necessary to display error messages reported by MSBuild, even if it's not tracked in projects collection or the verbosity is Quiet.
            // For nicer formatting, any messages from the engine we strip the file portion from.
            bool hasMSBuildPlaceholderLocation = e.File.Equals("MSBUILD", StringComparison.Ordinal);
            RenderImmediateMessage(FormatErrorMessage(e, Indentation, requireFileAndLinePortion: !hasMSBuildPlaceholderLocation));
            buildData.BuildErrorsCount++;
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
        int count = 0;
        while (!_cts.Token.WaitHandle.WaitOne(1_000 / 30))
        {
            count++;
            lock (_renderLock)
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

        EraseNodesDisplay();
    }

    /// <summary>
    /// Render Nodes section.
    /// It shows what all build nodes do.
    /// </summary>
    internal void DisplayNodes(bool updateSize = true)
    {
        int width = updateSize ? Terminal.Width : _currentFrame.Width;
        int height = updateSize ? Terminal.Height : _currentFrame.Height;
        TerminalNodesFrame newFrame = new TerminalNodesFrame(GetAllNodeData(), width: width, height: height);

        // Do not render delta but clear everything if Terminal width or height have changed.
        if (newFrame.Width != _currentFrame.Width || newFrame.Height != _currentFrame.Height)
        {
            EraseNodesDisplay();
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
    private void EraseNodesDisplay()
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
    /// Returns the display name for the given target.
    /// </summary>
    /// <remarks>
    /// This is used to map internal target names (like _TestRunStart) to user-friendly names (like Testing).
    /// </remarks>
    private static string GetDisplayTargetName(string targetName)
    {
        return targetName == _testStartTarget ? "Testing" : targetName;
    }

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
        lock (_renderLock)
        {
            // Calling erase helps to clear the screen before printing the message
            // The immediate output will not overlap with node status reporting
            EraseNodesDisplay();
            Terminal.WriteLine(message);
        }
    }

    // NodeIndexForContext is now inherited from base class

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

    private string FormatWarningMessage(BuildWarningEventArgs e, string indent) => FormatEventMessage(
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

    private string FormatInformationalMessage(BuildMessageEventArgs e) => FormatEventMessage(
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

    /// <summary>
    /// Renders message with just code/category/message data.
    /// No file data is included. This can be used for immediate/one-time
    /// messages that lack a specific project context, such as the .NET
    /// SDK's 'preview version' message, while not removing the code.
    /// </summary>
    private string FormatSimpleMessageWithoutFileData(BuildMessageEventArgs e, string indent) => FormatEventMessage(
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

    private string FormatErrorMessage(BuildErrorEventArgs e, string indent, bool requireFileAndLinePortion = true) => FormatEventMessage(
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

    #region Regex Patterns
    // Regex patterns for command line argument parsing
    private const string s_terminalLoggerArgPattern = @"(?:/|-|--)(?:tl|terminallogger):(?'value'on|off|true|false|auto)";
    private const string s_verbosityArgPattern = @"(?:/|-|--)(?:v|verbosity):(?'value'\w+)";
    private const string s_terminalLoggerParametersArgPattern = @"(?:/|-|--)(?:tlp|terminalloggerparameters):(?'value'.+)";
    private const string s_consoleLoggerParametersArgPattern = @"(?:/|-|--)(?:clp|consoleloggerparameters):(?'value'.+)";

#if NET
    [GeneratedRegex(s_terminalLoggerArgPattern, RegexOptions.IgnoreCase)]
    private static partial Regex TerminalLoggerArgPattern { get; }
    [GeneratedRegex(s_verbosityArgPattern, RegexOptions.IgnoreCase)]
    private static partial Regex VerbosityArgPattern { get; }
    [GeneratedRegex(s_terminalLoggerParametersArgPattern, RegexOptions.IgnoreCase)]
    private static partial Regex TerminalLoggerParametersArgPattern { get; }
    [GeneratedRegex(s_consoleLoggerParametersArgPattern, RegexOptions.IgnoreCase)]
    private static partial Regex ConsoleLoggerParametersArgPattern { get; }
#else
    private static Regex TerminalLoggerArgPattern { get; } = new Regex(s_terminalLoggerArgPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static Regex VerbosityArgPattern { get; } = new Regex(s_verbosityArgPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static Regex TerminalLoggerParametersArgPattern { get; } = new Regex(s_terminalLoggerParametersArgPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static Regex ConsoleLoggerParametersArgPattern { get; } = new Regex(s_consoleLoggerParametersArgPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
#endif
    #endregion
}
