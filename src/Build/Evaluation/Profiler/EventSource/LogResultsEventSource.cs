using System;
using System.IO;
using System.Diagnostics.Tracing;


//
// Profiles logging of results from executing a task.
//
namespace Microsoft.Build.Evaluation
{
    // Give your event sources a descriptive name using the EventSourceAttribute, otherwise the name of the class is used. 
    [EventSource(Name = "LogResults-Profiling")]
    public sealed class LogResultsEventSource : EventSource
    {
        #region Singleton instance

        // define the singleton instance of the event source
        public static LogResultsEventSource Log = new LogResultsEventSource();

        #endregion

        #region Events and NonEvents

        /// <summary>
        /// Call this method to notify listeners of a Load event for image 'imageName'
        /// </summary>
        /// <param name="baseAddress">The base address where the image was loaded</param>
        /// <param name="imageName">The image name</param>
        public void Load(string info)
        {
            WriteEvent(1, info);
        }

        #endregion
    }
}
