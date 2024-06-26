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
/// <see cref="IAnalysisContext"/> that uses <see cref="ILoggingService"/> to dispatch.
/// </summary>
internal class AnalysisLoggingContext : IAnalysisContext
{
    private readonly ILoggingService _loggingService;
    private readonly BuildEventContext _eventContext;

    public AnalysisLoggingContext(ILoggingService loggingService, BuildEventContext eventContext)
    {
        _loggingService = loggingService;
        _eventContext = eventContext;
    }

    public BuildEventContext BuildEventContext => _eventContext;

    public void DispatchBuildEvent(BuildEventArgs buildEvent)
        => _loggingService
            .LogBuildEvent(buildEvent);

    public void DispatchAsComment(MessageImportance importance, string messageResourceName, params object?[] messageArgs)
        => _loggingService
            .LogComment(_eventContext, importance, messageResourceName, messageArgs);

    public void DispatchAsCommentFromText(MessageImportance importance, string message)
        => _loggingService
            .LogCommentFromText(_eventContext, importance, message);

    public void DispatchAsErrorFromText(string? subcategoryResourceName, string? errorCode, string? helpKeyword, BuildEventFileInfo file, string message)
        => _loggingService
            .LogErrorFromText(_eventContext, subcategoryResourceName, errorCode, helpKeyword, file, message);
}
