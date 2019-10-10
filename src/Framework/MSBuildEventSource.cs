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
    [EventSource(Name = "Microsoft-Build")]
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
        [Event(12)]
        public void EvaluatePhase0Start(string projectFile)
        {
            WriteEvent(12, projectFile);
        }

        /// <summary>
        /// Call this method to notify listeners of progress made on Evaluate.
        /// </summary>
        /// <param name="projectFile">Relevant information about where in the run of the progam it is.</param>
        [Event(13)]
        public void EvaluatePhase0Stop(string projectFile)
        {
            WriteEvent(13, projectFile);
        }

        /// <summary>
        /// Call this method to notify listeners of progress made on Evaluate.
        /// </summary>
        /// <param name="projectFile">Relevant information about where in the run of the progam it is.</param>
        [Event(14)]
        public void EvaluatePhase1Start(string projectFile)
        {
            WriteEvent(14, projectFile);
        }

        /// <summary>
        /// Call this method to notify listeners of progress made on Evaluate.
        /// </summary>
        /// <param name="projectFile">Relevant information about where in the run of the progam it is.</param>
        [Event(15)]
        public void EvaluatePhase1Stop(string projectFile)
        {
            WriteEvent(15, projectFile);
        }

        /// <summary>
        /// Call this method to notify listeners of progress made on Evaluate.
        /// </summary>
        /// <param name="projectFile">Relevant information about where in the run of the progam it is.</param>
        [Event(16)]
        public void EvaluatePhase2Start(string projectFile)
        {
            WriteEvent(16, projectFile);
        }

        /// <summary>
        /// Call this method to notify listeners of progress made on Evaluate.
        /// </summary>
        /// <param name="projectFile">Relevant information about where in the run of the progam it is.</param>
        [Event(17)]
        public void EvaluatePhase2Stop(string projectFile)
        {
            WriteEvent(17, projectFile);
        }

        /// <summary>
        /// Call this method to notify listeners of progress made on Evaluate.
        /// </summary>
        /// <param name="projectFile">Relevant information about where in the run of the progam it is.</param>
        [Event(18)]
        public void EvaluatePhase3Start(string projectFile)
        {
            WriteEvent(18, projectFile);
        }

        /// <summary>
        /// Call this method to notify listeners of progress made on Evaluate.
        /// </summary>
        /// <param name="projectFile">Relevant information about where in the run of the progam it is.</param>
        [Event(19)]
        public void EvaluatePhase3Stop(string projectFile)
        {
            WriteEvent(19, projectFile);
        }

        /// <summary>
        /// Call this method to notify listeners of progress made on Evaluate.
        /// </summary>
        /// <param name="projectFile">Relevant information about where in the run of the progam it is.</param>
        [Event(20)]
        public void EvaluatePhase4Start(string projectFile)
        {
            WriteEvent(20, projectFile);
        }

        /// <summary>
        /// Call this method to notify listeners of progress made on Evaluate.
        /// </summary>
        /// <param name="projectFile">Relevant information about where in the run of the progam it is.</param>
        [Event(21)]
        public void EvaluatePhase4Stop(string projectFile)
        {
            WriteEvent(21, projectFile);
        }

        /// <summary>
        /// Call this method to notify listeners of progress made on Evaluate.
        /// </summary>
        /// <param name="projectFile">Relevant information about where in the run of the progam it is.</param>
        [Event(22)]
        public void EvaluatePhase5Start(string projectFile)
        {
            WriteEvent(22, projectFile);
        }

        /// <summary>
        /// Call this method to notify listeners of progress made on Evaluate.
        /// </summary>
        /// <param name="projectFile">Relevant information about where in the run of the progam it is.</param>
        [Event(23)]
        public void EvaluatePhase5Stop(string projectFile)
        {
            WriteEvent(23, projectFile);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="projectFile">Relevant information about where in the run of the progam it is.</param>
        [Event(24)]
        public void EvaluateStop(string projectFile)
        {
            WriteEvent(24, projectFile);
        }

        /// <summary>
        /// Call this method to notify listeners of information relevant to the execution of the GenerateResource task.
        /// </summary>
        [Event(25)]
        public void ExecuteGenerateResourceStart()
        {
            WriteEvent(25);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(26)]
        public void ExecuteGenerateResourceStop()
        {
            WriteEvent(26);
        }

        /// <summary>
        /// Call this method to notify listeners of profiling related to executing a task.
        /// </summary>
        [Event(27)]
        public void ExecuteTaskStart()
        {
            WriteEvent(27);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(28)]
        public void ExecuteTaskStop()
        {
            WriteEvent(28);
        }

        /// <summary>
        /// Call this method to notify listeners of timing related to loading an XmlDocumentWithLocation from a path.
        /// </summary>
        [Event(29)]
        public void LoadDocumentStart()
        {
            WriteEvent(29);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(30)]
        public void LoadDocumentStop()
        {
            WriteEvent(30);
        }

        /// <summary>
        /// Call this method to notify listeners of the result of loggint the results from executing a task.
        /// </summary>
        [Event(31)]
        public void LogResultsStart()
        {
            WriteEvent(31);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(32)]
        public void LogResultsStop()
        {
            WriteEvent(32);
        }

        /// <summary>
        /// Call this method to notify listeners of profiling for the function that parses an XML document into a ProjectRootElement.
        /// </summary>
        /// <param name="projectFileName">Relevant information about where in the run of the progam it is.</param>
        [Event(33)]
        public void ParseStart(string projectFileName)
        {

            WriteEvent(33, projectFileName);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="projectFileName">Relevant information about where in the run of the progam it is.</param>
        [Event(34)]
        public void ParseStop(string projectFileName)
        {
            WriteEvent(34, projectFileName);
        }

        /// <summary>
        /// Call this method to notify listeners of profiling for the method that removes blacklisted references from the reference table. It puts primary and dependency references in invalid file lists.
        /// </summary>
        [Event(35)]
        public void RemoveReferencesMarkedForExclusionStart()
        {
            WriteEvent(35);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(36)]
        public void RemoveReferencesMarkedForExclusionStop()
        {
            WriteEvent(36);
        }

        /// <summary>
        /// Call this method to provide timing analysis for requesting a new builder thread.
        /// </summary>
        [Event(37)]
        public void RequestThreadProcStart()
        {
            WriteEvent(37);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(38)]
        public void RequestThreadProcStop()
        {
            WriteEvent(38);
        }

        /// <summary>
        /// Call this method to capturing timing information for saving a project to the file system if dirty, creating directories as necessary.
        /// </summary>
        [Event(39)]
        public void SaveStart()
        {
            WriteEvent(39);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(40)]
        public void SaveStop()
        {
            WriteEvent(40);
        }

        /// <summary>
        /// Call this method to notify listeners of information relevant to identifying a list of files that correspond to an item with a wildcard.
        /// </summary>
        [Event(41)]
        public void SelectItemsStart()
        {
            WriteEvent(41);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(42)]
        public void SelectItemsStop()
        {
            WriteEvent(42);
        }

        /// <summary>
        /// Call this method to notify listeners of a target beginning execution.
        /// </summary>
        /// <param name="targetName"/>The name of the target being executed.</param>
        [Event(43)]
        public void TargetStart(string targetName)
        {
            WriteEvent(43, targetName);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        /// <param name="targetName">The name of the target being executed.</param>
        [Event(44)]
        public void TargetStop(string targetName)
        {
            WriteEvent(44, targetName);
        }

        /// <summary>
        /// Call this method to notify listeners of the start of a build as called from the command line.
        /// </summary>
        [Event(45)]
        public void XMakeStart()
        {
            WriteEvent(45);
        }

        /// <summary>
        /// Call this method to notify listeners of the end of the specified event.
        /// </summary>
        [Event(46)]
        public void XMakeStop()
        {
            WriteEvent(46);
        }

        #endregion
    }
}
