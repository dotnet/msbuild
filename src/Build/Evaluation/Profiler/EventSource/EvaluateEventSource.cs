using System;
using System.IO;
using System.Diagnostics.Tracing;


//
// Evaluates the project data passed in. This has multiple "endpoints" (that it continues through).
//
namespace Microsoft.Build.Evaluation
{
    // /OnlyProviders=*Evaluate-Profiling
    [EventSource(Name = "Evaluate-Profiling")]
    public sealed class EvaluateEventSource : EventSource
    {
        #region Singleton instance

        // define the singleton instance of the event source
        public static EvaluateEventSource Log = new EvaluateEventSource();

        private EvaluateEventSource() { }

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
