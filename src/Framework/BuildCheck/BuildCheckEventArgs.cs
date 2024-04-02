// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck;

public abstract class BuildCheckEventArgs : BuildEventArgs
{ }

public sealed class BuildCheckTracingEventArgs(Dictionary<string, TimeSpan> tracingData) : BuildCheckEventArgs
{
    internal BuildCheckTracingEventArgs() : this(new Dictionary<string, TimeSpan>())
    { }

    public Dictionary<string, TimeSpan> TracingData { get; private set; } = tracingData;

    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);

        writer.Write7BitEncodedInt(TracingData.Count);
        foreach (KeyValuePair<string, TimeSpan> kvp in TracingData)
        {
            writer.Write(kvp.Key);
            writer.Write(kvp.Value.Ticks);
        }
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);

        int count = reader.Read7BitEncodedInt();
        TracingData = new Dictionary<string, TimeSpan>(count);
        for (int i = 0; i < count; i++)
        {
            string key = reader.ReadString();
            TimeSpan value = TimeSpan.FromTicks(reader.ReadInt64());

            TracingData.Add(key, value);
        }
    }
}

public sealed class BuildCheckAcquisitionEventArgs(string acquisitionData) : BuildCheckEventArgs
{
    internal BuildCheckAcquisitionEventArgs() : this(string.Empty)
    { }

    public string AcquisitionData { get; private set; } = acquisitionData;

    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);

        writer.Write(AcquisitionData);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);

        AcquisitionData = reader.ReadString();
    }
}
public sealed class BuildCheckResultWarning : BuildWarningEventArgs
{
    public BuildCheckResultWarning(IBuildCheckResult result)
    {
        this.Message = result.FormatMessage();
    }

    internal BuildCheckResultWarning() { }

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

public sealed class BuildCheckResultError : BuildErrorEventArgs
{
    public BuildCheckResultError(IBuildCheckResult result)
    {
        this.Message = result.FormatMessage();
    }

    internal BuildCheckResultError() { }

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

public sealed class BuildCheckResultMessage : BuildMessageEventArgs
{
    public BuildCheckResultMessage(IBuildCheckResult result)
    {
        this.Message = result.FormatMessage();
    }

    internal BuildCheckResultMessage() { }

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
