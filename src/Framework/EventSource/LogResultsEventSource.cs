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
        private static int logNum = 1;

        private LogResultsEventSource() { }

        #endregion

        #region Events and NonEvents

        /// <summary>
        /// Call this method to notify listeners of the start of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void LogResultsStart(string info)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Project)) WriteEvent(logNum++, info);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void LogResultsStop(string info)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Project)) WriteEvent(logNum++, info);
        }

        #endregion
    }
}
