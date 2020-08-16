using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// An implementation of IEventSource that raises appropriate events for a provided BuildEventArgs object.
    /// </summary>
    /// <remarks>This class is public because BinaryLogReplayEventSource is a derived class.
    /// This is abstracted into its own class because it's a useful single-purpose helper that
    /// can be used independently as a generic implementation of IEventSource.</remarks>
    public class EventArgsDispatcher : IEventSource, IBuildEventArgsDispatchVisitor
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
            buildEvent.Visit(this);
            AnyEventRaised?.Invoke(null, buildEvent);
        }

        void IBuildEventArgsDispatchVisitor.Visit(BuildEventArgs buildEventArgs) { /* AnyEventRaised will be called by Dispatch */ }

        void IBuildEventArgsDispatchVisitor.Visit(BuildMessageEventArgs buildEventArgs) => MessageRaised?.Invoke(null, buildEventArgs);

        void IBuildEventArgsDispatchVisitor.Visit(TaskStartedEventArgs buildEventArgs) => TaskStarted?.Invoke(null, buildEventArgs);

        void IBuildEventArgsDispatchVisitor.Visit(TaskFinishedEventArgs buildEventArgs) => TaskFinished?.Invoke(null, buildEventArgs);

        void IBuildEventArgsDispatchVisitor.Visit(TargetStartedEventArgs buildEventArgs) => TargetStarted?.Invoke(null, buildEventArgs);

        void IBuildEventArgsDispatchVisitor.Visit(TargetFinishedEventArgs buildEventArgs) => TargetFinished?.Invoke(null, buildEventArgs);

        void IBuildEventArgsDispatchVisitor.Visit(ProjectStartedEventArgs buildEventArgs) => ProjectStarted?.Invoke(null, buildEventArgs);

        void IBuildEventArgsDispatchVisitor.Visit(ProjectFinishedEventArgs buildEventArgs) => ProjectFinished?.Invoke(null, buildEventArgs);

        void IBuildEventArgsDispatchVisitor.Visit(BuildStartedEventArgs buildEventArgs) => BuildStarted?.Invoke(null, buildEventArgs);

        void IBuildEventArgsDispatchVisitor.Visit(BuildFinishedEventArgs buildEventArgs) => BuildFinished?.Invoke(null, buildEventArgs);

        void IBuildEventArgsDispatchVisitor.Visit(CustomBuildEventArgs buildEventArgs) => CustomEventRaised?.Invoke(null, buildEventArgs);

        void IBuildEventArgsDispatchVisitor.Visit(BuildStatusEventArgs buildEventArgs) => StatusEventRaised?.Invoke(null, buildEventArgs);

        void IBuildEventArgsDispatchVisitor.Visit(BuildWarningEventArgs buildEventArgs) => WarningRaised?.Invoke(null, buildEventArgs);

        void IBuildEventArgsDispatchVisitor.Visit(BuildErrorEventArgs buildEventArgs) => ErrorRaised?.Invoke(null, buildEventArgs);
    }
}
