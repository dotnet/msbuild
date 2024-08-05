// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// <see cref="ICheckContext"/> that uses <see cref="ILoggingService"/> to dispatch.
/// </summary>
/// <remarks>
/// Making this a record struct to avoid allocations (unless called through interface - which leads to boxing).
/// This is wanted since this can be used in a hot path (of property reads and writes)
/// </remarks>
internal readonly struct AnalysisLoggingContext(ILoggingService loggingService, BuildEventContext eventContext)
    : ICheckContext
{
    public BuildEventContext BuildEventContext => eventContext;

    public void DispatchBuildEvent(BuildEventArgs buildEvent)
        => loggingService
            .LogBuildEvent(buildEvent);

    public void DispatchAsComment(MessageImportance importance, string messageResourceName, params object?[] messageArgs)
        => loggingService
            .LogComment(eventContext, importance, messageResourceName, messageArgs);

    public void DispatchAsCommentFromText(MessageImportance importance, string message)
        => loggingService
            .LogCommentFromText(eventContext, importance, message);

    public void DispatchAsErrorFromText(string? subcategoryResourceName, string? errorCode, string? helpKeyword, BuildEventFileInfo file, string message)
        => loggingService
            .LogErrorFromText(eventContext, subcategoryResourceName, errorCode, helpKeyword, file, message);
}
