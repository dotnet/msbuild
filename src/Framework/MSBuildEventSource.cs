using System;
using System.IO;
using System.Diagnostics.Tracing;
using System.Globalization;


//
// This captures information of how various key methods of building with MSBuild ran.
//
namespace Microsoft.Build.Eventing
{
    // /OnlyProviders=*MSBuild-Profiling
    [EventSource(Name = "MSBuild-Profiling")]
    public sealed class MSBuildEventSource : EventSource
    {
        #region Singleton instance

        // define the singleton instance of the event source
        public static MSBuildEventSource Log = new MSBuildEventSource();

        private MSBuildEventSource() { }

        #endregion

        #region Events and NonEvents

        /// <summary>
        /// Call this method to notify listeners of information relevant to collecting a set of items, mutating them in a specified way, and saving the results.
        /// </summary>
        public void ApplyStart()
        {
            if (IsEnabled()) WriteEvent(1);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        public void ApplyStop()
        {
            if (IsEnabled()) WriteEvent(2);
        }

        /// <summary>
        /// Call this method to notify listeners of information relevant to the setup for a BuildManager to receive build requests.
        /// </summary>
        public void BuildStart()
        {
            if (IsEnabled()) WriteEvent(3);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        public void BuildStop()
        {
            if (IsEnabled()) WriteEvent(4);
        }

        /// <summary>
        /// Call this method to notify listeners of information of how a project file built.
        /// </summary>
        public void BuildProjectStart()
        {
            if (IsEnabled()) WriteEvent(5);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        public void BuildProjectStop()
        {
            if (IsEnabled()) WriteEvent(6);
        }

        /// <summary>
        /// Call this method to notify listeners of information relevant to resolving references.
        /// </summary>
        public void ComputeClosureStart()
        {
            if (IsEnabled()) WriteEvent(7);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        public void ComputeClosureStop()
        {
            if (IsEnabled()) WriteEvent(8);
        }

        /// <summary>
        /// Call this method to notify listeners of information relevant to resolving conditionals.
        /// </summary>
        /// <param name="condition">Relevant information about where in the run of the progam it is.</param>
        public void EvaluateConditionStart(string condition)
        {
            if (IsEnabled()) WriteEvent(9, condition);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="condition">Relevant information about where in the run of the progam it is.</param>
        public void EvaluateConditionStop(string condition)
        {
            if (IsEnabled()) WriteEvent(10, condition);
        }

        /// <summary>
        /// Call this method to notify listeners of how the project data was evaluated.
        /// </summary>
        /// <param name="file">Relevant information about where in the run of the progam it is.</param>
        public void EvaluateStart(string file)
        {
            if (IsEnabled())
            {
                string projectFile = String.IsNullOrEmpty(file) ? "(null)" : file;
                WriteEvent(11, String.Format(CultureInfo.CurrentCulture, "Evaluate Project {0} - Start", projectFile));
            }
        }

        /// <summary>
        /// Call this method to notify listeners of progress made on Evaluate.
        /// </summary>
        /// <param name="file">Relevant information about where in the run of the progam it is.</param>
        /// <param name="phaseNum">The stage in Evaluate that the program has reached.</param>
        public void EvaluatePhase(string file, long phaseNum)
        {
            if (IsEnabled())
            {
                string projectFile = String.IsNullOrEmpty(file) ? "(null)" : file;
                WriteEvent(12, String.Format(CultureInfo.CurrentCulture, "Evaluate Project {0}", projectFile), phaseNum);
            }
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="file">Relevant information about where in the run of the progam it is.</param>
        public void EvaluateStop(string file)
        {
            if (IsEnabled())
            {
                string projectFile = String.IsNullOrEmpty(file) ? "(null)" : file;
                WriteEvent(13, String.Format(CultureInfo.CurrentCulture, "Evaluate Project {0} - Stop", projectFile));
            }
        }

        /// <summary>
        /// Call this method to notify listeners of information relevant to the execution of the GenerateResource task.
        /// </summary>
        public void ExecuteGenerateResourceStart()
        {
            if (IsEnabled()) WriteEvent(14);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        public void ExecuteGenerateResourceStop()
        {
            if (IsEnabled()) WriteEvent(15);
        }

        /// <summary>
        /// Call this method to notify listeners of profiling related to executing a task.
        /// </summary>
        public void ExecuteTaskStart()
        {
            if (IsEnabled()) WriteEvent(16);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        public void ExecuteTaskStop()
        {
            if (IsEnabled()) WriteEvent(17);
        }

        /// <summary>
        /// Call this method to notify listeners of timing related to loading an XmlDocumentWithLocation from a path.
        /// </summary>
        public void LoadDocumentStart()
        {
            if (IsEnabled()) WriteEvent(18);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        public void LoadDocumentStop()
        {
            if (IsEnabled()) WriteEvent(19);
        }

        /// <summary>
        /// Call this method to notify listeners of the result of loggint the results from executing a task.
        /// </summary>
        public void LogResultsStart()
        {
            if (IsEnabled()) WriteEvent(20);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        public void LogResultsStop()
        {
            if (IsEnabled()) WriteEvent(21);
        }

        /// <summary>
        /// Call this method to notify listeners of profiling for the function that parses an XML document into a ProjectRootElement.
        /// </summary>
        /// <param name="projectFileName">Relevant information about where in the run of the progam it is.</param>
        public void ParseStart(string projectFileName)
        {

            if (IsEnabled())
            {
                WriteEvent(22, string.IsNullOrEmpty(projectFileName) ? "(null)" : projectFileName);
            }
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="projectFileName">Relevant information about where in the run of the progam it is.</param>
        public void ParseStop(string projectFileName)
        {
            if (IsEnabled())
            {
                WriteEvent(23, string.IsNullOrEmpty(projectFileName) ? "(null)" : projectFileName);
            }
        }

        /// <summary>
        /// Call this method to notify listeners of profiling for the method that removes blacklisted references from the reference table. It puts primary and dependency references in invalid file lists.
        /// </summary>
        public void RemoveReferencesMarkedForExclusionStart()
        {
            if (IsEnabled()) WriteEvent(24);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        public void RemoveReferencesMarkedForExclusionStop()
        {
            if (IsEnabled()) WriteEvent(25);
        }

        /// <summary>
        /// Call this method to provide timing analysis for requesting a new builder thread.
        /// </summary>
        public void RequestThreadProcStart()
        {
            if (IsEnabled()) WriteEvent(26);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        public void RequestThreadProcStop()
        {
            if (IsEnabled()) WriteEvent(27);
        }

        /// <summary>
        /// Call this method to capturing timing information for saving a project to the file system if dirty, creating directories as necessary.
        /// </summary>
        public void SaveStart()
        {
            if (IsEnabled()) WriteEvent(28);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        public void SaveStop()
        {
            if (IsEnabled()) WriteEvent(29);
        }

        /// <summary>
        /// Call this method to notify listeners of information relevant to identifying a list of files that correspond to an item with a wildcard.
        /// </summary>
        public void SelectItemsStart()
        {
            if (IsEnabled()) WriteEvent(30);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        public void SelectItemsStop()
        {
            if (IsEnabled()) WriteEvent(31);
        }

        /// <summary>
        /// Call this method to notify listeners of a target beginning execution.
        /// </summary>
        /// <param name="targetName"/>The name of the target being executed.</param>
        public void TargetStart(string targetName)
        {
            if (IsEnabled()) WriteEvent(32, targetName);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="targetName">The name of the target being executed.</param>
        public void TargetStop(string targetName)
        {
            if (IsEnabled()) WriteEvent(33, targetName);
        }

        /// <summary>
        /// Call this method to notify listeners of the start of a build as called from the command line.
        /// </summary>
        public void XMakeStart()
        {
            if (IsEnabled()) WriteEvent(34);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        public void XMakeStop()
        {
            if (IsEnabled()) WriteEvent(35);
        }

        #endregion
    }
}
