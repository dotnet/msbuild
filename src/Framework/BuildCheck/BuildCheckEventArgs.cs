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

/// <summary>
/// Base class for all build check event args.
/// Not intended to be extended by external code.
/// </summary>
internal abstract class BuildCheckEventArgs : BuildEventArgs
{ }

/// <summary>
/// Transport mean for the BuildCheck tracing data from additional nodes.
/// </summary>
/// <param name="tracingData"></param>
internal sealed class BuildCheckTracingEventArgs(Dictionary<string, TimeSpan> tracingData) : BuildCheckEventArgs
{
    internal BuildCheckTracingEventArgs()
        : this([])
    {
    }

    internal BuildCheckTracingEventArgs(Dictionary<string, TimeSpan> data, bool isAggregatedGlobalReport) : this(data)
    {
        IsAggregatedGlobalReport = isAggregatedGlobalReport;
    }

    /// <summary>
    /// When true, the tracing information is from the whole build for logging purposes
    /// When false, the tracing is being used for communication between nodes and central process
    /// </summary>
    public bool IsAggregatedGlobalReport { get; private set; } = false;

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

internal sealed class BuildCheckAcquisitionEventArgs(string acquisitionPath, string projectPath) : BuildCheckEventArgs
{
    internal BuildCheckAcquisitionEventArgs()
        : this(string.Empty, string.Empty)
    {
    }

    /// <summary>
    /// Gets the path to the analyzer assembly that needs to be loaded into the application context.
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
    public BuildCheckResultWarning(IBuildCheckResult result, string code)
        : base(subcategory: null, code: code, file: result.ProjectFile, lineNumber: 0, columnNumber: 0, endLineNumber: 0, endColumnNumber: 0, message: result.FormatMessage(), helpKeyword: null, senderName: null)
        => RawMessage = result.FormatMessage();

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
    public BuildCheckResultError(IBuildCheckResult result, string code)
        : base(subcategory: null, code: code, file: result.ProjectFile, lineNumber: 0, columnNumber: 0, endLineNumber: 0, endColumnNumber: 0, message: result.FormatMessage(), helpKeyword: null, senderName: null)
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
    {
        RawMessage = result.FormatMessage();
        File = result.ProjectFile;
    }

    internal BuildCheckResultMessage() { }

    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);

        writer.Write(RawMessage!);
        writer.Write(File);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);

        RawMessage = reader.ReadString();
        File = reader.ReadString();
    }
}
