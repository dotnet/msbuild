using System;
using System.IO;
using System.Diagnostics.Tracing;


//
// Timing analysis for saving a project to the file system if dirty, creating directories as necessary.
//
namespace Microsoft.Build.Eventing
{
    // /OnlyProviders=*Save-Profiling
    [EventSource(Name = "Save-Profiling")]
    public sealed class SaveEventSource : EventSource
    {
        #region Singleton instance

        // define the singleton instance of the event source
        public static SaveEventSource Log = new SaveEventSource();

        private SaveEventSource() { }

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
