// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
#if NETFRAMEWORK
using Microsoft.VisualStudio.Telemetry;
#endif

namespace Microsoft.Build.Framework.Telemetry;

/// <summary>
/// Centralized helper for recording and flushing crash/failure telemetry.
/// All methods are best-effort and will never throw.
/// </summary>
internal static class CrashTelemetryRecorder
{
    /// <summary>
    /// Records crash telemetry data for later emission via <see cref="FlushCrashTelemetry"/>.
    /// </summary>
    /// <param name="exception">The exception that caused the crash.</param>
    /// <param name="exitType">Exit type classification.</param>
    /// <param name="isUnhandled">True if the exception was not caught by any catch block.</param>
    /// <param name="isCritical">Whether the exception is classified as critical (OOM, StackOverflow, etc.).</param>
    /// <param name="buildEngineVersion">MSBuild version string, if available.</param>
    /// <param name="buildEngineFrameworkName">Framework name, if available.</param>
    /// <param name="buildEngineHost">Host name (VS, VSCode, CLI, etc.), if available.</param>
    public static void RecordCrashTelemetry(
        Exception exception,
        CrashExitType exitType,
        bool isUnhandled,
        bool isCritical,
        string? buildEngineVersion = null,
        string? buildEngineFrameworkName = null,
        string? buildEngineHost = null)
    {
        try
        {
            CrashTelemetry crashTelemetry = CreateCrashTelemetry(exception, exitType, isUnhandled, isCritical);
            crashTelemetry.BuildEngineVersion = buildEngineVersion;
            crashTelemetry.BuildEngineFrameworkName = buildEngineFrameworkName;
            crashTelemetry.BuildEngineHost = buildEngineHost;
            KnownTelemetry.CrashTelemetry = crashTelemetry;
        }
        catch
        {
            // Best effort: telemetry must never cause a secondary failure.
        }
    }

    /// <summary>
    /// Records crash telemetry and immediately flushes it.
    /// Use when the process is about to terminate (e.g. unhandled exception handler).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void RecordAndFlushCrashTelemetry(
        Exception exception,
        CrashExitType exitType,
        bool isUnhandled,
        bool isCritical)
    {
        try
        {
            CrashTelemetry crashTelemetry = CreateCrashTelemetry(exception, exitType, isUnhandled, isCritical);

            // Initialize here because the process is about to die — this may be
            // the only chance to set up telemetry (e.g., crash before Main() init,
            // or in a task AppDomain with separate static state).
            TelemetryManager.Instance?.Initialize(isStandalone: false);

            using IActivity? activity = TelemetryManager.Instance
                ?.DefaultActivitySource
                ?.StartActivity(TelemetryConstants.Crash);
            activity?.SetTags(crashTelemetry);

            PostFaultEvent(crashTelemetry);
        }
        catch
        {
            // Best effort: telemetry must never cause a secondary failure.
        }
    }

    /// <summary>
    /// Flushes any pending crash telemetry via the telemetry manager.
    /// Requires that TelemetryManager has already been initialized by the caller.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void FlushCrashTelemetry()
    {
        try
        {
            CrashTelemetry? crashTelemetry = KnownTelemetry.CrashTelemetry;
            if (crashTelemetry is null)
            {
                return;
            }

            KnownTelemetry.CrashTelemetry = null;

            // Do not call TelemetryManager.Initialize here — the caller (Main or BuildManager)
            // is responsible for initialization. Calling Initialize from here would create a
            // VS telemetry session when tests call MSBuildApp.Execute() in-process, causing
            // environment variable side effects.
            using IActivity? activity = TelemetryManager.Instance
                ?.DefaultActivitySource
                ?.StartActivity(TelemetryConstants.Crash);
            activity?.SetTags(crashTelemetry);

            PostFaultEvent(crashTelemetry);
        }
        catch
        {
            // Best effort: telemetry must never cause a secondary failure.
        }
    }

    /// <summary>
    /// Posts a <c>FaultEvent</c> to the VS telemetry session so that crashes
    /// appear in Prism fault dashboards alongside other VS component faults.
    /// Only available on .NET Framework where the VS Telemetry SDK is loaded.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PostFaultEvent(CrashTelemetry crashTelemetry)
    {
#if NETFRAMEWORK
        try
        {
            string eventName = $"{TelemetryConstants.EventPrefix}{TelemetryConstants.Crash}";
            string description = $"{crashTelemetry.ExitType}: {crashTelemetry.ExceptionType}";
            var faultEvent = new FaultEvent(eventName, description, crashTelemetry.Exception);

            faultEvent.Properties[$"{TelemetryConstants.PropertyPrefix}ExitType"] = crashTelemetry.ExitType.ToString();
            faultEvent.Properties[$"{TelemetryConstants.PropertyPrefix}CrashOrigin"] = crashTelemetry.CrashOrigin.ToString();

            if (crashTelemetry.CrashOriginAssembly is not null)
            {
                faultEvent.Properties[$"{TelemetryConstants.PropertyPrefix}CrashOriginAssembly"] = crashTelemetry.CrashOriginAssembly;
            }

            if (crashTelemetry.StackHash is not null)
            {
                faultEvent.Properties[$"{TelemetryConstants.PropertyPrefix}StackHash"] = crashTelemetry.StackHash;
            }

            TelemetryService.DefaultSession.PostEvent(faultEvent);
        }
        catch
        {
            // Best effort: fault telemetry must never cause a secondary failure.
        }
#endif
    }

    private static CrashTelemetry CreateCrashTelemetry(
        Exception exception,
        CrashExitType exitType,
        bool isUnhandled,
        bool isCritical)
    {
        CrashTelemetry crashTelemetry = new();
        crashTelemetry.PopulateFromException(exception);
        crashTelemetry.ExitType = exitType;
        crashTelemetry.IsCritical = isCritical;
        crashTelemetry.IsUnhandled = isUnhandled;
        return crashTelemetry;
    }
}
