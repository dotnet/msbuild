using System;
using System.IO;
using System.Diagnostics.Tracing;
using System.Globalization;


//
// Profiling for the function that parses an XML document into a ProjectRootElement.
//
namespace Microsoft.Build.Eventing
{
    // /OnlyProviders=*Parse-Profiling
    [EventSource(Name = "Parse-Profiling")]
    public sealed class ParseEventSource : EventSource
    {
        #region Singleton instance

        // define the singleton instance of the event source
        public static ParseEventSource Log = new ParseEventSource();
        private static int logNum = 1;

        private ParseEventSource() { }

        #endregion

        #region Events and NonEvents

        /// <summary>
        /// Call this method to notify listeners of the start of the specified event.
        /// </summary>
        /// <param name="projectFileName">Relevant information about where in the run of the progam it is.</param>
        public void ParseStart(string projectFileName)
        {

            if (IsEnabled(EventLevel.Error, Keywords.Project))
            {
                string name = String.IsNullOrEmpty(projectFileName) ? "(null)" : projectFileName;
                WriteEvent(logNum++, String.Format(CultureInfo.CurrentCulture, "Parse Project {0} - Start", name));
            }
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="projectFileName">Relevant information about where in the run of the progam it is.</param>
        public void ParseStop(string projectFileName)
        {
            if (IsEnabled(EventLevel.Error, Keywords.Project))
            {
                string name = String.IsNullOrEmpty(projectFileName) ? "(null)" : projectFileName;
                WriteEvent(logNum++, String.Format(CultureInfo.CurrentCulture, "Parse Project {0} - Stop", name));
            }
        }

        #endregion
    }
}
