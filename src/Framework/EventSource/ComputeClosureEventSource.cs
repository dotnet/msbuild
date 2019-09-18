using System;
using System.IO;
using System.Diagnostics.Tracing;


//
// This captures information relevant to resolving references.
//
namespace Microsoft.Build.Eventing
{
    // /OnlyProviders=*ComputeClosure-Profiling
    [EventSource(Name = "ComputeClosure-Profiling")]
    public sealed class ComputeClosureEventSource : EventSource
    {
        #region Singleton instance

        // define the singleton instance of the event source
        public static ComputeClosureEventSource Log = new ComputeClosureEventSource();
        private static int logNum = 1;

        private ComputeClosureEventSource() { }

        #endregion

        #region Events and NonEvents

        /// <summary>
        /// Call this method to notify listeners of the start of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void ComputeClosureStart(string info)
        {
            if (IsEnabled(EventLevel.Error, Keywords.Project)) WriteEvent(logNum++, info);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void ComputeClosureStop(string info)
        {
            if (IsEnabled(EventLevel.Error, Keywords.Project)) WriteEvent(logNum++, info);
        }

        #endregion
    }
}
