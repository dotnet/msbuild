using System;
using System.IO;
using System.Diagnostics.Tracing;


//
// Main entrypoint for the GenerateResource task.
//
namespace Microsoft.Build.Eventing
{
    // /OnlyProvider=*ExecuteGenerateResource-Profiling
    [EventSource(Name = "ExecuteGeneralResource-Profiling")]
    public sealed class ExecuteGenerateResourceEventSource : EventSource
    {
        #region Singleton instance

        // define the singleton instance of the event source
        public static ExecuteGenerateResourceEventSource Log = new ExecuteGenerateResourceEventSource();

        private ExecuteGenerateResourceEventSource() { }

        #endregion

        #region Events and NonEvents

        /// <summary>
        /// Call this method to notify listeners of the start of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void ExecuteGeneralResourceStart(string info)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Task)) WriteEvent(1, info);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void ExecuteGeneralResourceStop(string info)
        {
            if (IsEnabled(EventLevel.Verbose, Keywords.Task)) WriteEvent(2, info);
        }

        #endregion
    }
}
