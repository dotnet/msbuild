// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.TaskHost.Exceptions;

/// <summary>
/// Remote exception internal data serving as the source for the exception deserialization.
/// </summary>
internal class BuildExceptionRemoteState
{
    public string RemoteTypeName { get; }

    public string? RemoteStackTrace { get; }

    public string? Source { get; }

    public string? HelpLink { get; }

    public int HResult { get; }

    public IDictionary<string, string?>? CustomKeyedSerializedData { get; }

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
}
