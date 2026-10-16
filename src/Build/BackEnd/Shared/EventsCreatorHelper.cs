// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Utilities;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.Shared;

internal static class EventsCreatorHelper
{
    public static BuildMessageEventArgs CreateMessageEvent<TArgs>(
        ResourceMessage<TArgs> message,
        MessageImportance importance,
        BuildEventContext context)
        where TArgs : struct, IMessageArgs
    {
        Assumed.NotNull(context);
        Assumed.NotNullOrEmpty(message.ResourceName, "Need resource string for comment message.");

        BuildMessageEventArgs buildEvent = new(
            ResourceUtilities.GetResourceString(message.ResourceName),
            helpKeyword: null,
            senderName: "MSBuild",
            importance)
        {
            BuildEventContext = context,
        };

        SetArgs(buildEvent, message.Args);
        return buildEvent;
    }

    public static BuildMessageEventArgs CreateMessageEvent<TArgs>(
        ResourceMessage<TArgs> message,
        MessageImportance importance,
        IElementLocation location,
        BuildEventContext context)
        where TArgs : struct, IMessageArgs
    {
        Assumed.NotNull(context);
        Assumed.NotNull(location);
        Assumed.NotNullOrEmpty(message.ResourceName, "Need resource string for comment message.");

        BuildMessageEventArgs buildEvent = new(
            subcategory: null,
            code: null,
            location.File,
            location.Line,
            location.Column,
            endLineNumber: 0,
            endColumnNumber: 0,
            ResourceUtilities.GetResourceString(message.ResourceName),
            helpKeyword: null,
            senderName: "MSBuild",
            importance)
        {
            BuildEventContext = context,
        };

        SetArgs(buildEvent, message.Args);
        return buildEvent;
    }

    public static BuildMessageEventArgs CreateMessageEventFromText(
        BuildEventContext buildEventContext,
        MessageImportance importance,
        string message,
        params object?[]? messageArgs)
    {
        Assumed.NotNull(buildEventContext);
        Assumed.NotNull(message);

        return new BuildMessageEventArgs(
            message,
            helpKeyword: null,
            senderName: "MSBuild",
            importance,
            DateTime.UtcNow,
            messageArgs)
        {
            BuildEventContext = buildEventContext,
        };
    }

    public static BuildErrorEventArgs CreateErrorEventFromText(
        BuildEventContext buildEventContext,
        DiagnosticSubcategory subcategory,
        string? errorCode,
        string? helpKeyword,
        IElementLocation location,
        string message)
    {
        Assumed.NotNull(buildEventContext);
        Assumed.NotNull(location);
        Assumed.NotNull(message);

        return new(
            subcategory.GetDisplayString(),
            errorCode,
            location.File,
            location.Line,
            location.Column,
            endLineNumber: 0,
            endColumnNumber: 0,
            message,
            helpKeyword,
            senderName: "MSBuild")
        {
            BuildEventContext = buildEventContext,
        };
    }

    public static BuildWarningEventArgs CreateWarningEventFromText(
        BuildEventContext buildEventContext,
        DiagnosticSubcategory subcategory,
        string? warningCode,
        string? helpKeyword,
        IElementLocation location,
        string message)
    {
        Assumed.NotNull(buildEventContext);
        Assumed.NotNull(location);
        Assumed.NotNull(message);

        return new(
            subcategory.GetDisplayString(),
            warningCode,
            location.File,
            location.Line,
            location.Column,
            endLineNumber: 0,
            endColumnNumber: 0,
            message,
            helpKeyword,
            senderName: "MSBuild")
        {
            BuildEventContext = buildEventContext,
        };
    }

    private static void SetArgs<TArgs>(BuildMessageEventArgs buildEvent, TArgs args)
        where TArgs : struct, IMessageArgs
    {
        if (args.Count > 0)
        {
            buildEvent.SetArgs(args);
        }
    }
}
