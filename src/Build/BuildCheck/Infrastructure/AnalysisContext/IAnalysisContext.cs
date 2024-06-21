// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Interface that contains an instance of <see cref="BuildEventContext"/> and methods to dispatch it.
/// </summary>
internal interface IAnalysisContext
{
    /// <summary>
    /// Instance of <see cref="BuildEventContext"/>.
    /// </summary>
    BuildEventContext BuildEventContext { get; }

    /// <summary>
    /// Dispatch the instance of <see cref="BuildEventContext"/> as a comment.
    /// </summary>
    void DispatchAsComment(MessageImportance importance, string messageResourceName, params object?[] messageArgs);

    /// <summary>
    /// Dispatch a <see cref="BuildEventArgs"/>.
    /// </summary>
    void DispatchBuildEvent(BuildEventArgs buildEvent);

    /// <summary>
    /// Dispatch the instance of <see cref="BuildEventContext"/> as an error message.
    /// </summary>
    void DispatchAsErrorFromText(string? subcategoryResourceName, string? errorCode, string? helpKeyword, BuildEventFileInfo file, string message);

    /// <summary>
    /// Dispatch the instance of <see cref="BuildEventContext"/> as a comment with provided text for the message.
    /// </summary>
    void DispatchAsCommentFromText(MessageImportance importance, string message);
}
