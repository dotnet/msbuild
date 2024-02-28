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

namespace Microsoft.Build.Experimental.BuildCop;

public abstract class BuildCopEventArgs : BuildEventArgs
{ }

public sealed class BuildCopTracingEventArgs(Dictionary<string, TimeSpan> tracingData) : BuildCopEventArgs
{
    internal BuildCopTracingEventArgs() : this(new Dictionary<string, TimeSpan>())
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

public sealed class BuildCopAcquisitionEventArgs(string acquisitionData) : BuildCopEventArgs
{
    internal BuildCopAcquisitionEventArgs() : this(string.Empty)
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
public sealed class BuildCopResultWarning : BuildWarningEventArgs
{
    public BuildCopResultWarning(IBuildCopResult result)
    {
        this.Message = result.FormatMessage();
    }

    internal BuildCopResultWarning() { }

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

public sealed class BuildCopResultError : BuildErrorEventArgs
{
    public BuildCopResultError(IBuildCopResult result)
    {
        this.Message = result.FormatMessage();
    }

    internal BuildCopResultError() { }

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

public sealed class BuildCopResultMessage : BuildMessageEventArgs
{
    public BuildCopResultMessage(IBuildCopResult result)
    {
        this.Message = result.FormatMessage();
    }

    internal BuildCopResultMessage() { }

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
