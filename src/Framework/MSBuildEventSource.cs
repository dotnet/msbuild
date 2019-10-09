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
    [EventSource(Name = "Microsoft-Build-Profiling")]
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
        [Event(1)]
        public void ApplyStart()
        {
            WriteEvent(1);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(2)]
        public void ApplyStop()
        {
            WriteEvent(2);
        }

        /// <summary>
        /// Call this method to notify listeners of information relevant to the setup for a BuildManager to receive build requests.
        /// </summary>
        [Event(3)]
        public void BuildStart()
        {
            WriteEvent(3);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(4)]
        public void BuildStop()
        {
            WriteEvent(4);
        }

        /// <summary>
        /// Call this method to notify listeners of information of how a project file built.
        /// </summary>
        [Event(5)]
        public void BuildProjectStart()
        {
            WriteEvent(5);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(6)]
        public void BuildProjectStop()
        {
            WriteEvent(6);
        }

        /// <summary>
        /// Call this method to notify listeners of information relevant to resolving references.
        /// </summary>
        [Event(7)]
        public void ComputeClosureStart()
        {
            WriteEvent(7);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(8)]
        public void ComputeClosureStop()
        {
            WriteEvent(8);
        }

        /// <summary>
        /// Call this method to notify listeners of information relevant to resolving conditionals.
        /// </summary>
        /// <param name="condition">Relevant information about where in the run of the progam it is.</param>
        [Event(9)]
        public void EvaluateConditionStart(string condition)
        {
            WriteEvent(9, condition);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="condition">Relevant information about where in the run of the progam it is.</param>
        [Event(10)]
        public void EvaluateConditionStop(string condition)
        {
            WriteEvent(10, condition);
        }

        /// <summary>
        /// Call this method to notify listeners of how the project data was evaluated.
        /// </summary>
        /// <param name="projectFile">Relevant information about where in the run of the progam it is.</param>
        [Event(11)]
        public void EvaluateStart(string projectFile)
        {
            WriteEvent(11, projectFile);
        }

        /// <summary>
        /// Call this method to notify listeners of progress made on Evaluate.
        /// </summary>
        /// <param name="projectFile">Relevant information about where in the run of the progam it is.</param>
        /// <param name="phaseNum">The stage in Evaluate that the program has reached.</param>
        [Event(12)]
        public void EvaluatePhase(string projectFile, long phaseNum)
        {
            WriteEvent(12, projectFile, phaseNum);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="projectFile">Relevant information about where in the run of the progam it is.</param>
        [Event(13)]
        public void EvaluateStop(string projectFile)
        {
            WriteEvent(13, projectFile);
        }

        /// <summary>
        /// Call this method to notify listeners of information relevant to the execution of the GenerateResource task.
        /// </summary>
        [Event(14)]
        public void ExecuteGenerateResourceStart()
        {
            WriteEvent(14);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(15)]
        public void ExecuteGenerateResourceStop()
        {
            WriteEvent(15);
        }

        /// <summary>
        /// Call this method to notify listeners of profiling related to executing a task.
        /// </summary>
        [Event(16)]
        public void ExecuteTaskStart()
        {
            WriteEvent(16);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(17)]
        public void ExecuteTaskStop()
        {
            WriteEvent(17);
        }

        /// <summary>
        /// Call this method to notify listeners of timing related to loading an XmlDocumentWithLocation from a path.
        /// </summary>
        [Event(18)]
        public void LoadDocumentStart()
        {
            WriteEvent(18);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(19)]
        public void LoadDocumentStop()
        {
            WriteEvent(19);
        }

        /// <summary>
        /// Call this method to notify listeners of the result of loggint the results from executing a task.
        /// </summary>
        [Event(20)]
        public void LogResultsStart()
        {
            WriteEvent(20);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(21)]
        public void LogResultsStop()
        {
            WriteEvent(21);
        }

        /// <summary>
        /// Call this method to notify listeners of profiling for the function that parses an XML document into a ProjectRootElement.
        /// </summary>
        /// <param name="projectFileName">Relevant information about where in the run of the progam it is.</param>
        [Event(22)]
        public void ParseStart(string projectFileName)
        {

            WriteEvent(22, projectFileName);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="projectFileName">Relevant information about where in the run of the progam it is.</param>
        [Event(23)]
        public void ParseStop(string projectFileName)
        {
            WriteEvent(23, projectFileName);
        }

        /// <summary>
        /// Call this method to notify listeners of profiling for the method that removes blacklisted references from the reference table. It puts primary and dependency references in invalid file lists.
        /// </summary>
        [Event(24)]
        public void RemoveReferencesMarkedForExclusionStart()
        {
            WriteEvent(24);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(25)]
        public void RemoveReferencesMarkedForExclusionStop()
        {
            WriteEvent(25);
        }

        /// <summary>
        /// Call this method to provide timing analysis for requesting a new builder thread.
        /// </summary>
        [Event(26)]
        public void RequestThreadProcStart()
        {
            WriteEvent(26);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(27)]
        public void RequestThreadProcStop()
        {
            WriteEvent(27);
        }

        /// <summary>
        /// Call this method to capturing timing information for saving a project to the file system if dirty, creating directories as necessary.
        /// </summary>
        [Event(28)]
        public void SaveStart()
        {
            WriteEvent(28);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(29)]
        public void SaveStop()
        {
            WriteEvent(29);
        }

        /// <summary>
        /// Call this method to notify listeners of information relevant to identifying a list of files that correspond to an item with a wildcard.
        /// </summary>
        [Event(30)]
        public void SelectItemsStart()
        {
            WriteEvent(30);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(31)]
        public void SelectItemsStop()
        {
            WriteEvent(31);
        }

        /// <summary>
        /// Call this method to notify listeners of a target beginning execution.
        /// </summary>
        /// <param name="targetName"/>The name of the target being executed.</param>
        [Event(32)]
        public void TargetStart(string targetName)
        {
            WriteEvent(32, targetName);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="targetName">The name of the target being executed.</param>
        [Event(33)]
        public void TargetStop(string targetName)
        {
            WriteEvent(33, targetName);
        }

        /// <summary>
        /// Call this method to notify listeners of the start of a build as called from the command line.
        /// </summary>
        [Event(34)]
        public void XMakeStart()
        {
            WriteEvent(34);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(35)]
        public void XMakeStop()
        {
            WriteEvent(35);
        }

        #endregion
    }
}
