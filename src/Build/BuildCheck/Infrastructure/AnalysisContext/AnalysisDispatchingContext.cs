// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck;

internal class AnalysisDispatchingContext : IAnalysisContext
{
    private readonly EventArgsDispatcher _eventDispatcher;
    private readonly BuildEventContext _eventContext;

    public AnalysisDispatchingContext(
        EventArgsDispatcher eventDispatcher,
        BuildEventContext eventContext)
    {
        _eventDispatcher = eventDispatcher;
        _eventContext = eventContext;
    }

    public BuildEventContext BuildEventContext => _eventContext;

    public void DispatchBuildEvent(BuildEventArgs buildEvent)
    {
        ErrorUtilities.VerifyThrow(buildEvent != null, "buildEvent is null");

        BuildWarningEventArgs? warningEvent = buildEvent as BuildWarningEventArgs;
        BuildErrorEventArgs? errorEvent = buildEvent as BuildErrorEventArgs;

        _eventDispatcher.Dispatch(buildEvent);
    }

    public void DispatchAsComment(MessageImportance importance, string messageResourceName, params object?[] messageArgs)
    {
        ErrorUtilities.VerifyThrow(!string.IsNullOrEmpty(messageResourceName), "Need resource string for comment message.");

        DispatchAsCommentFromText(_eventContext, importance, ResourceUtilities.GetResourceString(messageResourceName), messageArgs);
    }

    public void DispatchAsCommentFromText(MessageImportance importance, string message)
        => DispatchAsCommentFromText(_eventContext, importance, message, messageArgs: null);

    private void DispatchAsCommentFromText(BuildEventContext buildEventContext, MessageImportance importance, string message, params object?[]? messageArgs)
    {
        ErrorUtilities.VerifyThrow(buildEventContext != null, "buildEventContext was null");
        ErrorUtilities.VerifyThrow(message != null, "message was null");

        BuildMessageEventArgs buildEvent = new BuildMessageEventArgs(
                message,
                helpKeyword: null,
                senderName: "MSBuild",
                importance,
                DateTime.UtcNow,
                messageArgs);
        buildEvent.BuildEventContext = buildEventContext;
        _eventDispatcher.Dispatch(buildEvent);
    }

    public void DispatchAsErrorFromText(string? subcategoryResourceName, string? errorCode, string? helpKeyword, BuildEventFileInfo file, string message)
    {
        ErrorUtilities.VerifyThrow(_eventContext != null, "Must specify the buildEventContext");
        ErrorUtilities.VerifyThrow(file != null, "Must specify the associated file.");
        ErrorUtilities.VerifyThrow(message != null, "Need error message.");

        string? subcategory = null;

        if (subcategoryResourceName != null)
        {
            subcategory = AssemblyResources.GetString(subcategoryResourceName);
        }

        BuildErrorEventArgs buildEvent =
        new BuildErrorEventArgs(
            subcategory,
            errorCode,
            file!.File,
            file.Line,
            file.Column,
            file.EndLine,
            file.EndColumn,
            message,
            helpKeyword,
            "MSBuild");

        buildEvent.BuildEventContext = _eventContext;

        _eventDispatcher.Dispatch(buildEvent);
    }
}
