// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental;

public class BuildAnalyzerResult
{
    public static BuildAnalyzerResult Create(BuildAnalyzerRule rule, ElementLocation location, params string[] messageArgs)
    {
        return new BuildAnalyzerResult(rule, location, messageArgs);
    }

    public BuildAnalyzerResult(BuildAnalyzerRule buildAnalyzerRule, ElementLocation location, string[] messageArgs)
    {
        BuildAnalyzerRule = buildAnalyzerRule;
        Location = location;
        MessageArgs = messageArgs;
    }

    internal BuildEventArgs ToEventArgs(BuildAnalyzerResultSeverity severity)
        => severity switch
        {
            BuildAnalyzerResultSeverity.Info => new BuildAnalysisResultMessage(this),
            BuildAnalyzerResultSeverity.Warning => new BuildAnalysisResultWarning(this),
            BuildAnalyzerResultSeverity.Error => new BuildAnalysisResultError(this),
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
        };

    public BuildAnalyzerRule BuildAnalyzerRule { get; }
    public ElementLocation Location { get; }
    public string[] MessageArgs { get; }

    private string? _message;
    public string Message => _message ??= $"{(Equals(Location ?? ElementLocation.EmptyLocation, ElementLocation.EmptyLocation) ? string.Empty : (Location!.LocationString + ": "))}{BuildAnalyzerRule.Id}: {string.Format(BuildAnalyzerRule.MessageFormat, MessageArgs)}";
}

public sealed class BuildAnalysisResultWarning : BuildWarningEventArgs
{
    public BuildAnalysisResultWarning(BuildAnalyzerResult result)
    {
        this.Message = result.Message;
    }


    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);

        writer.Write(Message!);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);

        Message = reader.ReadString();
    }

    public override string? Message { get; protected set; }
}

public sealed class BuildAnalysisResultError : BuildErrorEventArgs
{
    public BuildAnalysisResultError(BuildAnalyzerResult result)
    {
        this.Message = result.Message;
    }


    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);

        writer.Write(Message!);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);

        Message = reader.ReadString();
    }

    public override string? Message { get; protected set; }
}

public sealed class BuildAnalysisResultMessage : BuildMessageEventArgs
{
    public BuildAnalysisResultMessage(BuildAnalyzerResult result)
    {
        this.Message = result.Message;
    }


    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);

        writer.Write(Message!);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);

        Message = reader.ReadString();
    }

    public override string? Message { get; protected set; }
}
