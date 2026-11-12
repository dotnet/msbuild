// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Interface that contains an instance of <see cref="BuildEventContext"/> and methods to dispatch it.
/// </summary>
internal interface ICheckContext
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
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="errorCode">The error code, or <see langword="null"/>.</param>
    /// <param name="helpKeyword">The help keyword, or <see langword="null"/>.</param>
    /// <param name="location">The location associated with the error.</param>
    /// <param name="message">The error message.</param>
    void DispatchAsErrorFromText(DiagnosticSubcategory subcategory, string? errorCode, string? helpKeyword, IElementLocation location, string message);

    /// <summary>
    /// Dispatch the instance of <see cref="BuildEventContext"/> as a comment with provided text for the message.
    /// </summary>
    void DispatchAsCommentFromText(MessageImportance importance, string message);

    /// <summary>
    /// Dispatch the instance of <see cref="BuildEventContext"/> as a warning message.
    /// </summary>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="warningCode">The warning code, or <see langword="null"/>.</param>
    /// <param name="helpKeyword">The help keyword, or <see langword="null"/>.</param>
    /// <param name="location">The location associated with the warning.</param>
    /// <param name="message">The warning message.</param>
    void DispatchAsWarningFromText(DiagnosticSubcategory subcategory, string? warningCode, string? helpKeyword, IElementLocation location, string message);

    /// <summary>
    /// Dispatch the telemetry data for a failed acquisition.
    /// </summary>
    void DispatchFailedAcquisitionTelemetry(string assemblyName, Exception exception);

    /// <summary>
    /// If supported - dispatches the telemetry data.
    /// </summary>
    void DispatchTelemetry(BuildCheckTracingData data);
}
