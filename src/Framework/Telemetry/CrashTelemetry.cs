// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Build.Framework.Telemetry;

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
    /// The exit type / category of the crash (e.g., "LoggerFailure", "Unexpected", "UnhandledException").
    /// </summary>
    public string? ExitType { get; set; }

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
    /// Timestamp when the crash occurred.
    /// </summary>
    public DateTime? CrashTimestamp { get; set; }

    /// <summary>
    /// Populates this instance from an exception.
    /// </summary>
    public void PopulateFromException(Exception exception)
    {
        ExceptionType = exception.GetType().FullName;
        InnerExceptionType = exception.InnerException?.GetType().FullName;
        HResult = exception.HResult;
        CrashTimestamp = DateTime.UtcNow;
        StackHash = ComputeStackHash(exception);
        StackTop = ExtractStackTop(exception);
    }

    /// <summary>
    /// Create a list of properties sent to VS telemetry as activity tags.
    /// </summary>
    public Dictionary<string, object> GetActivityProperties()
    {
        Dictionary<string, object> telemetryItems = new(10);

        AddIfNotNull(ExceptionType);
        AddIfNotNull(InnerExceptionType);
        AddIfNotNull(ExitType);
        AddIfNotNull(IsCritical);
        AddIfNotNull(IsUnhandled);
        AddIfNotNull(StackHash);
        AddIfNotNull(StackTop);
        AddIfNotNull(HResult);
        AddIfNotNull(BuildEngineVersion);
        AddIfNotNull(BuildEngineFrameworkName);
        AddIfNotNull(BuildEngineHost);

        if (CrashTimestamp.HasValue)
        {
            telemetryItems.Add(nameof(CrashTimestamp), CrashTimestamp.Value.ToString("O"));
        }

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
        AddIfNotNull(ExitType);
        AddIfNotNull(IsCritical?.ToString(), nameof(IsCritical));
        AddIfNotNull(IsUnhandled.ToString(), nameof(IsUnhandled));
        AddIfNotNull(StackHash);
        AddIfNotNull(StackTop);
        AddIfNotNull(HResult?.ToString(), nameof(HResult));
        AddIfNotNull(BuildEngineVersion);
        AddIfNotNull(BuildEngineFrameworkName);
        AddIfNotNull(BuildEngineHost);
        AddIfNotNull(CrashTimestamp?.ToString("O"), nameof(CrashTimestamp));

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
