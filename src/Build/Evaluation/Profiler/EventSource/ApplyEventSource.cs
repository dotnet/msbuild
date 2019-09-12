using System;
using System.IO;
using System.Diagnostics.Tracing;


//
// This captures information relevant to collecting a set of items, mutating them in a specified way, and saving the results.
//
namespace Microsoft.Build.Evaluation
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
