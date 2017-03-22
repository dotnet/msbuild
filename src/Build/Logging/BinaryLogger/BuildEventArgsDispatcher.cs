using Microsoft.Build.Framework;

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

        /// <summary>
        /// Raise one of the events that is appropriate for the type of the BuildEventArgs
        /// </summary>
        public void Dispatch(BuildEventArgs buildEvent)
        {
            if (buildEvent is BuildMessageEventArgs)
            {
                MessageRaised?.Invoke(null, (BuildMessageEventArgs)buildEvent);
            }
            else if (buildEvent is TaskStartedEventArgs)
            {
                TaskStarted?.Invoke(null, (TaskStartedEventArgs)buildEvent);
            }
            else if (buildEvent is TaskFinishedEventArgs)
            {
                TaskFinished?.Invoke(null, (TaskFinishedEventArgs)buildEvent);
            }
            else if (buildEvent is TargetStartedEventArgs)
            {
                TargetStarted?.Invoke(null, (TargetStartedEventArgs)buildEvent);
            }
            else if (buildEvent is TargetFinishedEventArgs)
            {
                TargetFinished?.Invoke(null, (TargetFinishedEventArgs)buildEvent);
            }
            else if (buildEvent is ProjectStartedEventArgs)
            {
                ProjectStarted?.Invoke(null, (ProjectStartedEventArgs)buildEvent);
            }
            else if (buildEvent is ProjectFinishedEventArgs)
            {
                ProjectFinished?.Invoke(null, (ProjectFinishedEventArgs)buildEvent);
            }
            else if (buildEvent is BuildStartedEventArgs)
            {
                BuildStarted?.Invoke(null, (BuildStartedEventArgs)buildEvent);
            }
            else if (buildEvent is BuildFinishedEventArgs)
            {
                BuildFinished?.Invoke(null, (BuildFinishedEventArgs)buildEvent);
            }
            else if (buildEvent is CustomBuildEventArgs)
            {
                CustomEventRaised?.Invoke(null, (CustomBuildEventArgs)buildEvent);
            }
            else if (buildEvent is BuildStatusEventArgs)
            {
                StatusEventRaised?.Invoke(null, (BuildStatusEventArgs)buildEvent);
            }
            else if (buildEvent is BuildWarningEventArgs)
            {
                WarningRaised?.Invoke(null, (BuildWarningEventArgs)buildEvent);
            }
            else if (buildEvent is BuildErrorEventArgs)
            {
                ErrorRaised?.Invoke(null, (BuildErrorEventArgs)buildEvent);
            }

            AnyEventRaised?.Invoke(null, buildEvent);
        }
    }
}
