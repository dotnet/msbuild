using System;
using System.IO;
using System.Diagnostics.Tracing;


//
// Profiler for loading a XmlDocumentWithLocation document from a path.
//
namespace Microsoft.Build.Eventing
{
    // /OnlyProviders=*LoadDocument-Profiling
    [EventSource(Name = "LoadDocument-Profiling")]
    public sealed class LoadDocumentEventSource : EventSource
    {
        #region Singleton instance

        // define the singleton instance of the event source
        public static LoadDocumentEventSource Log = new LoadDocumentEventSource();

        private LoadDocumentEventSource() { }

        #endregion

        #region Events and NonEvents

        /// <summary>
        /// Call this method to notify listeners of the start of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void LoadDocumentStart(string info)
        {
            if (IsEnabled(EventLevel.Informational, Keywords.Item)) WriteEvent(1, info);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void LoadDocumentStop(string info)
        {
            if (IsEnabled(EventLevel.Informational, Keywords.Item)) WriteEvent(2, info);
        }

        #endregion
    }
}
