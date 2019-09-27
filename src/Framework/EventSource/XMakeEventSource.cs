using System;
using System.IO;
using System.Diagnostics.Tracing;


//
// This captures information relevant to identifying a list of files that correspond to an item with a wildcard.
//
namespace Microsoft.Build.Eventing
{
    // /OnlyProviders=*XMake-Profiling
    [EventSource(Name = "XMake-Profiling")]
    public sealed class XMakeEventSource : EventSource
    {
        #region Singleton instance

        // define the singleton instance of the event source
        public static XMakeEventSource Log = new XMakeEventSource();

        private XMakeEventSource() { }

        #endregion

        #region Events and NonEvents

        /// <summary>
        /// Call this method to notify listeners of the start of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void XMakeStart(string info)
        {
            if (IsEnabled(EventLevel.Critical, Keywords.Project)) WriteEvent(1, info);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void XMakeStop(string info)
        {
            if (IsEnabled(EventLevel.Critical, Keywords.Project)) WriteEvent(2, info);
        }

        #endregion
    }
}
