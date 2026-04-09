// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Framework.Telemetry;

/// <summary>
/// Classifies where a crash originated.
/// </summary>
internal enum CrashOriginKind
{
    /// <summary>
    /// The origin could not be determined (e.g., no stack trace available).
    /// </summary>
    Unknown,

    /// <summary>
    /// The crash originated in MSBuild's own code (Microsoft.Build.* namespaces).
    /// </summary>
    MSBuild,

    /// <summary>
    /// The crash originated in third-party or other Microsoft code running
    /// in the MSBuild process (e.g., VS telemetry SDK, NuGet, Roslyn).
    /// </summary>
    ThirdParty,
}

/// <summary>
/// Classifies the exit type / category of the crash for telemetry.
/// Maps to <c>MSBuildApp.ExitType</c> values plus additional categories
/// used by the unhandled exception handler and BuildManager.
/// </summary>
internal enum CrashExitType
{
    /// <summary>
    /// Default / unknown exit type.
    /// </summary>
    Unknown,

    /// <summary>
    /// A logger aborted the build.
    /// </summary>
    LoggerAbort,

    /// <summary>
    /// A logger failed unexpectedly.
    /// </summary>
    LoggerFailure,

    /// <summary>
    /// The build stopped unexpectedly, for example,
    /// because a child died or hung.
    /// </summary>
    Unexpected,

    /// <summary>
    /// A project cache failed unexpectedly.
    /// </summary>
    ProjectCacheFailure,

    /// <summary>
    /// The client for MSBuild server failed unexpectedly, for example,
    /// because the server process died or hung.
    /// </summary>
    MSBuildClientFailure,

    /// <summary>
    /// An exception reached the unhandled exception handler.
    /// </summary>
    UnhandledException,

    /// <summary>
    /// An exception occurred during EndBuild in BuildManager.
    /// </summary>
    EndBuildFailure,

    /// <summary>
    /// An OutOfMemoryException occurred.
    /// </summary>
    OutOfMemory,

    /// <summary>
    /// EndBuild is stuck waiting for submissions or nodes to complete.
    /// Emitted periodically during the hang so diagnostics are available
    /// even if the hang never resolves.
    /// </summary>
    EndBuildHang,
}

/// <summary>
/// Telemetry data for MSBuild crashes and unhandled exceptions.
/// </summary>
internal class CrashTelemetry : TelemetryBase, IActivityTelemetryDataHolder
{
    public override string EventName => "crash";

    /// <summary>
    /// The full name of the exception type (e.g., "System.NullReferenceException").
    /// </summary>
    public string? ExceptionType { get; set; }

    /// <summary>
    /// Inner exception type, if any.
    /// </summary>
    public string? InnerExceptionType { get; set; }

    /// <summary>
    /// The exit type / category of the crash.
    /// </summary>
    public CrashExitType ExitType { get; set; }

    /// <summary>
    /// Whether the exception is classified as critical (OOM, StackOverflow, AccessViolation, etc.).
    /// </summary>
    public bool? IsCritical { get; set; }

    /// <summary>
    /// Whether the crash came from the unhandled exception handler (true) or a catch block (false).
    /// </summary>
    public bool IsUnhandled { get; set; }

    /// <summary>
    /// SHA-256 hash of the stack trace, for bucketing without sending PII.
    /// </summary>
    public string? StackHash { get; set; }

    /// <summary>
    /// The method at the top of the call stack where the exception originated.
    /// When the top frame is a known throw-helper (e.g., ErrorUtilities.ThrowInternalError),
    /// this still contains that frame for backward compatibility.
    /// </summary>
    public string? StackTop { get; set; }

    /// <summary>
    /// The first meaningful caller frame, skipping known throw-helper methods.
    /// For example, if the top frame is <c>ErrorUtilities.ThrowInternalError</c>,
    /// this will contain the frame that called it — which is what you actually need for triage.
    /// Null if the stack trace has no frame beyond the throw-helper, or if the top frame
    /// is not a throw-helper (in which case <see cref="StackTop"/> already has the meaningful frame).
    /// </summary>
    public string? StackCaller { get; set; }

    /// <summary>
    /// The full exception stack trace with file paths sanitized to remove PII.
    /// Each frame is preserved so that the complete call chain is visible in telemetry,
    /// unlike <see cref="StackTop"/> which only captures one frame.
    /// Truncated to <see cref="MaxStackTraceLength"/> characters.
    /// </summary>
    public string? FullStackTrace { get; set; }

    /// <summary>
    /// Maximum number of characters to include from the sanitized stack trace.
    /// </summary>
    internal const int MaxStackTraceLength = 4096;

    /// <summary>
    /// Compiled regex pattern for matching file/directory paths that may contain PII.
    /// Matches Windows absolute paths (X:\...), UNC paths (\\server\share\...), and Unix paths (/...).
    /// Shared between <see cref="TruncateMessage"/> and <see cref="SanitizeFilePathsInText"/>.
    /// </summary>
    private static readonly Regex FilePathPattern = new(
        @"(?:[A-Za-z]:\\|\\\\|/)(?:[^\s""'<>|*?\r\n]+)",
        RegexOptions.Compiled);

    /// <summary>
    /// A prefix of the exception message, truncated and sanitized to avoid PII.
    /// Particularly useful for <c>InternalErrorException</c> where the message text
    /// identifies the specific assertion that failed.
    /// </summary>
    public string? ExceptionMessage { get; set; }

    /// <summary>
    /// The HResult from the exception, if available.
    /// </summary>
    public int? HResult { get; set; }

    /// <summary>
    /// Version of MSBuild.
    /// </summary>
    public string? BuildEngineVersion { get; set; }

    /// <summary>
    /// Framework name (.NET 10.0, .NET Framework 4.7.2, etc.).
    /// </summary>
    public string? BuildEngineFrameworkName { get; set; }

    /// <summary>
    /// Host in which MSBuild is running (VS, VSCode, CLI, etc.).
    /// </summary>
    public string? BuildEngineHost { get; set; }

    /// <summary>
    /// The origin classification of the crash.
    /// Helps distinguish crashes in MSBuild's own code from crashes in dependencies
    /// that happen to run in the MSBuild process.
    /// </summary>
    public CrashOriginKind CrashOrigin { get; set; }

    /// <summary>
    /// The top-level namespace from the faulting stack frame (e.g., "Microsoft.Build",
    /// "Microsoft.VisualStudio.RemoteControl"). Useful for triage without revealing PII.
    /// </summary>
    public string? CrashOriginNamespace { get; set; }

    /// <summary>
    /// The deepest inner exception type in the exception chain.
    /// For wrapper exceptions like <see cref="System.TypeInitializationException"/>,
    /// this reveals the actual root cause exception type.
    /// </summary>
    public string? InnermostExceptionType { get; set; }

    /// <summary>
    /// The sanitized stack trace of the inner exception, if any.
    /// For wrapper exceptions like <c>InternalLoggerException</c>, the outer stack trace
    /// only shows MSBuild's logging infrastructure. The inner stack trace reveals the
    /// actual faulting component (e.g., a VS logger that threw <c>ObjectDisposedException</c>).
    /// Truncated to <see cref="MaxStackTraceLength"/> characters.
    /// </summary>
    public string? InnerExceptionStackTrace { get; set; }

    /// <summary>
    /// A prefix of the inner exception's message, truncated and sanitized to avoid PII.
    /// For <c>ObjectDisposedException</c>, this includes the disposed object name
    /// which identifies the specific component that was prematurely disposed.
    /// </summary>
    public string? InnerExceptionMessage { get; set; }

    /// <summary>
    /// The type name of the build event that was being logged when a logger exception occurred.
    /// Only populated when the exception is an <c>InternalLoggerException</c> with an associated
    /// <c>BuildEventArgs</c>. Example values: "BuildFinishedEventArgs", "BuildMessageEventArgs".
    /// Helps narrow down the code path in the faulting logger.
    /// </summary>
    public string? LoggerEventType { get; set; }

    /// <summary>
    /// Working set of the MSBuild process at crash time, in MB.
    /// Helps diagnose OOM and memory-pressure crashes.
    /// </summary>
    public long? ProcessWorkingSetMB { get; set; }

    /// <summary>
    /// Approximate percentage of physical memory in use at crash time (0-100).
    /// Available on Windows (.NET Framework via GlobalMemoryStatusEx) and
    /// .NET Core (via GC.GetGCMemoryInfo).
    /// </summary>
    public int? MemoryLoadPercent { get; set; }

    /// <summary>
    /// The name of the thread on which the crash occurred.
    /// Helps identify whether the crash was on the main thread, a worker thread,
    /// a node communication thread, etc.
    /// </summary>
    public string? CrashThreadName { get; set; }

    // --- EndBuild hang diagnostic properties (populated only for ExitType == EndBuildHang) ---

    /// <summary>
    /// Which wait point EndBuild is stuck at (e.g. "WaitingForSubmissions", "WaitingForNodes").
    /// </summary>
    public string? EndBuildWaitPhase { get; set; }

    /// <summary>
    /// How long EndBuild has been waiting, in milliseconds.
    /// </summary>
    public long? EndBuildWaitDurationMs { get; set; }

    /// <summary>
    /// Number of submissions still in the pending dictionary.
    /// </summary>
    public int? PendingSubmissionCount { get; set; }

    /// <summary>
    /// Number of submissions that have a BuildResult but LoggingCompleted is false.
    /// These submissions are the ones blocking EndBuild.
    /// </summary>
    public int? SubmissionsWithResultNoLogging { get; set; }

    /// <summary>
    /// Whether a thread exception has been recorded on the BuildManager.
    /// </summary>
    public bool? ThreadExceptionRecorded { get; set; }

    /// <summary>
    /// Number of unmatched ProjectStarted events (no corresponding ProjectFinished).
    /// </summary>
    public int? UnmatchedProjectStartedCount { get; set; }

    /// <summary>
    /// State of the logging service (Instantiated, Initialized, ShuttingDown, Shutdown).
    /// Helps determine if the logging pipeline is still alive and processing events.
    /// </summary>
    public string? LoggingServiceState { get; set; }

    /// <summary>
    /// Number of events queued in the logging service's async event queue.
    /// A large number indicates the logging pipeline is backed up.
    /// </summary>
    public int? LoggingEventQueueDepth { get; set; }

    /// <summary>
    /// Whether the BuildManager has started shutting down.
    /// </summary>
    public bool? IsShuttingDown { get; set; }

    /// <summary>
    /// Whether the execution cancellation token has been triggered.
    /// </summary>
    public bool? IsCancellationRequested { get; set; }

    /// <summary>
    /// Number of pending work items in the BuildManager's work queue.
    /// The OnProjectFinished handler posts to this queue, so a blocked queue
    /// prevents logging completion.
    /// </summary>
    public int? WorkQueueDepth { get; set; }

    /// <summary>
    /// Per-submission diagnostic summary for pending submissions.
    /// Format: "id:Started:HasResult:HasException:LoggingCompleted" separated by semicolons.
    /// </summary>
    public string? SubmissionDetails { get; set; }

    /// <summary>
    /// Semicolon-separated list of registered logger type names.
    /// Identifies which loggers could be blocking the logging pipeline.
    /// </summary>
    public string? RegisteredLoggerTypeNames { get; set; }

    /// <summary>
    /// Comma-separated list of active node IDs that have not yet shut down.
    /// Populated during WaitingForNodes phase to identify which nodes are stuck.
    /// </summary>
    public string? ActiveNodeIds { get; set; }

    /// <summary>
    /// Whether node reuse is enabled. When true, nodes are told to go idle
    /// rather than exit, which changes the shutdown pathway.
    /// </summary>
    public bool? EnableNodeReuse { get; set; }

    /// <summary>
    /// Per-node diagnostic summary for active nodes.
    /// Format: "nodeId:configurationId:projectFile" separated by semicolons.
    /// Shows what each stuck node was last working on.
    /// </summary>
    public string? ActiveNodeDetails { get; set; }

    /// <summary>
    /// Diagnostic state of the project cache configuration when Project is unexpectedly null.
    /// Format: "configId:projectFileName:IsLoaded:IsCached:WasGeneratedByNode:IsVsScenario:HasGlobalPlugins".
    /// </summary>
    public string? ProjectCacheState { get; set; }

    // --- Build state diagnostic properties (help diagnose the context of the crash) ---

    /// <summary>
    /// Whether MSBuild is running standalone (CLI) or hosted (VS, etc.).
    /// </summary>
    public bool? IsStandaloneExecution { get; set; }

    /// <summary>
    /// Maximum number of build nodes configured (BuildParameters.MaxNodeCount).
    /// Helps determine if out-of-proc nodes were in use.
    /// </summary>
    public int? MaxNodeCount { get; set; }

    /// <summary>
    /// Number of currently active build nodes at crash time.
    /// </summary>
    public int? ActiveNodeCount { get; set; }

    /// <summary>
    /// Number of active build submissions at crash time.
    /// </summary>
    public int? SubmissionCount { get; set; }

    /// <summary>
    /// The original exception, kept for passing to <c>FaultEvent</c>.
    /// Not serialized to telemetry properties.
    /// </summary>
    internal Exception? Exception { get; set; }

    /// <summary>
    /// Populates this instance from an exception.
    /// </summary>
    public void PopulateFromException(Exception exception)
    {
        Exception = exception;
        ExceptionType = exception.GetType().FullName;
        InnerExceptionType = exception.InnerException?.GetType().FullName;
        InnermostExceptionType = GetInnermostException(exception)?.GetType().FullName;
        HResult = exception.HResult;
        ExceptionMessage = TruncateMessage(exception.Message);
        StackHash = ComputeStackHash(exception);
        StackTop = ExtractStackTop(exception);
        StackCaller = ExtractStackCaller(exception);
        FullStackTrace = ExtractFullStackTrace(exception);
        CrashOriginNamespace = ExtractOriginNamespace(exception);
        CrashOrigin = ClassifyOrigin(CrashOriginNamespace);
        CrashThreadName = System.Threading.Thread.CurrentThread.Name;
        PopulateInnerExceptionDetails(exception);
        PopulateMemoryStats();
    }

    /// <summary>
    /// Captures inner exception details that help diagnose wrapper exceptions.
    /// For <c>InternalLoggerException</c>, the inner exception identifies the actual
    /// faulting logger, and the <c>BuildEventArgs</c> property identifies what event
    /// was being delivered.
    /// </summary>
    private void PopulateInnerExceptionDetails(Exception exception)
    {
        Exception? inner = exception.InnerException;
        if (inner is not null)
        {
            InnerExceptionStackTrace = ExtractFullStackTrace(inner);
            InnerExceptionMessage = TruncateMessage(inner.Message);
        }

        // Extract BuildEventArgs type from InternalLoggerException without taking
        // a direct dependency on Microsoft.Build.dll. The property is public.
        try
        {
            var buildEventArgsProp = exception.GetType().GetProperty("BuildEventArgs");
            if (buildEventArgsProp?.GetValue(exception) is object eventArgs)
            {
                LoggerEventType = eventArgs.GetType().Name;
            }
        }
        catch
        {
            // Best effort: reflection failures must not cause a secondary failure.
        }
    }

    /// <summary>
    /// Captures memory usage stats at the time of the crash.
    /// Best-effort: failures are silently ignored.
    /// </summary>
    private void PopulateMemoryStats()
    {
        try
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            ProcessWorkingSetMB = process.WorkingSet64 / (1024 * 1024);
        }
        catch
        {
            // Best effort.
        }

        try
        {
#if NETFRAMEWORK
            NativeMethods.MemoryStatus? memoryStatus = NativeMethods.GetMemoryStatus();
            if (memoryStatus != null)
            {
                MemoryLoadPercent = (int)memoryStatus.MemoryLoad;
            }
#else
            // On .NET Core, GC.GetGCMemoryInfo() provides the total available memory
            // to the GC, which we use to compute an approximate memory load percentage.
            // This helps diagnose OOM and memory-pressure crashes on Linux/macOS where
            // GlobalMemoryStatusEx is not available.
            GCMemoryInfo gcMemInfo = System.GC.GetGCMemoryInfo();
            long totalAvailable = gcMemInfo.TotalAvailableMemoryBytes;
            if (totalAvailable > 0)
            {
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                MemoryLoadPercent = (int)(((double)process.WorkingSet64 / totalAvailable) * 100);
            }
#endif
        }
        catch
        {
            // Best effort.
        }
    }

    /// <summary>
    /// Create a list of properties sent to VS telemetry as activity tags.
    /// </summary>
    public Dictionary<string, object> GetActivityProperties()
    {
        Dictionary<string, object> telemetryItems = new(10);

        AddIfNotNull(ExceptionType);
        AddIfNotNull(InnerExceptionType);
        if (ExitType != CrashExitType.Unknown)
        {
            telemetryItems.Add(nameof(ExitType), ExitType.ToString());
        }
        AddIfNotNull(IsCritical);
        AddIfNotNull(IsUnhandled);
        AddIfNotNull(StackHash);
        AddIfNotNull(StackTop);
        AddIfNotNull(StackCaller);
        AddIfNotNull(FullStackTrace);
        AddIfNotNull(ExceptionMessage);
        AddIfNotNull(HResult);
        AddIfNotNull(BuildEngineVersion);
        AddIfNotNull(BuildEngineFrameworkName);
        AddIfNotNull(BuildEngineHost);
        if (CrashOrigin != CrashOriginKind.Unknown)
        {
            telemetryItems.Add(nameof(CrashOrigin), CrashOrigin.ToString());
        }
        AddIfNotNull(CrashOriginNamespace);
        AddIfNotNull(CrashThreadName);
        AddIfNotNull(InnermostExceptionType);
        AddIfNotNull(InnerExceptionStackTrace);
        AddIfNotNull(InnerExceptionMessage);
        AddIfNotNull(LoggerEventType);
        AddIfNotNull(ProcessWorkingSetMB);
        AddIfNotNull(MemoryLoadPercent);

        // EndBuild hang diagnostic properties
        AddIfNotNull(EndBuildWaitPhase);
        AddIfNotNull(EndBuildWaitDurationMs);
        AddIfNotNull(PendingSubmissionCount);
        AddIfNotNull(SubmissionsWithResultNoLogging);
        AddIfNotNull(ThreadExceptionRecorded);
        AddIfNotNull(UnmatchedProjectStartedCount);
        AddIfNotNull(LoggingServiceState);
        AddIfNotNull(LoggingEventQueueDepth);
        AddIfNotNull(IsShuttingDown);
        AddIfNotNull(IsCancellationRequested);
        AddIfNotNull(WorkQueueDepth);
        AddIfNotNull(SubmissionDetails);
        AddIfNotNull(RegisteredLoggerTypeNames);
        AddIfNotNull(ActiveNodeIds);
        AddIfNotNull(EnableNodeReuse);
        AddIfNotNull(ActiveNodeDetails);
        AddIfNotNull(ProjectCacheState);

        // Build state diagnostic properties
        AddIfNotNull(IsStandaloneExecution);
        AddIfNotNull(MaxNodeCount);
        AddIfNotNull(ActiveNodeCount);
        AddIfNotNull(SubmissionCount);

        return telemetryItems;

        void AddIfNotNull(object? value, [CallerArgumentExpression(nameof(value))] string key = "")
        {
            if (value is not null)
            {
                telemetryItems.Add(key, value);
            }
        }
    }

    public override IDictionary<string, string> GetProperties()
    {
        var properties = new Dictionary<string, string>();

        AddIfNotNull(ExceptionType);
        AddIfNotNull(InnerExceptionType);
        if (ExitType != CrashExitType.Unknown)
        {
            AddIfNotNull(ExitType.ToString(), nameof(ExitType));
        }
        AddIfNotNull(IsCritical?.ToString(), nameof(IsCritical));
        AddIfNotNull(IsUnhandled.ToString(), nameof(IsUnhandled));
        AddIfNotNull(StackHash);
        AddIfNotNull(StackTop);
        AddIfNotNull(StackCaller);
        AddIfNotNull(FullStackTrace);
        AddIfNotNull(ExceptionMessage);
        AddIfNotNull(HResult?.ToString(), nameof(HResult));
        AddIfNotNull(BuildEngineVersion);
        AddIfNotNull(BuildEngineFrameworkName);
        AddIfNotNull(BuildEngineHost);
        if (CrashOrigin != CrashOriginKind.Unknown)
        {
            AddIfNotNull(CrashOrigin.ToString(), nameof(CrashOrigin));
        }
        AddIfNotNull(CrashOriginNamespace);
        AddIfNotNull(CrashThreadName);
        AddIfNotNull(InnermostExceptionType);
        AddIfNotNull(InnerExceptionStackTrace);
        AddIfNotNull(InnerExceptionMessage);
        AddIfNotNull(LoggerEventType);
        AddIfNotNull(ProcessWorkingSetMB?.ToString(), nameof(ProcessWorkingSetMB));
        AddIfNotNull(MemoryLoadPercent?.ToString(), nameof(MemoryLoadPercent));

        // EndBuild hang diagnostic properties
        AddIfNotNull(EndBuildWaitPhase);
        AddIfNotNull(PendingSubmissionCount?.ToString(), nameof(PendingSubmissionCount));
        AddIfNotNull(SubmissionsWithResultNoLogging?.ToString(), nameof(SubmissionsWithResultNoLogging));
        AddIfNotNull(EndBuildWaitDurationMs?.ToString(), nameof(EndBuildWaitDurationMs));
        AddIfNotNull(ThreadExceptionRecorded?.ToString(), nameof(ThreadExceptionRecorded));
        AddIfNotNull(UnmatchedProjectStartedCount?.ToString(), nameof(UnmatchedProjectStartedCount));
        AddIfNotNull(LoggingServiceState);
        AddIfNotNull(LoggingEventQueueDepth?.ToString(), nameof(LoggingEventQueueDepth));
        AddIfNotNull(IsShuttingDown?.ToString(), nameof(IsShuttingDown));
        AddIfNotNull(IsCancellationRequested?.ToString(), nameof(IsCancellationRequested));
        AddIfNotNull(WorkQueueDepth?.ToString(), nameof(WorkQueueDepth));
        AddIfNotNull(SubmissionDetails);
        AddIfNotNull(RegisteredLoggerTypeNames);
        AddIfNotNull(ActiveNodeIds);
        AddIfNotNull(EnableNodeReuse?.ToString(), nameof(EnableNodeReuse));
        AddIfNotNull(ActiveNodeDetails);
        AddIfNotNull(ProjectCacheState);

        // Build state diagnostic properties
        AddIfNotNull(IsStandaloneExecution?.ToString(), nameof(IsStandaloneExecution));
        AddIfNotNull(MaxNodeCount?.ToString(), nameof(MaxNodeCount));
        AddIfNotNull(ActiveNodeCount?.ToString(), nameof(ActiveNodeCount));
        AddIfNotNull(SubmissionCount?.ToString(), nameof(SubmissionCount));

        return properties;

        void AddIfNotNull(string? value, [CallerArgumentExpression(nameof(value))] string key = "")
        {
            if (value is not null)
            {
                properties[key] = value;
            }
        }
    }

    /// <summary>
    /// Known namespace prefixes that indicate the crash originated in MSBuild code.
    /// </summary>
    private static readonly string[] s_msBuildNamespacePrefixes =
    [
        "Microsoft.Build",
    ];

    /// <summary>
    /// Walks the inner exception chain and returns the deepest (innermost) exception.
    /// Returns null if the exception has no inner exception.
    /// Guards against circular references with a depth limit.
    /// </summary>
    private static Exception? GetInnermostException(Exception exception)
    {
        Exception? inner = exception.InnerException;
        if (inner is null)
        {
            return null;
        }

        // Guard against circular references — 20 levels is more than enough
        // for any real exception chain.
        const int maxDepth = 20;
        int depth = 0;
        while (inner.InnerException is not null && depth < maxDepth)
        {
            inner = inner.InnerException;
            depth++;
        }

        return inner;
    }

    /// <summary>
    /// Extracts the top-level namespace from the first stack frame of the exception.
    /// Returns null if the stack trace is unavailable or cannot be parsed.
    /// Uses index-based parsing to avoid intermediate string allocations.
    /// </summary>
    internal static string? ExtractOriginNamespace(Exception exception)
    {
        string? stackTrace = exception.StackTrace;
        if (stackTrace is null)
        {
            return null;
        }

        // Get first line boundaries: "   at Namespace.Type.Method(...) in path:line N"
        int lineEnd = stackTrace.IndexOf('\n');
        if (lineEnd < 0)
        {
            lineEnd = stackTrace.Length;
        }

        // Find start of qualified name (skip leading whitespace and "at ")
        int start = 0;
        while (start < lineEnd && char.IsWhiteSpace(stackTrace[start]))
        {
            start++;
        }

        if (start + 3 <= lineEnd &&
            stackTrace[start] == 'a' && stackTrace[start + 1] == 't' && stackTrace[start + 2] == ' ')
        {
            start += 3;
        }

        // Find end of qualified name: stop at '(' or " in "
        int end = lineEnd;
        int parenIndex = stackTrace.IndexOf('(', start, end - start);
        if (parenIndex >= 0)
        {
            end = parenIndex;
        }

        int inIndex = stackTrace.IndexOf(" in ", start, end - start, StringComparison.Ordinal);
        if (inIndex >= 0)
        {
            end = inIndex;
        }

        // Trim trailing whitespace
        while (end > start && char.IsWhiteSpace(stackTrace[end - 1]))
        {
            end--;
        }

        if (end <= start)
        {
            return null;
        }

        // "Namespace.Sub.Type.Method" → count dots to determine segment count,
        // then take up to 3 segments excluding the last one (Method).
        int dotCount = 0;
        for (int i = start; i < end; i++)
        {
            if (stackTrace[i] == '.')
            {
                dotCount++;
            }
        }

        if (dotCount < 2)
        {
            // Not enough segments — return first segment (or the whole thing if no dots)
            int firstDot = stackTrace.IndexOf('.', start, end - start);
            return firstDot >= 0
                ? stackTrace.Substring(start, firstDot - start)
                : stackTrace.Substring(start, end - start);
        }

        // Walk forward to find the end of the Nth namespace segment (up to 3,
        // but at most dotCount - 2 to exclude Type.Method).
        int take = Math.Min(3, dotCount - 1); // -1 because last segment after last dot is Method
        int dotsFound = 0;
        int cutoff = start;
        for (int i = start; i < end; i++)
        {
            if (stackTrace[i] == '.')
            {
                dotsFound++;
                if (dotsFound == take)
                {
                    cutoff = i;
                    break;
                }
            }
        }

        return cutoff > start ? stackTrace.Substring(start, cutoff - start) : null;
    }

    /// <summary>
    /// Classifies the crash origin based on the faulting namespace.
    /// Returns <see cref="CrashOriginKind.MSBuild"/> if the namespace starts with a known MSBuild prefix,
    /// <see cref="CrashOriginKind.ThirdParty"/> if it doesn't, or <see cref="CrashOriginKind.Unknown"/>
    /// if no namespace could be determined.
    /// </summary>
    internal static CrashOriginKind ClassifyOrigin(string? originNamespace)
    {
        if (string.IsNullOrEmpty(originNamespace))
        {
            return CrashOriginKind.Unknown;
        }

        foreach (string prefix in s_msBuildNamespacePrefixes)
        {
            if (originNamespace!.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || (originNamespace.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && originNamespace.Length > prefix.Length
                    && originNamespace[prefix.Length] == '.'))
            {
                return CrashOriginKind.MSBuild;
            }
        }

        return CrashOriginKind.ThirdParty;
    }

    /// <summary>
    /// Computes a SHA-256 hash of the exception stack trace for bucketing without PII.
    /// </summary>
    private static string? ComputeStackHash(Exception exception)
    {
        string? stackTrace = exception.StackTrace;
        if (stackTrace is null)
        {
            return null;
        }

        // Include the inner exception's stack trace in the hash so that wrapper exceptions
        // (e.g., InternalLoggerException) get different buckets for different root causes.
        // Without this, all InternalLoggerExceptions from EventSourceSink.Consume hash
        // identically regardless of which logger actually faulted.
        if (exception.InnerException?.StackTrace is string innerStack)
        {
            stackTrace = stackTrace + "\n--- InnerException ---\n" + innerStack;
        }

#if NET
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(stackTrace));
        return Convert.ToHexString(hashBytes);
#else
        using SHA256 sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(stackTrace));
        StringBuilder sb = new(hashBytes.Length * 2);
        foreach (byte b in hashBytes)
        {
            sb.Append(b.ToString("X2"));
        }

        return sb.ToString();
#endif
    }

    /// <summary>
    /// Truncates the exception message and sanitizes file paths to avoid sending PII.
    /// Some ThrowInternalError call sites embed file paths (e.g., project paths, SDK paths)
    /// in the message, which may contain usernames or other PII.
    /// </summary>
    internal static string? TruncateMessage(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return null;
        }

        // Strip the "MSB0001: Internal MSBuild Error: " prefix that InternalErrorException prepends.
        const string internalErrorPrefix = "MSB0001: Internal MSBuild Error: ";
        if (message!.StartsWith(internalErrorPrefix, StringComparison.Ordinal))
        {
            message = message.Substring(internalErrorPrefix.Length);
        }

        // Redact file/directory paths that may contain PII (e.g., C:\Users\useralias\...).
        message = FilePathPattern.Replace(message, "<path>");

        const int maxLength = 256;
        return message.Length <= maxLength ? message : message.Substring(0, maxLength);
    }

    /// <summary>
    /// Known throw-helper method suffixes. When the top stack frame ends with one of
    /// these, <see cref="ExtractStackCaller"/> will skip it and return the next frame.
    /// These are methods that only exist to format and throw an exception — the real
    /// bug is always in their caller.
    /// </summary>
    private static readonly string[] s_throwHelperSuffixes =
    [
        "ErrorUtilities.ThrowInternalError(",
        "ErrorUtilities.VerifyThrowInternalError(",
        "ErrorUtilities.ThrowInternalErrorUnreachable(",
        "ErrorUtilities.VerifyThrowInternalErrorUnreachable(",
        "ErrorUtilities.VerifyThrowInternalNull(",
        "ErrorUtilities.ThrowInvalidOperation(",
        "ErrorUtilities.VerifyThrow(",
    ];

    /// <summary>
    /// Extracts the top frame of the stack trace to identify the crash location.
    /// </summary>
    private static string? ExtractStackTop(Exception exception)
    {
        string? stackTrace = exception.StackTrace;
        if (stackTrace is null)
        {
            return null;
        }

        // Get the first line of the stack trace (the top frame).
        int newLineIndex = stackTrace.IndexOf('\n');
        string topFrame = newLineIndex >= 0 ? stackTrace.Substring(0, newLineIndex) : stackTrace;
        return SanitizeStackFrame(topFrame.Trim());
    }

    /// <summary>
    /// Extracts and sanitizes the full stack trace from the exception.
    /// Each frame has file paths redacted. Truncated to <see cref="MaxStackTraceLength"/>.
    /// </summary>
    internal static string? ExtractFullStackTrace(Exception exception)
    {
        string? stackTrace = exception.StackTrace;
        if (string.IsNullOrEmpty(stackTrace))
        {
            return null;
        }

        string sanitized = SanitizeFilePathsInText(stackTrace!);

        if (sanitized.Length > MaxStackTraceLength)
        {
            const string truncationSuffix = "... [truncated]";
            sanitized = sanitized.Substring(0, MaxStackTraceLength - truncationSuffix.Length) + truncationSuffix;
        }

        return sanitized;
    }

    /// <summary>
    /// If the top stack frame is a known throw-helper (e.g., ErrorUtilities.ThrowInternalError),
    /// extracts the next frame — the actual caller where the bug lives.
    /// Returns null if the top frame is not a throw-helper or no further frames exist.
    /// </summary>
    internal static string? ExtractStackCaller(Exception exception)
    {
        string? stackTrace = exception.StackTrace;
        if (stackTrace is null)
        {
            return null;
        }

        // Check if the first frame is a known throw-helper.
        int firstNewLine = stackTrace.IndexOf('\n');
        string firstFrame = (firstNewLine >= 0 ? stackTrace.Substring(0, firstNewLine) : stackTrace).Trim();

        bool isThrowHelper = false;
        foreach (string suffix in s_throwHelperSuffixes)
        {
            if (firstFrame.IndexOf(suffix, StringComparison.Ordinal) >= 0)
            {
                isThrowHelper = true;
                break;
            }
        }

        if (!isThrowHelper || firstNewLine < 0)
        {
            return null;
        }

        // Extract the second frame (the caller of the throw-helper).
        int secondStart = firstNewLine + 1;
        if (secondStart >= stackTrace.Length)
        {
            return null;
        }

        int secondNewLine = stackTrace.IndexOf('\n', secondStart);
        string secondFrame = secondNewLine >= 0
            ? stackTrace.Substring(secondStart, secondNewLine - secondStart)
            : stackTrace.Substring(secondStart);

        string trimmed = secondFrame.Trim();
        return trimmed.Length > 0 ? SanitizeStackFrame(trimmed) : null;
    }

    /// <summary>
    /// Redacts file paths from a stack frame to avoid leaking PII (e.g. usernames in paths).
    /// Preserves the method signature and line number.
    /// </summary>
    private static string SanitizeStackFrame(string frame)
    {
        if (string.IsNullOrEmpty(frame))
        {
            return frame;
        }

        // Typical .NET stack frame:
        //   at Namespace.Type.Method() in C:\Users\username\path\file.cs:line 123
        const string inToken = " in ";
        const string lineToken = ":line ";

        int inIndex = frame.IndexOf(inToken, StringComparison.Ordinal);
        if (inIndex < 0)
        {
            return frame;
        }

        int lineIndex = frame.IndexOf(lineToken, inIndex, StringComparison.Ordinal);
        if (lineIndex < 0)
        {
            return frame.Substring(0, inIndex + inToken.Length) + "<redacted>";
        }

        string prefix = frame.Substring(0, inIndex + inToken.Length);
        string lineSuffix = frame.Substring(lineIndex);
        return prefix + "<redacted>" + lineSuffix;
    }

    /// <summary>
    /// Sanitizes file paths embedded in multi-line text (e.g., stack traces, exception dumps) to remove PII.
    /// Handles both standard .NET stack frame patterns (" in path:line N") and
    /// bare file paths (Windows absolute, UNC, Unix absolute) that may appear in exception messages.
    /// </summary>
    internal static string SanitizeFilePathsInText(string text)
    {
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            // Sanitize " in <path>:line N" patterns (stack frames)
            const string inToken = " in ";
            const string lineToken = ":line ";

            int inIndex = line.IndexOf(inToken, StringComparison.Ordinal);
            if (inIndex >= 0)
            {
                int lineIndex = line.IndexOf(lineToken, inIndex, StringComparison.Ordinal);
                if (lineIndex >= 0)
                {
                    string prefix = line.Substring(0, inIndex + inToken.Length);
                    string lineSuffix = line.Substring(lineIndex);
                    lines[i] = prefix + "<redacted>" + lineSuffix;
                }
                else
                {
                    // " in <path>" without ":line N"
                    lines[i] = line.Substring(0, inIndex + inToken.Length) + "<redacted>";
                }
            }
            else
            {
                // For non-stack-frame lines (e.g., exception messages embedded in ToString()),
                // apply general path redaction to catch paths that appear outside " in " patterns.
                lines[i] = FilePathPattern.Replace(line, "<path>");
            }
        }

        return string.Join("\n", lines);
    }
}
