using System;
using System.IO;
using System.Diagnostics.Tracing;


//
// Profiling for the method that removes blacklisted references from the reference table. It puts primary and dependency references in invalid file lists.
//
namespace Microsoft.Build.Eventing
{
    // /OnlyProviders=*RemoveReferencesMarkedForExclusion-Profiling
    [EventSource(Name = "RemoveReferencesMarkedForExclusion-Profiling")]
    public sealed class RemoveReferencesMarkedForExclusionEventSource : EventSource
    {
        #region Singleton instance

        // define the singleton instance of the event source
        public static RemoveReferencesMarkedForExclusionEventSource Log = new RemoveReferencesMarkedForExclusionEventSource();

        private RemoveReferencesMarkedForExclusionEventSource() { }

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
