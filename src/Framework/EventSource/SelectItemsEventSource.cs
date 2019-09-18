using System;
using System.IO;
using System.Diagnostics.Tracing;


//
// This captures information relevant to identifying a list of files that correspond to an item with a wildcard.
//
namespace Microsoft.Build.Eventing
{
    // /OnlyProviders=*SelectItems-Profiling
    [EventSource(Name = "SelectItems-Profiling")]
    public sealed class SelectItemsEventSource : EventSource
    {
        #region Singleton instance

        // define the singleton instance of the event source
        public static SelectItemsEventSource Log = new SelectItemsEventSource();
        private static int logNum = 1;

        private SelectItemsEventSource() { }

        #endregion

        #region Events and NonEvents

        /// <summary>
        /// Call this method to notify listeners of the start of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void SelectItemsStart(string info)
        {
            if (IsEnabled(EventLevel.Informational, Keywords.Item)) WriteEvent(logNum++, info);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void SelectItemsStop(string info)
        {
            if (IsEnabled(EventLevel.Informational, Keywords.Item)) WriteEvent(logNum++, info);
        }

        #endregion
    }
}
