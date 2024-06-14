// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// An implementation of IEventSource that raises appropriate events for a provided BuildEventArgs object.
    /// </summary>
    /// <remarks>This class is public because BinaryLogReplayEventSource is a derived class.
    /// This is abstracted into its own class because it's a useful single-purpose helper that
    /// can be used independently as a generic implementation of IEventSource.</remarks>
    public class EventArgsDispatcher : IEventSource
    {
        /// <summary>
        /// This event is raised for all BuildEventArgs objects after a more type-specific event
        /// </summary>
        public event AnyEventHandler AnyEventRaised;

        /// <summary>
        /// Raised for BuildStatusEventArgs instances
        /// </summary>
        public event BuildStatusEventHandler StatusEventRaised;

        /// <summary>
        /// Raised for CustomBuildEventArgs instances
        /// </summary>
        public event CustomBuildEventHandler CustomEventRaised;

        /// <summary>
        /// Raised for BuildStartedEventArgs instances
        /// </summary>
        public event BuildStartedEventHandler BuildStarted;

        /// <summary>
        /// Raised for BuildFinishedEventArgs instances
        /// </summary>
        public event BuildFinishedEventHandler BuildFinished;

        /// <summary>
        /// Raised for ProjectStartedEventArgs instances
        /// </summary>
        public event ProjectStartedEventHandler ProjectStarted;

        /// <summary>
        /// Raised for ProjectFinishedEventArgs instances
        /// </summary>
        public event ProjectFinishedEventHandler ProjectFinished;

        /// <summary>
        /// Raised for TargetStartedEventArgs instances
        /// </summary>
        public event TargetStartedEventHandler TargetStarted;

        /// <summary>
        /// Raised for TargetFinishedEventArgs instances
        /// </summary>
        public event TargetFinishedEventHandler TargetFinished;

        /// <summary>
        /// Raised for TaskStartedEventArgs instances
        /// </summary>
        public event TaskStartedEventHandler TaskStarted;

        /// <summary>
        /// Raised for TaskFinishedEventArgs instances
        /// </summary>
        public event TaskFinishedEventHandler TaskFinished;

        /// <summary>
        /// Raised for BuildErrorEventArgs instances
        /// </summary>
        public event BuildErrorEventHandler ErrorRaised;

        /// <summary>
        /// Raised for BuildWarningEventArgs instances
        /// </summary>
        public event BuildWarningEventHandler WarningRaised;

        /// <summary>
        /// Raised for BuildMessageEventArgs instances
        /// </summary>
        public event BuildMessageEventHandler MessageRaised;

        internal bool HasStructuredEventsSubscribers =>
            AnyEventRaised != null ||
            StatusEventRaised != null ||
            CustomEventRaised != null ||
            BuildStarted != null ||
            BuildFinished != null ||
            ProjectStarted != null ||
            ProjectFinished != null ||
            TargetStarted != null ||
            TargetFinished != null ||
            TaskStarted != null ||
            TaskFinished != null ||
            ErrorRaised != null ||
            WarningRaised != null ||
            MessageRaised != null;

        /// <summary>
        /// Raise one of the events that is appropriate for the type of the BuildEventArgs
        /// </summary>
        public void Dispatch(BuildEventArgs buildEvent)
        {
            if (buildEvent is BuildMessageEventArgs buildMessageEventArgs)
            {
                MessageRaised?.Invoke(null, buildMessageEventArgs);
            }
            else if (buildEvent is TaskStartedEventArgs taskStartedEventArgs)
            {
                TaskStarted?.Invoke(null, taskStartedEventArgs);
            }
            else if (buildEvent is TaskFinishedEventArgs taskFinishedEventArgs)
            {
                TaskFinished?.Invoke(null, taskFinishedEventArgs);
            }
            else if (buildEvent is TargetStartedEventArgs targetStartedEventArgs)
            {
                TargetStarted?.Invoke(null, targetStartedEventArgs);
            }
            else if (buildEvent is TargetFinishedEventArgs targetFinishedEventArgs)
            {
                TargetFinished?.Invoke(null, targetFinishedEventArgs);
            }
            else if (buildEvent is ProjectStartedEventArgs projectStartedEventArgs)
            {
                ProjectStarted?.Invoke(null, projectStartedEventArgs);
            }
            else if (buildEvent is ProjectFinishedEventArgs projectFinishedEventArgs)
            {
                ProjectFinished?.Invoke(null, projectFinishedEventArgs);
            }
            else if (buildEvent is BuildStartedEventArgs buildStartedEventArgs)
            {
                BuildStarted?.Invoke(null, buildStartedEventArgs);
            }
            else if (buildEvent is BuildFinishedEventArgs buildFinishedEventArgs)
            {
                BuildFinished?.Invoke(null, buildFinishedEventArgs);
            }
            else if (buildEvent is CustomBuildEventArgs customBuildEventArgs)
            {
                CustomEventRaised?.Invoke(null, customBuildEventArgs);
            }
            else if (buildEvent is BuildStatusEventArgs buildStatusEventArgs)
            {
                StatusEventRaised?.Invoke(null, buildStatusEventArgs);
            }
            else if (buildEvent is BuildWarningEventArgs buildWarningEventArgs)
            {
                WarningRaised?.Invoke(null, buildWarningEventArgs);
            }
            else if (buildEvent is BuildErrorEventArgs buildErrorEventArgs)
            {
                ErrorRaised?.Invoke(null, buildErrorEventArgs);
            }

            AnyEventRaised?.Invoke(null, buildEvent);
        }
    }
}
