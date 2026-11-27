// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd;

/// <summary>
/// How the task completed -- successful, failed, or crashed
/// </summary>
internal enum TaskCompleteType
{
    /// <summary>
    /// Task execution succeeded
    /// </summary>
    Success,

    /// <summary>
    /// Task execution failed
    /// </summary>
    Failure,

    /// <summary>
    /// Task crashed during initialization steps -- loading the task,
    /// validating or setting the parameters, etc.
    /// </summary>
    CrashedDuringInitialization,

    /// <summary>
    /// Task crashed while being executed
    /// </summary>
    CrashedDuringExecution,

    /// <summary>
    /// Task crashed after being executed
    /// -- Getting outputs, etc
    /// </summary>
    CrashedAfterExecution,
}
