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
