using System;
using System.IO;
using System.Diagnostics.Tracing;


//
// This captures information relevant to resolving conditionals.
//
namespace Microsoft.Build.Eventing
{
    // /OnlyProviders=*EvaluateCondition-Profiling
    [EventSource(Name = "EvaluateCondition-Profiling")]
    public sealed class EvaluateConditionEventSource : EventSource
    {
        #region Singleton instance

        // define the singleton instance of the event source
        public static EvaluateConditionEventSource Log = new EvaluateConditionEventSource();
        private static int logNum = 1;
        private EvaluateConditionEventSource() { }

        #endregion

        #region Events and NonEvents

        /// <summary>
        /// Call this method to notify listeners of the start of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void EvaluateConditionStart(string info)
        {
            if (IsEnabled(EventLevel.Error, Keywords.Project)) WriteEvent(logNum++, info);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="info">Relevant information about where in the run of the progam it is.</param>
        public void EvaluateConditionStop(string info)
        {
            if (IsEnabled(EventLevel.Error, Keywords.Project)) WriteEvent(logNum++, info);
        }

        #endregion
    }
}
