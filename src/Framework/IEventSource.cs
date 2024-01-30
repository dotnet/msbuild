// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

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

    /// <summary>
    /// Helper methods for <see cref="IEventSource"/> interface.
    /// </summary>
    public static class EventSourceExtensions
    {
        /// <summary>
        /// Helper method ensuring single deduplicated subscription to the <see cref="IEventSource.MessageRaised"/> event.
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="handler">Handler to the event. If this handler is already subscribed, single subscription will be ensured.</param>
        public static void HandleMessageRaised(this IEventSource eventSource, BuildMessageEventHandler handler)
        {
            eventSource.MessageRaised -= handler;
            eventSource.MessageRaised += handler;
        }

        /// <summary>
        /// Helper method ensuring single deduplicated subscription to the <see cref="IEventSource.ErrorRaised"/> event.
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="handler">Handler to the event. If this handler is already subscribed, single subscription will be ensured.</param>
        public static void HandleErrorRaised(this IEventSource eventSource, BuildErrorEventHandler handler)
        {
            eventSource.ErrorRaised -= handler;
            eventSource.ErrorRaised += handler;
        }

        /// <summary>
        /// Helper method ensuring single deduplicated subscription to the <see cref="IEventSource.WarningRaised"/> event.
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="handler">Handler to the event. If this handler is already subscribed, single subscription will be ensured.</param>
        public static void HandleWarningRaised(this IEventSource eventSource, BuildWarningEventHandler handler)
        {
            eventSource.WarningRaised -= handler;
            eventSource.WarningRaised += handler;
        }

        /// <summary>
        /// Helper method ensuring single deduplicated subscription to the <see cref="IEventSource.BuildStarted"/> event.
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="handler">Handler to the event. If this handler is already subscribed, single subscription will be ensured.</param>
        public static void HandleBuildStarted(this IEventSource eventSource, BuildStartedEventHandler handler)
        {
            eventSource.BuildStarted -= handler;
            eventSource.BuildStarted += handler;
        }

        /// <summary>
        /// Helper method ensuring single deduplicated subscription to the <see cref="IEventSource.BuildFinished"/> event.
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="handler">Handler to the event. If this handler is already subscribed, single subscription will be ensured.</param>
        public static void HandleBuildFinished(this IEventSource eventSource, BuildFinishedEventHandler handler)
        {
            eventSource.BuildFinished -= handler;
            eventSource.BuildFinished += handler;
        }

        /// <summary>
        /// Helper method ensuring single deduplicated subscription to the <see cref="IEventSource.ProjectStarted"/> event.
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="handler">Handler to the event. If this handler is already subscribed, single subscription will be ensured.</param>
        public static void HandleProjectStarted(this IEventSource eventSource, ProjectStartedEventHandler handler)
        {
            eventSource.ProjectStarted -= handler;
            eventSource.ProjectStarted += handler;
        }

        /// <summary>
        /// Helper method ensuring single deduplicated subscription to the <see cref="IEventSource.ProjectFinished"/> event.
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="handler">Handler to the event. If this handler is already subscribed, single subscription will be ensured.</param>
        public static void HandleProjectFinished(this IEventSource eventSource, ProjectFinishedEventHandler handler)
        {
            eventSource.ProjectFinished -= handler;
            eventSource.ProjectFinished += handler;
        }


        /// <summary>
        /// Helper method ensuring single deduplicated subscription to the <see cref="IEventSource.TargetStarted"/> event.
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="handler">Handler to the event. If this handler is already subscribed, single subscription will be ensured.</param>
        public static void HandleTargetStarted(this IEventSource eventSource, TargetStartedEventHandler handler)
        {
            eventSource.TargetStarted -= handler;
            eventSource.TargetStarted += handler;
        }

        /// <summary>
        /// Helper method ensuring single deduplicated subscription to the <see cref="IEventSource.TargetFinished"/> event.
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="handler">Handler to the event. If this handler is already subscribed, single subscription will be ensured.</param>
        public static void HandleTargetFinished(this IEventSource eventSource, TargetFinishedEventHandler handler)
        {
            eventSource.TargetFinished -= handler;
            eventSource.TargetFinished += handler;
        }

        /// <summary>
        /// Helper method ensuring single deduplicated subscription to the <see cref="IEventSource.TaskStarted"/> event.
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="handler">Handler to the event. If this handler is already subscribed, single subscription will be ensured.</param>
        public static void HandleTaskStarted(this IEventSource eventSource, TaskStartedEventHandler handler)
        {
            eventSource.TaskStarted -= handler;
            eventSource.TaskStarted += handler;
        }

        /// <summary>
        /// Helper method ensuring single deduplicated subscription to the <see cref="IEventSource.TaskFinished"/> event.
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="handler">Handler to the event. If this handler is already subscribed, single subscription will be ensured.</param>
        public static void HandleTaskFinished(this IEventSource eventSource, TaskFinishedEventHandler handler)
        {
            eventSource.TaskFinished -= handler;
            eventSource.TaskFinished += handler;
        }

        /// <summary>
        /// Helper method ensuring single deduplicated subscription to the <see cref="IEventSource.CustomEventRaised"/> event.
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="handler">Handler to the event. If this handler is already subscribed, single subscription will be ensured.</param>
        public static void HandleCustomEventRaised(this IEventSource eventSource, CustomBuildEventHandler handler)
        {
            eventSource.CustomEventRaised -= handler;
            eventSource.CustomEventRaised += handler;
        }

        /// <summary>
        /// Helper method ensuring single deduplicated subscription to the <see cref="IEventSource.StatusEventRaised"/> event.
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="handler">Handler to the event. If this handler is already subscribed, single subscription will be ensured.</param>
        public static void HandleStatusEventRaised(this IEventSource eventSource, BuildStatusEventHandler handler)
        {
            eventSource.StatusEventRaised -= handler;
            eventSource.StatusEventRaised += handler;
        }

        /// <summary>
        /// Helper method ensuring single deduplicated subscription to the <see cref="IEventSource.AnyEventRaised"/> event.
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="handler">Handler to the event. If this handler is already subscribed, single subscription will be ensured.</param>
        public static void HandleAnyEventRaised(this IEventSource eventSource, AnyEventHandler handler)
        {
            eventSource.AnyEventRaised -= handler;
            eventSource.AnyEventRaised += handler;
        }
    }
}
