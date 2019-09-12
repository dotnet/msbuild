using System;
using System.IO;
using System.Diagnostics.Tracing;


//
// Executes a task.
//
namespace Microsoft.Build.Evaluation
{
    // /OnlyProviders=*ExecuteTask-Profiling
    [EventSource(Name = "ExecuteTask-Profiling")]
    public sealed class ExecuteTaskEventSource : EventSource
    {
        #region Singleton instance

        // define the singleton instance of the event source
        public static ExecuteTaskEventSource Log = new ExecuteTaskEventSource();

        private ExecuteTaskEventSource() { }

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
