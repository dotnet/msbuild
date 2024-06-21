// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.Shared;

internal static class EventsCreatorHelper
{
    public static BuildMessageEventArgs CreateMessageEventFromText(BuildEventContext buildEventContext, MessageImportance importance, string message, params object?[]? messageArgs)
    {
        ErrorUtilities.VerifyThrowInternalNull(buildEventContext, nameof(buildEventContext));
        ErrorUtilities.VerifyThrowInternalNull(message, nameof(message));

        BuildMessageEventArgs buildEvent = new BuildMessageEventArgs(
                message,
                helpKeyword: null,
                senderName: "MSBuild",
                importance,
                DateTime.UtcNow,
                messageArgs);
        buildEvent.BuildEventContext = buildEventContext;

        return buildEvent;
    }

    public static BuildErrorEventArgs CreateErrorEventFromText(BuildEventContext buildEventContext, string? subcategoryResourceName, string? errorCode, string? helpKeyword, BuildEventFileInfo file, string message)
    {
        ErrorUtilities.VerifyThrowInternalNull(buildEventContext, nameof(buildEventContext));
        ErrorUtilities.VerifyThrowInternalNull(file, nameof(file));
        ErrorUtilities.VerifyThrowInternalNull(message, nameof(message));

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

        buildEvent.BuildEventContext = buildEventContext;

        return buildEvent;
    }
}
