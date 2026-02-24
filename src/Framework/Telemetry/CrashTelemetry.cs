// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

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
    /// </summary>
    public string? StackTop { get; set; }

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
    public string? CrashOriginAssembly { get; set; }

    /// <summary>
    /// The deepest inner exception type in the exception chain.
    /// For wrapper exceptions like <see cref="System.TypeInitializationException"/>,
    /// this reveals the actual root cause exception type.
    /// </summary>
    public string? InnermostExceptionType { get; set; }

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
        StackHash = ComputeStackHash(exception);
        StackTop = ExtractStackTop(exception);
        CrashOriginAssembly = ExtractOriginNamespace(exception);
        CrashOrigin = ClassifyOrigin(CrashOriginAssembly);
        PopulateMemoryStats();
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
        AddIfNotNull(HResult);
        AddIfNotNull(BuildEngineVersion);
        AddIfNotNull(BuildEngineFrameworkName);
        AddIfNotNull(BuildEngineHost);
        if (CrashOrigin != CrashOriginKind.Unknown)
        {
            telemetryItems.Add(nameof(CrashOrigin), CrashOrigin.ToString());
        }
        AddIfNotNull(CrashOriginAssembly);
        AddIfNotNull(InnermostExceptionType);
        AddIfNotNull(ProcessWorkingSetMB);
        AddIfNotNull(MemoryLoadPercent);

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
        AddIfNotNull(HResult?.ToString(), nameof(HResult));
        AddIfNotNull(BuildEngineVersion);
        AddIfNotNull(BuildEngineFrameworkName);
        AddIfNotNull(BuildEngineHost);
        if (CrashOrigin != CrashOriginKind.Unknown)
        {
            AddIfNotNull(CrashOrigin.ToString(), nameof(CrashOrigin));
        }
        AddIfNotNull(CrashOriginAssembly);
        AddIfNotNull(ProcessWorkingSetMB?.ToString(), nameof(ProcessWorkingSetMB));
        AddIfNotNull(MemoryLoadPercent?.ToString(), nameof(MemoryLoadPercent));

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
        "Microsoft.Build.",
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
    /// </summary>
    internal static string? ExtractOriginNamespace(Exception exception)
    {
        string? stackTrace = exception.StackTrace;
        if (stackTrace is null)
        {
            return null;
        }

        // Get first line: "   at Namespace.Type.Method(...) in path:line N"
        int newLineIndex = stackTrace.IndexOf('\n');
        string topFrame = (newLineIndex >= 0 ? stackTrace.Substring(0, newLineIndex) : stackTrace).Trim();

        if (topFrame.StartsWith("at ", StringComparison.Ordinal))
        {
            topFrame = topFrame.Substring(3);
        }

        // Extract the qualified name before '(' and before " in ".
        int parenIndex = topFrame.IndexOf('(');
        if (parenIndex >= 0)
        {
            topFrame = topFrame.Substring(0, parenIndex);
        }

        int inIndex = topFrame.IndexOf(" in ", StringComparison.Ordinal);
        if (inIndex >= 0)
        {
            topFrame = topFrame.Substring(0, inIndex);
        }

        // "Namespace.Sub.Type.Method" → split and take up to 3 namespace segments,
        // always excluding the last 2 (Type + Method).
        string[] parts = topFrame.Trim().Split('.');
        if (parts.Length < 2)
        {
            return parts[0].Length > 0 ? parts[0] : null;
        }

        int take = Math.Min(3, parts.Length - 2);
        return take > 0 ? string.Join(".", parts, 0, take) : parts[0];
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
            if (originNamespace!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || originNamespace.Equals("Microsoft.Build", StringComparison.OrdinalIgnoreCase))
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
}
