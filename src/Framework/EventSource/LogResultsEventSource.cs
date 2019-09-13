using System;
using System.IO;
using System.Diagnostics.Tracing;


//
// Profiles logging of results from executing a task.
//
namespace Microsoft.Build.Eventing
{
    // /OnlyProviders=*LogResults-Profiling
    [EventSource(Name = "LogResults-Profiling")]
    public sealed class LogResultsEventSource : EventSource
    {
        #region Singleton instance

        // define the singleton instance of the event source
        public static LogResultsEventSource Log = new LogResultsEventSource();

        private LogResultsEventSource() { }

        #endregion

        #region Events and NonEvents

        /// <summary>
        /// Call this method to notify listeners of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void Load(string info)
        {
            WriteEvent(1, info);
        }

        #endregion
    }
}
