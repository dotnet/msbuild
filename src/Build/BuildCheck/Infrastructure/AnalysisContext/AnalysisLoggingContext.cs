// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// <see cref="IAnalysisContext"/> that uses <see cref="ILoggingService"/> to dispatch.
/// </summary>
/// <remarks>
/// Making this a record struct to avoid allocations (unless called through interface - which leads to boxing).
/// This is wanted since this can be used in a hot path (of property reads and writes)
/// </remarks>
internal readonly struct AnalysisLoggingContext(ILoggingService loggingService, BuildEventContext eventContext)
    : IAnalysisContext
{
    public BuildEventContext BuildEventContext => eventContext;

    public void DispatchBuildEvent(BuildEventArgs buildEvent)
    {
        // When logging happens out of process, we need to map the project context id to the project file on the receiving side.
        if (ShouldUpdateProjectFileMap(buildEvent))
        {
            UpdateProjectFileMap(buildEvent);
        }

        loggingService.LogBuildEvent(buildEvent);
    }

    public void DispatchAsComment(MessageImportance importance, string messageResourceName, params object?[] messageArgs)
        => loggingService
            .LogComment(eventContext, importance, messageResourceName, messageArgs);

    public void DispatchAsCommentFromText(MessageImportance importance, string message)
        => loggingService
            .LogCommentFromText(eventContext, importance, message);

    public void DispatchAsErrorFromText(string? subcategoryResourceName, string? errorCode, string? helpKeyword, BuildEventFileInfo file, string message)
        => loggingService
            .LogErrorFromText(eventContext, subcategoryResourceName, errorCode, helpKeyword, file, message);

    private bool ShouldUpdateProjectFileMap(BuildEventArgs buildEvent) => buildEvent.BuildEventContext != null &&
               buildEvent.BuildEventContext.ProjectContextId != BuildEventContext.InvalidProjectContextId &&
               !loggingService.ProjectFileMap.ContainsKey(buildEvent.BuildEventContext.ProjectContextId);

    private void UpdateProjectFileMap(BuildEventArgs buildEvent)
    {
        string file = GetFileFromBuildEvent(buildEvent);
        if (!string.IsNullOrEmpty(file))
        {
            loggingService.ProjectFileMap[buildEvent.BuildEventContext!.ProjectContextId] = file;
        }
    }

    private string GetFileFromBuildEvent(BuildEventArgs buildEvent) => buildEvent switch
    {
        BuildWarningEventArgs we => we.File,
        BuildErrorEventArgs ee => ee.File,
        BuildMessageEventArgs me => me.File,
        _ => string.Empty,
    };
}
