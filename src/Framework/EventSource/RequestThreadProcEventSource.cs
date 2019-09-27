using System;
using System.IO;
using System.Diagnostics.Tracing;


//
// Class to provide timing analysis for requesting a new builder thread.
//
namespace Microsoft.Build.Eventing
{
    // /OnlyProviders=*RequestThreadProc-Profiling
    [EventSource(Name = "RequestThreadProc-Profiling")]
    public sealed class RequestThreadProcEventSource : EventSource
    {
        #region Singleton instance

        // define the singleton instance of the event source
        public static RequestThreadProcEventSource Log = new RequestThreadProcEventSource();

        private RequestThreadProcEventSource() { }

        #endregion

        #region Events and NonEvents

        /// <summary>
        /// Call this method to notify listeners of the start of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void RequestThreadProcStart(string info)
        {
            if (IsEnabled(EventLevel.Error, Keywords.Project)) WriteEvent(1, info);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void RequestThreadProcStop(string info)
        {
            if (IsEnabled(EventLevel.Error, Keywords.Project)) WriteEvent(2, info);
        }

        #endregion
    }
}
