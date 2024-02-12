// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental;

public class BuildAnalysisResult
{
    public static BuildAnalysisResult Create(BuildAnalysisRule rule, ElementLocation location, params string[] messageArgs)
    {
        return new BuildAnalysisResult(rule, location, messageArgs);
    }

    public BuildAnalysisResult(BuildAnalysisRule buildAnalysisRule, ElementLocation location, string[] messageArgs)
    {
        BuildAnalysisRule = buildAnalysisRule;
        Location = location;
        MessageArgs = messageArgs;
    }

    internal BuildEventArgs ToEventArgs(BuildAnalysisResultSeverity severity)
        => severity switch
        {
            BuildAnalysisResultSeverity.Info => new BuildAnalysisResultMessage(this),
            BuildAnalysisResultSeverity.Warning => new BuildAnalysisResultWarning(this),
            BuildAnalysisResultSeverity.Error => new BuildAnalysisResultError(this),
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
        };

    public BuildAnalysisRule BuildAnalysisRule { get; }
    public ElementLocation Location { get; }
    public string[] MessageArgs { get; }

    private string? _message;
    public string Message => _message ??= $"{(Equals(Location ?? ElementLocation.EmptyLocation, ElementLocation.EmptyLocation) ? string.Empty : (Location!.LocationString + ": "))}{BuildAnalysisRule.Id}: {string.Format(BuildAnalysisRule.MessageFormat, MessageArgs)}";
}

public sealed class BuildAnalysisResultWarning : BuildWarningEventArgs
{
    public BuildAnalysisResultWarning(BuildAnalysisResult result)
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
    public BuildAnalysisResultError(BuildAnalysisResult result)
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
    public BuildAnalysisResultMessage(BuildAnalysisResult result)
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
