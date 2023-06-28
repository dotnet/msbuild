// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Framework.BuildException;

/// <summary>
/// Remote exception internal data serving as the source for the exception deserialization.
/// </summary>
internal class BuildExceptionRemoteState
{
    public BuildExceptionRemoteState(
        string remoteTypeName,
        string? remoteStackTrace,
        string? source,
        string? helpLink,
        int hResult,
        IDictionary<string, string?>? customKeyedSerializedData)
    {
        RemoteTypeName = remoteTypeName;
        RemoteStackTrace = remoteStackTrace;
        Source = source;
        HelpLink = helpLink;
        HResult = hResult;
        CustomKeyedSerializedData = customKeyedSerializedData;
    }

    public string RemoteTypeName { get; init; }
    public string? RemoteStackTrace { get; init; }
    public string? Source { get; init; }
    public string? HelpLink { get; init; }
    public int HResult { get; init; }
    public IDictionary<string, string?>? CustomKeyedSerializedData { get; init; }
}
