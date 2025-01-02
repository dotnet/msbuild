// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Tracing;

namespace Microsoft.Build.Eventing
{
    /// <summary>
    /// This captures information of how various key methods of building with MSBuild ran.
    /// </summary>
    /// <remarks>
    /// Changes to existing event method signatures will not be reflected unless you update the <see cref="EventAttribute.Version" /> property or assign a new event ID.
    /// </remarks>
    [EventSource(Name = "Microsoft-Build")]
    internal sealed class MSBuildEventSource : EventSource
    {
        public static class Keywords
        {
            /// <summary>
            /// Keyword applied to all MSBuild events.
            /// </summary>
            /// <remarks>
            /// Literally every event should define this.
            /// </remarks>
            public const EventKeywords All = (EventKeywords)0x1;

            /// <summary>
            /// Keyword for events that should go in the text performance log when turned on.
            /// </summary>
            /// <remarks>
            /// This keyword should be applied only to events that are low-volume
            /// and likely to be useful to diagnose perf issues using the
            /// <see href="https://github.com/dotnet/msbuild/pull/5861">text perf log</see>.
            /// </remarks>
            public const EventKeywords PerformanceLog = (EventKeywords)0x2;
        }

        /// <summary>
        /// define the singleton instance of the event source
        /// </summary>
        public static MSBuildEventSource Log = new MSBuildEventSource();

        private MSBuildEventSource() { }

        #region Events

        /// <summary>
        /// Call this method to notify listeners of information relevant to collecting a set of items, mutating them in a specified way, and saving the results.
        /// </summary>
        /// <param name="itemType">The type of the item being mutated.</param>
        [Event(1, Keywords = Keywords.All)]
        public void ApplyLazyItemOperationsStart(string itemType)
        {
            WriteEvent(1, itemType);
        }

        /// <param name="itemType">The type of the item being mutated.</param>
        [Event(2, Keywords = Keywords.All)]
        public void ApplyLazyItemOperationsStop(string itemType)
        {
            WriteEvent(2, itemType);
        }

        /// <summary>
        /// Call this method to notify listeners of information relevant to the setup for a BuildManager to receive build requests.
        /// </summary>
        [Event(3, Keywords = Keywords.All | Keywords.PerformanceLog)]
        public void BuildStart()
        {
            WriteEvent(3);
        }

        [Event(4, Keywords = Keywords.All | Keywords.PerformanceLog)]
        public void BuildStop()
        {
            WriteEvent(4);
        }

        /// <summary>
        /// Call this method to notify listeners of information of how a project file built.
        /// <param name="projectPath">Filename of the project being built.</param>
        /// </summary>
        [Event(5, Keywords = Keywords.All | Keywords.PerformanceLog)]
        public void BuildProjectStart(string projectPath)
        {
            WriteEvent(5, projectPath);
        }

        /// <param name="projectPath">Filename of the project being built.</param>
        /// <param name="targets">Names of the targets that built.</param>
        [Event(6, Keywords = Keywords.All | Keywords.PerformanceLog)]
        public void BuildProjectStop(string projectPath, string targets)
        {
            WriteEvent(6, projectPath, targets);
        }

        [Event(7, Keywords = Keywords.All)]
        public void RarComputeClosureStart()
        {
            WriteEvent(7);
        }

        [Event(8, Keywords = Keywords.All)]
        public void RarComputeClosureStop()
        {
            WriteEvent(8);
        }

        /// <param name="condition">The condition being evaluated.</param>
        [Event(9, Keywords = Keywords.All)]
        public void EvaluateConditionStart(string condition)
        {
            WriteEvent(9, condition);
        }

        /// <param name="condition">The condition being evaluated.</param>
        /// <param name="result">The result of evaluating the condition.</param>
        [Event(10, Keywords = Keywords.All)]
        public void EvaluateConditionStop(string condition, bool result)
        {
            WriteEvent(10, condition, result);
        }

        /// <summary>
        /// Call this method to notify listeners of how the project data was evaluated.
        /// </summary>
        /// <param name="projectFile">Filename of the project being evaluated.</param>
        [Event(11, Keywords = Keywords.All | Keywords.PerformanceLog)]
        public void EvaluateStart(string projectFile)
        {
            WriteEvent(11, projectFile);
        }

        /// <param name="projectFile">Filename of the project being evaluated.</param>
        [Event(12, Keywords = Keywords.All | Keywords.PerformanceLog)]
        public void EvaluateStop(string projectFile)
        {
            WriteEvent(12, projectFile);
        }

        /// <param name="projectFile">Filename of the project being evaluated.</param>
        [Event(13, Keywords = Keywords.All)]
        public void EvaluatePass0Start(string projectFile)
        {
            WriteEvent(13, projectFile);
        }

        /// <param name="projectFile">Filename of the project being evaluated.</param>
        [Event(14, Keywords = Keywords.All)]
        public void EvaluatePass0Stop(string projectFile)
        {
            WriteEvent(14, projectFile);
        }

        /// <param name="projectFile">Filename of the project being evaluated.</param>
        [Event(15, Keywords = Keywords.All)]
        public void EvaluatePass1Start(string projectFile)
        {
            WriteEvent(15, projectFile);
        }

        /// <param name="projectFile">Filename of the project being evaluated.</param>
        [Event(16, Keywords = Keywords.All)]
        public void EvaluatePass1Stop(string projectFile)
        {
            WriteEvent(16, projectFile);
        }

        /// <param name="projectFile">Filename of the project being evaluated.</param>
        [Event(17, Keywords = Keywords.All)]
        public void EvaluatePass2Start(string projectFile)
        {
            WriteEvent(17, projectFile);
        }

        /// <param name="projectFile">Filename of the project being evaluated.</param>
        [Event(18, Keywords = Keywords.All)]
        public void EvaluatePass2Stop(string projectFile)
        {
            WriteEvent(18, projectFile);
        }

        /// <param name="projectFile">Filename of the project being evaluated.</param>
        [Event(19, Keywords = Keywords.All)]
        public void EvaluatePass3Start(string projectFile)
        {
            WriteEvent(19, projectFile);
        }

        /// <param name="projectFile">Filename of the project being evaluated.</param>
        [Event(20, Keywords = Keywords.All)]
        public void EvaluatePass3Stop(string projectFile)
        {
            WriteEvent(20, projectFile);
        }

        /// <param name="projectFile">Filename of the project being evaluated.</param>
        [Event(21, Keywords = Keywords.All)]
        public void EvaluatePass4Start(string projectFile)
        {
            WriteEvent(21, projectFile);
        }

        /// <param name="projectFile">Filename of the project being evaluated.</param>
        [Event(22, Keywords = Keywords.All)]
        public void EvaluatePass4Stop(string projectFile)
        {
            WriteEvent(22, projectFile);
        }

        /// <param name="projectFile">Filename of the project being evaluated.</param>
        [Event(23, Keywords = Keywords.All)]
        public void EvaluatePass5Start(string projectFile)
        {
            WriteEvent(23, projectFile);
        }

        /// <param name="projectFile">Filename of the project being evaluated.</param>
        [Event(24, Keywords = Keywords.All)]
        public void EvaluatePass5Stop(string projectFile)
        {
            WriteEvent(24, projectFile);
        }

        [Event(25, Keywords = Keywords.All)]
        public void GenerateResourceOverallStart()
        {
            WriteEvent(25);
        }

        [Event(26, Keywords = Keywords.All)]
        public void GenerateResourceOverallStop()
        {
            WriteEvent(26);
        }

        [Event(27, Keywords = Keywords.All | Keywords.PerformanceLog)]
        public void RarOverallStart()
        {
            WriteEvent(27);
        }

        [Event(28, Keywords = Keywords.All | Keywords.PerformanceLog, Version = 1)]
        public void RarOverallStop(int assembliesCount, int assemblyFilesCount, int resolvedFilesCount, int resolvedDependencyFilesCount, int copyLocalFilesCount, bool findDependencies)
        {
            WriteEvent(28, assembliesCount, assemblyFilesCount, resolvedFilesCount, resolvedDependencyFilesCount, copyLocalFilesCount, findDependencies);
        }

        /// <summary>
        /// Call this method to notify listeners of information relevant to identifying a list of files that correspond to an item with a wildcard.
        /// </summary>
        /// <param name="rootDirectory">Source of files to glob.</param>
        /// <param name="glob">Pattern, possibly with wildcard(s) to be expanded.</param>
        /// <param name="excludedPatterns">Patterns not to expand.</param>
        [Event(41, Keywords = Keywords.All)]
        public void ExpandGlobStart(string rootDirectory, string glob, string excludedPatterns)
        {
            WriteEvent(41, rootDirectory, glob, excludedPatterns);
        }

        /// <param name="rootDirectory">Source of files to glob.</param>
        /// <param name="glob">Pattern, possibly with wildcard(s) to be expanded.</param>
        /// <param name="excludedPatterns">Patterns not to expand.</param>
        [Event(42, Keywords = Keywords.All)]
        public void ExpandGlobStop(string rootDirectory, string glob, string excludedPatterns)
        {
            WriteEvent(42, rootDirectory, glob, excludedPatterns);
        }

        /// <summary>
        /// Call this method to notify listeners of timing related to loading an XmlDocumentWithLocation from a path.
        /// <param name="fullPath">Path to the document to load.</param>
        /// </summary>
        [Event(29, Keywords = Keywords.All | Keywords.PerformanceLog)]
        public void LoadDocumentStart(string fullPath)
        {
            WriteEvent(29, fullPath);
        }

        /// <param name="fullPath">Path to the document to load.</param>
        [Event(30, Keywords = Keywords.All | Keywords.PerformanceLog)]
        public void LoadDocumentStop(string fullPath)
        {
            WriteEvent(30, fullPath);
        }

        [Event(31, Keywords = Keywords.All)]
        public void RarLogResultsStart()
        {
            WriteEvent(31);
        }

        [Event(32, Keywords = Keywords.All)]
        public void RarLogResultsStop()
        {
            WriteEvent(32);
        }

        /// <summary>
        /// Call this method to notify listeners of profiling for the function that parses an XML document into a ProjectRootElement.
        /// </summary>
        /// <param name="projectFileName">Filename of the project being evaluated.</param>
        [Event(33, Keywords = Keywords.All)]
        public void ParseStart(string projectFileName)
        {
            WriteEvent(33, projectFileName);
        }

        /// <param name="projectFileName">Filename of the project being evaluated.</param>
        [Event(34, Keywords = Keywords.All)]
        public void ParseStop(string projectFileName)
        {
            WriteEvent(34, projectFileName);
        }

        /// <summary>
        /// Call this method to notify listeners of profiling for the method that removes denylisted references from the reference table. It puts primary and dependency references in invalid file lists.
        /// </summary>
        [Event(35, Keywords = Keywords.All)]
        public void RarRemoveReferencesMarkedForExclusionStart()
        {
            WriteEvent(35);
        }

        [Event(36, Keywords = Keywords.All)]
        public void RarRemoveReferencesMarkedForExclusionStop()
        {
            WriteEvent(36);
        }

        [Event(37, Keywords = Keywords.All | Keywords.PerformanceLog)]
        public void RequestThreadProcStart()
        {
            WriteEvent(37);
        }

        [Event(38, Keywords = Keywords.All | Keywords.PerformanceLog)]
        public void RequestThreadProcStop()
        {
            WriteEvent(38);
        }

        /// <param name="fileLocation">Project file's location.</param>
        [Event(39, Keywords = Keywords.All)]
        public void SaveStart(string fileLocation)
        {
            WriteEvent(39, fileLocation);
        }

        /// <param name="fileLocation">Project file's location.</param>
        [Event(40, Keywords = Keywords.All)]
        public void SaveStop(string fileLocation)
        {
            WriteEvent(40, fileLocation);
        }

        /// <param name="targetName">The name of the target being executed.</param>
        [Event(43, Keywords = Keywords.All | Keywords.PerformanceLog)]
        public void TargetStart(string targetName)
        {
            WriteEvent(43, targetName);
        }

        /// <param name="targetName">The name of the target being executed.</param>
        /// <param name="result">Target stop result.</param>
        [Event(44, Keywords = Keywords.All | Keywords.PerformanceLog, Version = 1)]
        public void TargetStop(string targetName, string result)
        {
            WriteEvent(44, targetName, result);
        }

        /// <summary>
        /// Call this method to notify listeners of the start of a build as called from the command line.
        /// </summary>
        /// <param name="commandLine">The command line used to run MSBuild.</param>
        [Event(45, Keywords = Keywords.All | Keywords.PerformanceLog)]
        public void MSBuildExeStart(string commandLine)
        {
            WriteEvent(45, commandLine);
        }

        /// <param name="commandLine">The command line used to run MSBuild.</param>
        [Event(46, Keywords = Keywords.All | Keywords.PerformanceLog)]
        public void MSBuildExeStop(string commandLine)
        {
            WriteEvent(46, commandLine);
        }

        [Event(47, Keywords = Keywords.All)]
        public void ExecuteTaskStart(string taskName, int taskID)
        {
            WriteEvent(47, taskName, taskID);
        }

        [Event(48, Keywords = Keywords.All)]
        public void ExecuteTaskStop(string taskName, int taskID)
        {
            WriteEvent(48, taskName, taskID);
        }

        [Event(49, Keywords = Keywords.All)]
        public void ExecuteTaskYieldStart(string taskName, int taskID)
        {
            WriteEvent(49, taskName, taskID);
        }

        [Event(50, Keywords = Keywords.All)]
        public void ExecuteTaskYieldStop(string taskName, int taskID)
        {
            WriteEvent(50, taskName, taskID);
        }

        [Event(51, Keywords = Keywords.All)]
        public void ExecuteTaskReacquireStart(string taskName, int taskID)
        {
            WriteEvent(51, taskName, taskID);
        }

        [Event(52, Keywords = Keywords.All)]
        public void ExecuteTaskReacquireStop(string taskName, int taskID)
        {
            WriteEvent(52, taskName, taskID);
        }

        [Event(53, Keywords = Keywords.All)]
        public void ProjectGraphConstructionStart(string graphEntryPoints)
        {
            WriteEvent(53, graphEntryPoints);
        }

        [Event(54, Keywords = Keywords.All)]
        public void ProjectGraphConstructionStop(string graphEntryPoints)
        {
            WriteEvent(54, graphEntryPoints);
        }

        [Event(55, Keywords = Keywords.All)]
        public void PacketReadSize(int size)
        {
            WriteEvent(55, size);
        }

        [Event(56, Keywords = Keywords.All)]
        public void TargetUpToDateStart()
        {
            WriteEvent(56);
        }

        [Event(57, Keywords = Keywords.All)]
        public void TargetUpToDateStop(int result)
        {
            WriteEvent(57, result);
        }

        [Event(58, Keywords = Keywords.All)]
        public void CopyUpToDateStart(string path)
        {
            WriteEvent(58, path);
        }

        [Event(59, Keywords = Keywords.All)]
        public void CopyUpToDateStop(string path, bool wasUpToDate)
        {
            WriteEvent(59, path, wasUpToDate);
        }

        [Event(60, Keywords = Keywords.All)]
        public void WriteLinesToFileUpToDateStart()
        {
            WriteEvent(60);
        }

        [Event(61, Keywords = Keywords.All)]
        public void WriteLinesToFileUpToDateStop(string fileItemSpec, bool wasUpToDate)
        {
            WriteEvent(61, fileItemSpec, wasUpToDate);
        }

        [Event(62, Keywords = Keywords.All)]
        public void SdkResolverLoadAllResolversStart()
        {
            WriteEvent(62);
        }

        [Event(63, Keywords = Keywords.All)]
        public void SdkResolverLoadAllResolversStop(int resolverCount)
        {
            WriteEvent(63, resolverCount);
        }

        [Event(64, Keywords = Keywords.All)]
        public void SdkResolverResolveSdkStart()
        {
            WriteEvent(64);
        }

        [Event(65, Keywords = Keywords.All)]
        public void SdkResolverResolveSdkStop(string resolverName, string sdkName, string solutionPath, string projectPath, string sdkPath, bool success)
        {
            WriteEvent(65, resolverName, sdkName, solutionPath, projectPath, sdkPath, success);
        }

        [Event(66, Keywords = Keywords.All)]
        public void CachedSdkResolverServiceResolveSdkStart(string sdkName, string solutionPath, string projectPath)
        {
            WriteEvent(66, sdkName, solutionPath, projectPath);
        }

        [Event(67, Keywords = Keywords.All, Version = 2)]
        public void CachedSdkResolverServiceResolveSdkStop(string sdkName, string solutionPath, string projectPath, bool success, bool wasResultCached)
        {
            WriteEvent(67, sdkName, solutionPath, projectPath, success, wasResultCached);
        }

        /// <remarks>
        /// This events are quite frequent so they are collected by Debug binaries only.
        /// </remarks>
        [Event(68, Keywords = Keywords.All)]
        public void ReusableStringBuilderFactoryStart(int hash, int newCapacity, int oldCapacity, string type)
        {
            WriteEvent(68, hash, newCapacity, oldCapacity, type);
        }

        /// <remarks>
        /// This events are quite frequent so they are collected by Debug binaries only.
        /// </remarks>
        [Event(69, Keywords = Keywords.All)]
        public void ReusableStringBuilderFactoryStop(int hash, int returningCapacity, int returningLength, string type)
        {
            WriteEvent(69, hash, returningCapacity, returningLength, type);
        }

        /// <remarks>
        /// As oppose to other ReusableStringBuilderFactory events this one is expected to happens very un-frequently
        ///    and if it is seen more than 100x per build it might indicates wrong usage patterns resulting into degrading
        ///    efficiency of ReusableStringBuilderFactory. Hence it is collected in release build as well.
        /// </remarks>
        [Event(70, Keywords = Keywords.All)]
        public void ReusableStringBuilderFactoryUnbalanced(int oldHash, int newHash)
        {
            WriteEvent(70, oldHash, newHash);
        }

        [Event(71, Keywords = Keywords.All)]
        public void ProjectCacheCreatePluginInstanceStart(string pluginAssemblyPath)
        {
            WriteEvent(71, pluginAssemblyPath);
        }

        [Event(72, Keywords = Keywords.All)]
        public void ProjectCacheCreatePluginInstanceStop(string pluginAssemblyPath, string pluginTypeName)
        {
            WriteEvent(72, pluginAssemblyPath, pluginTypeName);
        }

        [Event(73, Keywords = Keywords.All)]
        public void ProjectCacheBeginBuildStart(string pluginTypeName)
        {
            WriteEvent(73, pluginTypeName);
        }

        [Event(74, Keywords = Keywords.All)]
        public void ProjectCacheBeginBuildStop(string pluginTypeName)
        {
            WriteEvent(74, pluginTypeName);
        }

        [Event(75, Keywords = Keywords.All)]
        public void ProjectCacheGetCacheResultStart(string pluginTypeName, string projectPath, string targets)
        {
            WriteEvent(75, pluginTypeName, projectPath, targets);
        }

        [Event(76, Keywords = Keywords.All)]
        public void ProjectCacheGetCacheResultStop(string pluginTypeName, string projectPath, string targets, string cacheResultType)
        {
            WriteEvent(76, pluginTypeName, projectPath, targets, cacheResultType);
        }

        [Event(77, Keywords = Keywords.All)]
        public void ProjectCacheEndBuildStart(string pluginTypeName)
        {
            WriteEvent(77, pluginTypeName);
        }

        [Event(78, Keywords = Keywords.All)]
        public void ProjectCacheEndBuildStop(string pluginTypeName)
        {
            WriteEvent(78, pluginTypeName);
        }

        [Event(79, Keywords = Keywords.All)]
        public void OutOfProcSdkResolverServiceRequestSdkPathFromMainNodeStart(int submissionId, string sdkName, string solutionPath, string projectPath)
        {
            WriteEvent(79, submissionId, sdkName, solutionPath, projectPath);
        }

        [Event(80, Keywords = Keywords.All)]
        public void OutOfProcSdkResolverServiceRequestSdkPathFromMainNodeStop(int submissionId, string sdkName, string solutionPath, string projectPath, bool success, bool wasResultCached)
        {
            WriteEvent(80, submissionId, sdkName, solutionPath, projectPath, success, wasResultCached);
        }

        [Event(81, Keywords = Keywords.All)]
        public void SdkResolverFindResolversManifestsStart()
        {
            WriteEvent(81);
        }

        [Event(82, Keywords = Keywords.All)]
        public void SdkResolverFindResolversManifestsStop(int resolverManifestCount)
        {
            WriteEvent(82, resolverManifestCount);
        }

        [Event(83, Keywords = Keywords.All)]
        public void SdkResolverLoadResolversStart()
        {
            WriteEvent(83);
        }

        [Event(84, Keywords = Keywords.All)]
        public void SdkResolverLoadResolversStop(string manifestName, int resolverCount)
        {
            WriteEvent(84, manifestName, resolverCount);
        }

        [Event(85, Keywords = Keywords.All)]
        public void CreateLoadedTypeStart(string assemblyName)
        {
            WriteEvent(85, assemblyName);
        }

        [Event(86, Keywords = Keywords.All)]
        public void CreateLoadedTypeStop(string assemblyName)
        {
            WriteEvent(86, assemblyName);
        }

        [Event(87, Keywords = Keywords.All)]
        public void LoadAssemblyAndFindTypeStart()
        {
            WriteEvent(87);
        }

        [Event(88, Keywords = Keywords.All)]
        public void LoadAssemblyAndFindTypeStop(string assemblyPath, int numberOfPublicTypesSearched)
        {
            WriteEvent(88, assemblyPath, numberOfPublicTypesSearched);
        }

        [Event(89, Keywords = Keywords.All)]
        public void MSBuildServerBuildStart(string commandLine)
        {
            WriteEvent(89, commandLine);
        }

        [Event(90, Keywords = Keywords.All)]
        public void MSBuildServerBuildStop(string commandLine, int countOfConsoleMessages, long sumSizeOfConsoleMessages, string clientExitType, string serverExitType)
        {
            WriteEvent(90, commandLine, countOfConsoleMessages, sumSizeOfConsoleMessages, clientExitType, serverExitType);
        }

        [Event(91, Keywords = Keywords.All)]
        public void ProjectCacheHandleBuildResultStart(string pluginTypeName, string projectPath, string targets)
        {
            WriteEvent(91, pluginTypeName, projectPath, targets);
        }

        [Event(92, Keywords = Keywords.All)]
        public void ProjectCacheHandleBuildResultStop(string pluginTypeName, string projectPath, string targets)
        {
            WriteEvent(92, pluginTypeName, projectPath, targets);
        }
        #endregion
    }
}
