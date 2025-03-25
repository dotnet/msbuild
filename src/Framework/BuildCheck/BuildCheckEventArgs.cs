// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Base class for all build check event args.
/// Not intended to be extended by external code.
/// </summary>
internal abstract class BuildCheckEventArgs : BuildEventArgs
{ }

/// <summary>
/// Transport mean for the BuildCheck tracing data from additional nodes.
/// </summary>
internal sealed class BuildCheckTracingEventArgs(
    BuildCheckTracingData tracingData) : BuildCheckEventArgs
{
    internal BuildCheckTracingEventArgs()
        : this(new BuildCheckTracingData())
    { }

    internal BuildCheckTracingEventArgs(Dictionary<string, TimeSpan> executionData)
        : this(new BuildCheckTracingData(executionData))
    { }

    internal BuildCheckTracingEventArgs(
        BuildCheckTracingData tracingData,
        bool isAggregatedGlobalReport) : this(tracingData) => IsAggregatedGlobalReport = isAggregatedGlobalReport;

    /// <summary>
    /// When true, the tracing information is from the whole build for logging purposes
    /// When false, the tracing is being used for communication between nodes and central process
    /// </summary>
    public bool IsAggregatedGlobalReport { get; private set; } = false;

    public BuildCheckTracingData TracingData { get; private set; } = tracingData;

    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);

        writer.WriteDurationsDictionary(TracingData.InfrastructureTracingData);

        writer.Write7BitEncodedInt(TracingData.TelemetryData.Count);
        foreach (BuildCheckRuleTelemetryData data in TracingData.TelemetryData.Values)
        {
            writer.Write(data.RuleId);
            writer.Write(data.CheckFriendlyName);
            writer.Write(data.IsBuiltIn);
            writer.Write7BitEncodedInt((int)data.DefaultSeverity);
            writer.Write7BitEncodedInt(data.ExplicitSeverities.Count);
            foreach (DiagnosticSeverity severity in data.ExplicitSeverities)
            {
                writer.Write7BitEncodedInt((int)severity);
            }
            writer.Write7BitEncodedInt(data.ProjectNamesWhereEnabled.Count);
            foreach (string projectName in data.ProjectNamesWhereEnabled)
            {
                writer.Write(projectName);
            }
            writer.Write7BitEncodedInt(data.ViolationMessagesCount);
            writer.Write7BitEncodedInt(data.ViolationWarningsCount);
            writer.Write7BitEncodedInt(data.ViolationErrorsCount);
            writer.Write(data.IsThrottled);
            writer.Write(data.TotalRuntime.Ticks);
        }
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);

        var infrastructureTracingData = reader.ReadDurationDictionary();

        int count = reader.Read7BitEncodedInt();
        List<BuildCheckRuleTelemetryData> tracingData = new List<BuildCheckRuleTelemetryData>(count);
        for (int i = 0; i < count; i++)
        {
            string ruleId = reader.ReadString();
            string checkFriendlyName = reader.ReadString();
            bool isBuiltIn = reader.ReadBoolean();
            DiagnosticSeverity defaultSeverity = (DiagnosticSeverity)reader.Read7BitEncodedInt();
            int explicitSeveritiesCount = reader.Read7BitEncodedInt();
            HashSet<DiagnosticSeverity> explicitSeverities =
                EnumerableExtensions.NewHashSet<DiagnosticSeverity>(explicitSeveritiesCount);
            for (int j = 0; j < explicitSeveritiesCount; j++)
            {
                explicitSeverities.Add((DiagnosticSeverity)reader.Read7BitEncodedInt());
            }
            int projectNamesWhereEnabledCount = reader.Read7BitEncodedInt();
            HashSet<string> projectNamesWhereEnabled =
                EnumerableExtensions.NewHashSet<string>(projectNamesWhereEnabledCount);
            for (int j = 0; j < projectNamesWhereEnabledCount; j++)
            {
                projectNamesWhereEnabled.Add(reader.ReadString());
            }
            int violationMessagesCount = reader.Read7BitEncodedInt();
            int violationWarningsCount = reader.Read7BitEncodedInt();
            int violationErrorsCount = reader.Read7BitEncodedInt();
            bool isThrottled = reader.ReadBoolean();
            TimeSpan totalRuntime = TimeSpan.FromTicks(reader.ReadInt64());

            BuildCheckRuleTelemetryData data = new BuildCheckRuleTelemetryData(
                ruleId, checkFriendlyName, isBuiltIn, defaultSeverity, explicitSeverities, projectNamesWhereEnabled,
                violationMessagesCount, violationWarningsCount, violationErrorsCount, isThrottled, totalRuntime);

            tracingData.Add(data);
        }

        TracingData = new BuildCheckTracingData(tracingData, infrastructureTracingData);
    }
}

internal sealed class BuildCheckAcquisitionEventArgs(string acquisitionPath, string projectPath) : BuildCheckEventArgs
{
    internal BuildCheckAcquisitionEventArgs()
        : this(string.Empty, string.Empty)
    {
    }

    /// <summary>
    /// Gets the path to the check assembly that needs to be loaded into the application context.
    /// </summary>
    /// <remarks>
    /// The <see cref="AcquisitionPath"/> property contains the file system path to the assembly
    /// that is required to be loaded into the application context. This path is used for loading
    /// the specified assembly dynamically during runtime.
    /// </remarks>
    /// <value>
    /// A <see cref="System.String"/> representing the file system path to the assembly.
    /// </value>
    public string AcquisitionPath { get; private set; } = acquisitionPath;

    public string ProjectPath { get; private set; } = projectPath;

    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);

        writer.Write(AcquisitionPath);
        writer.Write(ProjectPath);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);

        AcquisitionPath = reader.ReadString();
        ProjectPath = reader.ReadString();
    }
}

internal sealed class BuildCheckResultWarning : BuildWarningEventArgs
{
    public BuildCheckResultWarning(IBuildCheckResult result)
        : base(code: result.Code, file: result.Location.File, lineNumber: result.Location.Line, columnNumber: result.Location.Column, message: result.FormatMessage()) =>
        RawMessage = result.FormatMessage();

    internal BuildCheckResultWarning() { }

    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);

        writer.Write(RawMessage!);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);

        RawMessage = reader.ReadString();
    }
}

internal sealed class BuildCheckResultError : BuildErrorEventArgs
{
    public BuildCheckResultError(IBuildCheckResult result)
        : base(code: result.Code, file: result.Location.File, lineNumber: result.Location.Line, columnNumber: result.Location.Column, message: result.FormatMessage())
        => RawMessage = result.FormatMessage();

    internal BuildCheckResultError() { }

    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);

        writer.Write(RawMessage!);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);

        RawMessage = reader.ReadString();
    }
}

internal sealed class BuildCheckResultMessage : BuildMessageEventArgs
{
    public BuildCheckResultMessage(IBuildCheckResult result)
        : base(code: result.Code, file: result.Location.File, lineNumber: result.Location.Line, columnNumber: result.Location.Column, message: result.FormatMessage())
        => RawMessage = result.FormatMessage();

    internal BuildCheckResultMessage(string formattedMessage) => RawMessage = formattedMessage;

    internal BuildCheckResultMessage() { }

    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);

        writer.Write(RawMessage!);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);

        RawMessage = reader.ReadString();
    }
}
