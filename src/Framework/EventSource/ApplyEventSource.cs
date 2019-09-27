using System;
using System.IO;
using System.Diagnostics.Tracing;


//
// This captures information relevant to collecting a set of items, mutating them in a specified way, and saving the results.
//
namespace Microsoft.Build.Eventing
{
    // /OnlyProviders=*Apply-Profiling
    [EventSource(Name = "Apply-Profiling")]
    public sealed class ApplyEventSource : EventSource
    {
        #region Singleton instance

        // define the singleton instance of the event source
        public static ApplyEventSource Log = new ApplyEventSource();

        private ApplyEventSource() { }

        #endregion

        #region Events and NonEvents

        /// <summary>
        /// Call this method to notify listeners of the start of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void ApplyStart(string info)
        {
            if (IsEnabled(EventLevel.Informational, Keywords.Item)) WriteEvent(1, info);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void ApplyStop(string info)
        {
            if (IsEnabled(EventLevel.Informational, Keywords.Item)) WriteEvent(2, info);
        }

        #endregion
    }
}
