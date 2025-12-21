// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.App;

/// <summary>
/// Enumeration of the various ways in which the MSBuild.exe application can exit.
/// </summary>
[SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Justification = "shipped already")]
public enum ExitType
{
    /// <summary>
    /// The application executed successfully.
    /// </summary>
    Success,
    /// <summary>
    /// There was a syntax error in a command line argument.
    /// </summary>
    SwitchError,
    /// <summary>
    /// A command line argument was not valid.
    /// </summary>
    InitializationError,
    /// <summary>
    /// The build failed.
    /// </summary>
    BuildError,
    /// <summary>
    /// A logger aborted the build.
    /// </summary>
    LoggerAbort,
    /// <summary>
    /// A logger failed unexpectedly.
    /// </summary>
    LoggerFailure,
    /// <summary>
    /// The build stopped unexpectedly, for example,
    /// because a child died or hung.
    /// </summary>
    Unexpected,
    /// <summary>
    /// A project cache failed unexpectedly.
    /// </summary>
    ProjectCacheFailure,
    /// <summary>
    /// The client for MSBuild server failed unexpectedly, for example,
    /// because the server process died or hung.
    /// </summary>
    MSBuildClientFailure
}
