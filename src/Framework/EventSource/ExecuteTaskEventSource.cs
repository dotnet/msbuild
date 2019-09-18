using System;
using System.IO;
using System.Diagnostics.Tracing;


//
// Executes a task.
//
namespace Microsoft.Build.Eventing
{
    // /OnlyProviders=*ExecuteTask-Profiling
    [EventSource(Name = "ExecuteTask-Profiling")]
    public sealed class ExecuteTaskEventSource : EventSource
    {
        #region Singleton instance

        // define the singleton instance of the event source
        public static ExecuteTaskEventSource Log = new ExecuteTaskEventSource();
        private static int logNum = 1;

        private ExecuteTaskEventSource() { }

        #endregion

        #region Events and NonEvents

        /// <summary>
        /// Call this method to notify listeners of the start of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void ExecuteTaskStart(string info)
        {
            if (IsEnabled(EventLevel.Error, Keywords.Task)) WriteEvent(logNum++, info);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void ExecuteTaskStop(string info)
        {
            if (IsEnabled(EventLevel.Error, Keywords.Task)) WriteEvent(logNum++, info);
        }

        #endregion
    }
}
