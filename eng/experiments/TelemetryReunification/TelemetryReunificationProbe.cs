// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Web.Script.Serialization;
using Microsoft.Build.Framework;

namespace TelemetryReunification;

// Untimed diagnostics only. This assembly is not built into or shipped with MSBuild.
public sealed class SessionProbe : ILogger
{
    public LoggerVerbosity Verbosity { get; set; }
    public string? Parameters { get; set; }

    public void Initialize(IEventSource eventSource)
    {
        Console.WriteLine(Probe.Capture("standalone-initialize", requireOwnedSession: true));
        eventSource.BuildFinished += (_, _) => Console.WriteLine(Probe.Capture("standalone-build-finished", requireOwnedSession: true));
    }

    public void Shutdown()
    {
        Console.WriteLine(Probe.Capture("standalone-logger-shutdown", requireOwnedSession: true));
    }
}

public sealed class BindingProbe : ITask
{
    public IBuildEngine BuildEngine { get; set; } = null!;
    public ITaskHost HostObject { get; set; } = null!;

    [Required]
    public string SnapshotPath { get; set; } = null!;

    public bool Execute()
    {
        var serializer = new JavaScriptSerializer();
        var snapshot = (Dictionary<string, object>)serializer.DeserializeObject(System.IO.File.ReadAllText(SnapshotPath));
        var requestedBindings = new List<Dictionary<string, object?>>();
        foreach (Dictionary<string, object> assembly in (IEnumerable)snapshot["Assemblies"])
        {
            // Exercise default-context binding, without activating a transport or changing consent/flights.
            Assembly bound = Assembly.Load((string)assembly["Identity"]);
            string mvid = bound.ManifestModule.ModuleVersionId.ToString();
            if (!mvid.Equals((string)assembly["Mvid"], StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unexpected binding for {assembly["Identity"]}: {bound.Location}, MVID {mvid}.");
            }

            requestedBindings.Add(new Dictionary<string, object?>
            {
                ["name"] = bound.GetName().Name,
                ["identity"] = bound.FullName,
                ["mvid"] = mvid,
                ["path"] = bound.Location,
            });
        }

        BuildEngine.LogMessageEvent(new BuildMessageEventArgs(
            Probe.Capture("explicit-worker-closure-binding; not a task-created telemetry session", requireOwnedSession: false, requestedBindings),
            null, nameof(BindingProbe), MessageImportance.High));
        return true;
    }
}

internal static class Probe
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    internal static string Capture(string stage, bool requireOwnedSession, IReadOnlyList<Dictionary<string, object?>>? requestedBindings = null)
    {
        Assembly? sdk = null;
        var loaded = new List<Dictionary<string, object?>>();
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            string name = assembly.GetName().Name!;
            if (name == "Microsoft.VisualStudio.Telemetry")
            {
                sdk = assembly;
            }

            if (name.StartsWith("Microsoft.VisualStudio.", StringComparison.Ordinal)
                || name.StartsWith("OpenTelemetry", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.Bcl.", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
                || name.StartsWith("System.", StringComparison.Ordinal))
            {
                loaded.Add(new Dictionary<string, object?>
                {
                    ["name"] = name,
                    ["path"] = assembly.Location,
                    ["mvid"] = assembly.ManifestModule.ModuleVersionId.ToString(),
                });
            }
        }

        if (sdk is null)
        {
            throw new InvalidOperationException("The real process did not load the telemetry SDK.");
        }

        Type initializer = typeof(ILogger).Assembly.GetType("Microsoft.Build.Framework.Telemetry.VsTelemetryInitializer", throwOnError: true)!;
        object? ownedSession = initializer.GetField("s_telemetrySession", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
        if (ownedSession is null && requireOwnedSession)
        {
            throw new InvalidOperationException("The engine did not create its ordinary owned telemetry session.");
        }

        // Unlike DefaultSession, this getter does not create a session when none exists.
        object? session = sdk.GetType("Microsoft.VisualStudio.Telemetry.TelemetryService", throwOnError: true)!
            .GetProperty("InternalDefaultSession", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
        object? enabled = null;
        object? started = null;
        object? optedIn = null;
        object? initialized = null;
        bool hasOpenTelemetryApi = false;
        string? preparationFailure = null;
        var channels = new List<string>();
        var actualChannels = new List<Dictionary<string, object?>>();
        bool hasNoopChannel = false;
        if (session is not null)
        {
            PropertyInfo? openTelemetryProperty = FindProperty(session.GetType(), "IsOpenTelemetryEnabled");
            hasOpenTelemetryApi = openTelemetryProperty is not null;
            enabled = openTelemetryProperty?.GetValue(session);
            optedIn = FindProperty(session.GetType(), "IsOptedIn")!.GetValue(session);
            object internalSession = FindField(session.GetType(), "internalTelemetrySessionObject").GetValue(session)!;
            started = FindField(internalSession.GetType(), "isSessionStarted").GetValue(internalSession);
            initialized = FindField(internalSession.GetType(), "isInitialized").GetValue(internalSession);
            foreach (string channel in (IEnumerable)FindField(internalSession.GetType(), "defaultSessionChannelsId").GetValue(internalSession)!)
            {
                channels.Add(channel);
            }

            if (hasOpenTelemetryApi)
            {
                object diagnostics = FindField(internalSession.GetType(), "diagnosticTelemetry").GetValue(internalSession)!;
                foreach (object entry in (IEnumerable)FindField(diagnostics.GetType(), "initializationProperties").GetValue(diagnostics)!)
                {
                    string key = (string)entry.GetType().GetProperty("Key")!.GetValue(entry)!;
                    if (key == "VS.TelemetryApi.OtelChannelPreparationStatus")
                    {
                        preparationFailure = (string)entry.GetType().GetProperty("Value")!.GetValue(entry)!;
                    }
                }
            }

            object processor = FindField(internalSession.GetType(), "<EventProcessor>k__BackingField").GetValue(internalSession)!;
            object context = FindField(processor.GetType(), "eventProcessorContext").GetValue(processor)!;
            object router = FindProperty(context.GetType(), "Router")!.GetValue(context)!;
            foreach (object channel in (IEnumerable)FindField(router.GetType(), "channelList").GetValue(router)!)
            {
                object? id = FindProperty(channel.GetType(), "ChannelId")!.GetValue(channel);
                hasNoopChannel |= string.Equals(id as string, "openTelemetryNoopChannel", StringComparison.Ordinal);
                actualChannels.Add(new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["type"] = channel.GetType().FullName,
                });
            }
        }

        string json = new JavaScriptSerializer().Serialize(new Dictionary<string, object?>
        {
            ["stage"] = stage,
            ["pid"] = Process.GetCurrentProcess().Id,
            ["is_worker_process"] = Environment.CommandLine.IndexOf("/nodemode:", StringComparison.OrdinalIgnoreCase) >= 0
                || Environment.CommandLine.IndexOf("-nodemode:", StringComparison.OrdinalIgnoreCase) >= 0,
            ["sdk_path"] = sdk.Location,
            ["sdk_mvid"] = sdk.ManifestModule.ModuleVersionId.ToString(),
            ["sdk_file_version"] = FileVersionInfo.GetVersionInfo(sdk.Location).FileVersion,
            ["engine_session_present"] = ownedSession is not null,
            ["existing_sdk_session_present"] = session is not null,
            ["session_initialized"] = initialized,
            ["session_started"] = started,
            ["is_opted_in"] = optedIn,
            ["is_open_telemetry_enabled"] = enabled,
            ["channels"] = channels,
            ["actual_channels"] = actualChannels,
            ["otel_preparation_failure"] = preparationFailure,
            ["preparation_evidence"] = session is null
                ? "no existing SDK session; activation not exercised"
                : !hasOpenTelemetryApi
                ? "not applicable; old SDK has no OpenTelemetry API"
                : preparationFailure is null ? "no retained failure property; not proof that OpenTelemetry was requested" : "failure recorded",
            ["loaded_assemblies"] = loaded,
            ["requested_bindings"] = requestedBindings,
        });

        if (preparationFailure is not null || hasNoopChannel || channels.Contains("openTelemetryNoopChannel"))
        {
            throw new InvalidOperationException("OpenTelemetry preparation/wrapper failed; successful legacy fallback is not a valid experiment: " + json);
        }

        return "REUNIFICATION:" + json;
    }

    private static FieldInfo FindField(Type type, string name)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            FieldInfo? field = current.GetField(name, InstanceFlags);
            if (field is not null)
            {
                return field;
            }
        }

        throw new MissingFieldException(type.FullName, name);
    }

    private static PropertyInfo? FindProperty(Type type, string name)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            PropertyInfo? property = current.GetProperty(name, InstanceFlags);
            if (property is not null)
            {
                return property;
            }
        }

        return null;
    }
}
