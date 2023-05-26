// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
#if !MSBUILD_FRAMEWORK && !TASKHOST
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Internal;
#else
namespace Microsoft.Build.BackEnd;
#endif

public abstract class BuildExceptionBase : Exception
{
    private string? _remoteTypeName;
    private string? _remoteStackTrace;

    protected internal BuildExceptionBase()
        : base()
    { }

    protected internal BuildExceptionBase(string message)
        : base(message)
    { }

    protected internal BuildExceptionBase(
        string message,
        Exception? inner)
        : base(message, inner)
    { }

    // This is needed as soon as we allow opt out of the non-BinaryFormatter serialization
    protected internal BuildExceptionBase(SerializationInfo info, StreamingContext context)
        : base(info, context)
    { }

    public override string? StackTrace => string.IsNullOrEmpty(_remoteStackTrace) ? base.StackTrace : _remoteStackTrace;

    public override string ToString() => string.IsNullOrEmpty(_remoteTypeName) ? base.ToString() : $"{_remoteTypeName}->{base.ToString()}";

    protected internal virtual void InitializeCustomState(IDictionary<string, string?>? customKeyedSerializedData)
    { /* This is it. Override for exceptions with custom state */ }

    protected internal virtual IDictionary<string, string?>? FlushCustomState()
    {
        /* This is it. Override for exceptions with custom state */
        return null;
    }

    // Do not remove - accessed via reflection
    //  we cannot use strong typed method, as InvalidProjectFileException needs to be independent on the base in Microsoft.Build.Framework
    //  (that's given by the legacy need of nuget.exe to call SolutionFile utils from Microsoft.Build without proper loading Microsoft.Build.Framework)
    private void InitializeFromRemoteState(BuildExceptionRemoteState remoteState)
    {
        _remoteTypeName = remoteState.RemoteTypeName;
        _remoteStackTrace = remoteState.RemoteStackTrace;
        base.Source = remoteState.Source;
        base.HelpLink = remoteState.HelpLink;
        base.HResult = remoteState.HResult;
        if (remoteState.Source != null)
        {
            InitializeCustomState(remoteState.CustomKeyedSerializedData);
        }
    }
}
