// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Type of handler for MessageRaised events
    /// </summary>
    public delegate void BuildMessageEventHandler(object sender, BuildMessageEventArgs e);

    /// <summary>
    /// Type of handler for ErrorRaised events
    /// </summary>
    public delegate void BuildErrorEventHandler(object sender, BuildErrorEventArgs e);

    /// <summary>
    /// Type of handler for WarningRaised events
    /// </summary>
    public delegate void BuildWarningEventHandler(object sender, BuildWarningEventArgs e);

    /// <summary>
    /// Type of handler for CustomEventRaised events
    /// </summary>
    public delegate void CustomBuildEventHandler(object sender, CustomBuildEventArgs e);

    /// <summary>
    /// Type of handler for BuildStartedEvent events
    /// </summary>
    public delegate void BuildStartedEventHandler(object sender, BuildStartedEventArgs e);

    /// <summary>
    /// Type of handler for BuildFinishedEvent events
    /// </summary>
    public delegate void BuildFinishedEventHandler(object sender, BuildFinishedEventArgs e);

    /// <summary>
    /// Type of handler for ProjectStarted events
    /// </summary>
    public delegate void ProjectStartedEventHandler(object sender, ProjectStartedEventArgs e);

    /// <summary>
    /// Type of handler for ProjectFinished events
    /// </summary>
    public delegate void ProjectFinishedEventHandler(object sender, ProjectFinishedEventArgs e);

    /// <summary>
    /// Type of handler for TargetStarted events
    /// </summary>
    public delegate void TargetStartedEventHandler(object sender, TargetStartedEventArgs e);

    /// <summary>
    /// Type of handler for TargetFinished events
    /// </summary>
    public delegate void TargetFinishedEventHandler(object sender, TargetFinishedEventArgs e);

    /// <summary>
    /// Type of handler for TaskStarted events
    /// </summary>
    public delegate void TaskStartedEventHandler(object sender, TaskStartedEventArgs e);

    /// <summary>
    /// Type of handler for TaskFinished events
    /// </summary>
    public delegate void TaskFinishedEventHandler(object sender, TaskFinishedEventArgs e);

    /// <summary>
    /// Type of handler for BuildStatus events
    /// </summary>
    public delegate void BuildStatusEventHandler(object sender, BuildStatusEventArgs e);

    /// <summary>
    /// Type of handler for AnyEventRaised events
    /// </summary>
    public delegate void AnyEventHandler(object sender, BuildEventArgs e);

    /// <summary> 
    /// This interface defines the events raised by the build engine.
    /// Loggers use this interface to subscribe to the events they
    /// are interested in receiving.
    /// </summary>
    public interface IEventSource
    {
        /// <summary>
        /// this event is raised to log a message
        /// </summary>
        event BuildMessageEventHandler MessageRaised;

        /// <summary>
        /// this event is raised to log an error
        /// </summary>
        event BuildErrorEventHandler ErrorRaised;

        /// <summary>
        /// this event is raised to log a warning
        /// </summary>
        event BuildWarningEventHandler WarningRaised;

        /// <summary>
        /// this event is raised to log the start of a build
        /// </summary>
        event BuildStartedEventHandler BuildStarted;

        /// <summary>
        /// this event is raised to log the end of a build
        /// </summary>
        event BuildFinishedEventHandler BuildFinished;

        /// <summary>
        /// this event is raised to log the start of a project build
        /// </summary>
        event ProjectStartedEventHandler ProjectStarted;

        /// <summary>
        /// this event is raised to log the end of a project build
        /// </summary>
        event ProjectFinishedEventHandler ProjectFinished;

        /// <summary>
        /// this event is raised to log the start of a target build
        /// </summary>
        event TargetStartedEventHandler TargetStarted;

        /// <summary>
        /// this event is raised to log the end of a target build
        /// </summary>
        event TargetFinishedEventHandler TargetFinished;

        /// <summary>
        /// this event is raised to log the start of task execution
        /// </summary>
        event TaskStartedEventHandler TaskStarted;

        /// <summary>
        /// this event is raised to log the end of task execution
        /// </summary>
        event TaskFinishedEventHandler TaskFinished;

        /// <summary>
        /// this event is raised to log custom events
        /// </summary>
        event CustomBuildEventHandler CustomEventRaised;

        /// <summary>
        /// this event is raised to log any build status event
        /// </summary>
        event BuildStatusEventHandler StatusEventRaised;

        /// <summary>
        /// this event is raised to log any build event.  These events do not include telemetry.  To receive telemetry, you must attach to the <see cref="IEventSource2.TelemetryLogged"/> event.
        /// </summary>
        event AnyEventHandler AnyEventRaised;
    }
}
