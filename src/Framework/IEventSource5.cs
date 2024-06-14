namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface defines the events raised by the build engine.
    /// Loggers use this interface to subscribe to the events they
    /// are interested in receiving.
    /// </summary>
    public interface IEventSource5 : IEventSource4
    {
        /// <summary>
        /// this event is raised to log the cancellation of a build
        /// </summary>
        event BuildCanceledEventHandler BuildCanceled;
    }
}
