using System;
using System.IO;
using System.Diagnostics.Tracing;
using System.Globalization;


//
// Evaluates the project data passed in. This has multiple "endpoints" (that it continues through).
//
namespace Microsoft.Build.Eventing
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
        /// Call this method to notify listeners of the start of the specified event.
        /// </summary>
        /// <param name="file">Relevant information about where in the run of the progam it is.</param>
        public void EvaluateStart(string file)
        {
            if (IsEnabled(EventLevel.Informational, Keywords.Project))
            {
                string projectFile = String.IsNullOrEmpty(file) ? "(null)" : file;
                WriteEvent(1, String.Format(CultureInfo.CurrentCulture, "Evaluate Project {0} - Start", projectFile));
            }
        }

        /// <summary>
        /// Call this method to notify listeners of progress made on Evaluate.
        /// </summary>
        /// <param name="file">Relevant information about where in the run of the progam it is.</param>
        /// <param name="phaseNum">The stage in Evaluate that the program has reached.</param>
        public void EvaluatePhase(string file, long phaseNum)
        {
            if (IsEnabled(EventLevel.Informational, Keywords.Project))
            {
                string projectFile = String.IsNullOrEmpty(file) ? "(null)" : file;
                WriteEvent(2, String.Format(CultureInfo.CurrentCulture, "Evaluate Project {0}", projectFile), phaseNum);
            }
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="file">Relevant information about where in the run of the progam it is.</param>
        public void EvaluateStop(string file)
        {
            if (IsEnabled(EventLevel.Informational, Keywords.Project))
            {
                string projectFile = String.IsNullOrEmpty(file) ? "(null)" : file;
                WriteEvent(3, String.Format(CultureInfo.CurrentCulture, "Evaluate Project {0} - Stop", projectFile));
            }
        }

        #endregion
    }
}
