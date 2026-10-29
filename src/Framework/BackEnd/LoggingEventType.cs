// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Telemetry;

namespace Microsoft.Build.Shared;

/// <summary>
/// An enumeration of all the types of BuildEventArgs that can be
/// packaged by this logMessagePacket
/// </summary>
/// <remarks>
/// Several of these values must be kept in sync with MSBuildTaskHost's LoggingEventType.
/// </remarks>
internal enum LoggingEventType
{
    /// <summary>
    /// An invalid eventId, used during initialization of a <see cref="LoggingEventType"/>.
    /// </summary>
    Invalid = -1,

    /// <summary>
    /// Event is a CustomEventArgs.
    /// </summary>
    CustomEvent = 0,

    /// <summary>
    /// Event is a <see cref="BuildErrorEventArgs"/>.
    /// </summary>
    BuildErrorEvent = 1,

    /// <summary>
    /// Event is a <see cref="BuildFinishedEventArgs"/>.
    /// </summary>
    BuildFinishedEvent = 2,

    /// <summary>
    /// Event is a <see cref="BuildMessageEventArgs"/>.
    /// </summary>
    BuildMessageEvent = 3,

    /// <summary>
    /// Event is a <see cref="BuildStartedEventArgs"/>.
    /// </summary>
    BuildStartedEvent = 4,

    /// <summary>
    /// Event is a <see cref="BuildWarningEventArgs"/>.
    /// </summary>
    BuildWarningEvent = 5,

    /// <summary>
    /// Event is a <see cref="ProjectFinishedEventArgs"/>.
    /// </summary>
    ProjectFinishedEvent = 6,

    /// <summary>
    /// Event is a <see cref="ProjectStartedEventArgs"/>.
    /// </summary>
    ProjectStartedEvent = 7,

    /// <summary>
    /// Event is a <see cref="TargetStartedEventArgs"/>.
    /// </summary>
    TargetStartedEvent = 8,

    /// <summary>
    /// Event is a <see cref="TargetFinishedEventArgs"/>.
    /// </summary>
    TargetFinishedEvent = 9,

    /// <summary>
    /// Event is a <see cref="TaskStartedEventArgs"/>.
    /// </summary>
    TaskStartedEvent = 10,

    /// <summary>
    /// Event is a <see cref="TaskFinishedEventArgs"/>.
    /// </summary>
    TaskFinishedEvent = 11,

    /// <summary>
    /// Event is a <see cref="TaskCommandLineEventArgs"/>.
    /// </summary>
    TaskCommandLineEvent = 12,

    /// <summary>
    /// Event is a <see cref="TaskParameterEventArgs"/>.
    /// </summary>
    TaskParameterEvent = 13,

    /// <summary>
    /// Event is a <see cref="ProjectEvaluationStartedEventArgs"/>.
    /// </summary>
    ProjectEvaluationStartedEvent = 14,

    /// <summary>
    /// Event is a <see cref="ProjectEvaluationFinishedEventArgs"/>.
    /// </summary>
    ProjectEvaluationFinishedEvent = 15,

    /// <summary>
    /// Event is a <see cref="ProjectImportedEventArgs"/>.
    /// </summary>
    ProjectImportedEvent = 16,

    /// <summary>
    /// Event is a <see cref="TargetSkippedEventArgs"/>.
    /// </summary>
    TargetSkipped = 17,

    /// <summary>
    /// Event is a <see cref="TelemetryEventArgs"/>.
    /// </summary>
    Telemetry = 18,

    /// <summary>
    /// Event is an <see cref="EnvironmentVariableReadEventArgs"/>.
    /// </summary>
    EnvironmentVariableReadEvent = 19,

    /// <summary>
    /// Event is a <see cref="ResponseFileUsedEventArgs"/>.
    /// </summary>
    ResponseFileUsedEvent = 20,

    /// <summary>
    /// Event is an <see cref="AssemblyLoadBuildEventArgs"/>.
    /// </summary>
    AssemblyLoadEvent = 21,

    /// <summary>
    /// Event is <see cref="ExternalProjectStartedEventArgs"/>.
    /// </summary>
    ExternalProjectStartedEvent = 22,

    /// <summary>
    /// Event is <see cref="ExternalProjectFinishedEventArgs"/>.
    /// </summary>
    ExternalProjectFinishedEvent = 23,

    /// <summary>
    /// Event is <see cref="ExtendedCustomBuildEventArgs"/>.
    /// </summary>
    ExtendedCustomEvent = 24,

    /// <summary>
    /// Event is <see cref="ExtendedBuildErrorEventArgs"/>.
    /// </summary>
    ExtendedBuildErrorEvent = 25,

    /// <summary>
    /// Event is <see cref="ExtendedBuildWarningEventArgs"/>.
    /// </summary>
    ExtendedBuildWarningEvent = 26,

    /// <summary>
    /// Event is <see cref="ExtendedBuildMessageEventArgs"/>.
    /// </summary>
    ExtendedBuildMessageEvent = 27,

    /// <summary>
    /// Event is <see cref="CriticalBuildMessageEventArgs"/>.
    /// </summary>
    CriticalBuildMessage = 28,

    /// <summary>
    /// Event is <see cref="MetaprojectGeneratedEventArgs"/>.
    /// </summary>
    MetaprojectGenerated = 29,

    /// <summary>
    /// Event is <see cref="PropertyInitialValueSetEventArgs"/>.
    /// </summary>
    PropertyInitialValueSet = 30,

    /// <summary>
    /// Event is <see cref="PropertyReassignmentEventArgs"/>.
    /// </summary>
    PropertyReassignment = 31,

    /// <summary>
    /// Event is <see cref="UninitializedPropertyReadEventArgs"/>.
    /// </summary>
    UninitializedPropertyRead = 32,

    /// <summary>
    /// Event is <see cref="ExtendedCriticalBuildMessageEventArgs"/>.
    /// </summary>
    ExtendedCriticalBuildMessageEvent = 33,

    /// <summary>
    /// Event is a <see cref="GeneratedFileUsedEventArgs"/>.
    /// </summary>
    GeneratedFileUsedEvent = 34,

    /// <summary>
    /// Event is <see cref="BuildCheckResultMessage"/>.
    /// </summary>
    BuildCheckMessageEvent = 35,

    /// <summary>
    /// Event is <see cref="BuildCheckResultWarning"/>.
    /// </summary>
    BuildCheckWarningEvent = 36,

    /// <summary>
    /// Event is <see cref="BuildCheckResultError"/>.
    /// </summary>
    BuildCheckErrorEvent = 37,

    /// <summary>
    /// Event is <see cref="BuildCheckTracingEventArgs"/>.
    /// </summary>
    BuildCheckTracingEvent = 38,

    /// <summary>
    /// Event is <see cref="BuildCheckAcquisitionEventArgs"/>.
    /// </summary>
    BuildCheckAcquisitionEvent = 39,

    /// <summary>
    /// Event is <see cref="BuildSubmissionStartedEventArgs"/>.
    /// </summary>
    BuildSubmissionStartedEvent = 40,

    /// <summary>
    /// Event is <see cref="BuildCanceledEventArgs"/>
    /// </summary>
    BuildCanceledEvent = 41,

    /// <summary>
    /// Event is <see cref="WorkerNodeTelemetryEventArgs"/>
    /// </summary>
    WorkerNodeTelemetryEvent = 42,

    /// <summary>
    /// Event is <see cref="LoggersRegisteredEventArgs"/>
    /// </summary>
    LoggersRegisteredEvent = 43,

    /// <summary>
    /// Event is <see cref="MSBuildServerLifecycleEventArgs"/>
    /// </summary>
    MSBuildServerLifecycleEvent = 44,

    /// <summary>
    /// Event is <see cref="AssemblyResolutionSearchTraceEventArgs"/>.
    /// </summary>
    AssemblyResolutionSearchTraceEvent = 45,

    /// <summary>
    /// Event is <see cref="AssemblyConflictDependencyDetailsMessageEventArgs"/>.
    /// </summary>
    AssemblyConflictDependencyDetailsEvent = 46,

    /// <summary>
    /// Event is <see cref="AssemblyConflictWarningEventArgs"/>.
    /// </summary>
    AssemblyConflictWarningEvent = 47,
}
