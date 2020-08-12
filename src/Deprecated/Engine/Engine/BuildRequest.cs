// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Request for a linear evaluation of a list of targets in a project
    /// </summary>
    [DebuggerDisplay("BuildRequest (Project={ProjectFileName}, Targets={System.String.Join(\";\", TargetNames)}, NodeIndex={NodeIndex}, HandleId={HandleId})")]
    internal class BuildRequest
    {
        #region Constructors

        internal BuildRequest()
        {
            // used for serialization
        }

        /// <summary>
        /// Called by the Engine
        /// </summary>
        internal BuildRequest
        (
            int handleId,
            string projectFileName,
            string[] targetNames,
            BuildPropertyGroup globalProperties,
            string toolsetVersion,
            int requestId,
            bool useResultsCache,
            bool unloadProjectsOnCompletion
        )
            :
            this
                (
                handleId,
                projectFileName,
                targetNames,
                (IDictionary)null,
                toolsetVersion,
                requestId,
                useResultsCache,
                unloadProjectsOnCompletion
                )
        {
            // Create a hashtable out of the BuildPropertyGroup passed in.
            // This constructor is called only through the object model.
            if (globalProperties != null)
            {
                Hashtable globalPropertiesTable = new Hashtable(globalProperties.Count);
                foreach (BuildProperty property in globalProperties)
                {
                    globalPropertiesTable.Add(property.Name, property.FinalValue);
                }

                this.globalPropertiesPassedByTask = globalPropertiesTable;
                this.globalProperties = globalProperties;
            }
        }

        /// <summary>
        /// Called by the TEM ("generated" request)
        /// </summary>
        internal BuildRequest
        (
            int handleId,
            string projectFileName,
            string[] targetNames,
            IDictionary globalProperties,
            string toolsetVersion,
            int requestId,
            bool useResultsCache,
            bool unloadProjectsOnCompletion
        )
        {
            this.handleId = handleId;
            this.nodeIndex = EngineCallback.invalidNode;
            this.projectFileName = projectFileName;
            this.targetNames = targetNames;
            this.parentEngine = null;
            this.outputsByTarget = null;
            this.resultByTarget = new Hashtable(StringComparer.OrdinalIgnoreCase);
            this.globalPropertiesPassedByTask = null;
            this.globalProperties = null;
            this.buildSucceeded = false;
            this.buildSettings = BuildSettings.None;
            this.projectToBuild = null;
            this.fireProjectStartedFinishedEvents = false;
            this.requestId = requestId;
            this.useResultsCache = useResultsCache;
            this.unloadProjectsOnCompletion = unloadProjectsOnCompletion;
            this.restoredFromCache = false;
            this.toolsetVersion = toolsetVersion;
            this.isExternalRequest = false;
            this.parentHandleId = EngineCallback.invalidEngineHandle;
            this.projectId = 0;
            this.startTime = 0;
            this.processingTotalTime = 0;
            this.taskTime = 0;
            this.toolsVersionPeekedFromProjectFile = false;

            if (globalProperties is Hashtable)
            {
                this.globalPropertiesPassedByTask = (Hashtable)globalProperties;
            }
            else if (globalProperties != null)
            {
                // We were passed an IDictionary that was not a Hashtable. It may
                // not be serializable, so convert it into a Hashtable, which is.
                this.globalPropertiesPassedByTask = new Hashtable(globalProperties.Count);
                foreach (DictionaryEntry newGlobalProperty in globalProperties)
                {
                    this.globalPropertiesPassedByTask.Add(newGlobalProperty.Key,
                                                          newGlobalProperty.Value);
                }
            }
        }
        #endregion

        #region Properties
        /// <summary>
        /// The engine is set inside the proxy prior to enqueing the request
        /// </summary>
        internal Engine ParentEngine
        {
            get
            {
                return this.parentEngine;
            }
            set
            {
                this.parentEngine = value;
            }
        }
        /// <summary>
        /// The outputs of the build request
        /// </summary>
        internal IDictionary OutputsByTarget
        {
            get
            {
                return this.outputsByTarget;
            }
            set
            {
                this.outputsByTarget = value;
            }
        }

        /// <summary>
        /// Build result per target
        /// </summary>
        internal Hashtable ResultByTarget
        {
            get
            {
                return this.resultByTarget;
            }
        }

        /// <summary>
        /// The result of the build request
        /// </summary>
        internal bool BuildSucceeded
        {
            get
            {
                return this.buildSucceeded;
            }
            set
            {
                this.buildSucceeded = value;
            }
        }

        /// <summary>
        /// The list of targets that need to be evaluated
        /// </summary>
        internal string[] TargetNames
        {
            get
            {
                return this.targetNames;
            }
        }

        /// <summary>
        /// The build settings
        /// </summary>
        internal BuildSettings BuildSettings
        {
            get
            {
                return this.buildSettings;
            }
            set
            {
                this.buildSettings = value;
            }
        }

        /// <summary>
        /// The project to be evaluated
        /// </summary>
        internal Project ProjectToBuild
        {
            get
            {
                return this.projectToBuild;
            }
            set
            {
                this.projectToBuild = value;
            }
        }

        internal bool FireProjectStartedFinishedEvents
        {
            get
            {
                return this.fireProjectStartedFinishedEvents;
            }
            set
            {
                this.fireProjectStartedFinishedEvents = value;
            }
        }

        internal int NodeIndex
        {
            get
            {
                return this.nodeIndex;
            }
            set
            {
                this.nodeIndex = value;
            }
        }

        /// <summary>
        /// Maps the BuildRequest to the TaskExecutionContext.
        /// If BuildRequest originated in the Engine itself in CreateLocalBuildRequest, HandleId is EngineCallback.invalidEngineHandle.
        /// </summary>
        internal int HandleId
        {
            get
            {
                return this.handleId;
            }
            set
            {
                this.handleId = value;
            }
        }

        internal int ParentHandleId
        {
            get
            {
                return parentHandleId;
            }
            set
            {
                parentHandleId = value;
            }
        }

        internal int ProjectId
        {
            get
            {
                return this.projectId;
            }
            set
            {
                this.projectId = value;
            }
        }

        internal int ParentRequestId
        {
            get
            {
                return this.parentRequestId;
            }
            set
            {
                this.parentRequestId = value;
            }
        }

        internal string ProjectFileName
        {
            get
            {
                return this.projectFileName;
            }
            set
            {
                this.projectFileName = value;
            }
        }

        internal BuildPropertyGroup GlobalProperties
        {
            get
            {
                return this.globalProperties;
            }
            set
            {
                this.globalProperties = value;
            }
        }

        internal IDictionary GlobalPropertiesPassedByTask
        {
            get
            {
                return this.globalPropertiesPassedByTask;
            }
        }

        internal bool BuildCompleted
        {
            get
            {
                return this.buildCompleted;
            }
            set
            {
                this.buildCompleted = value;
            }
        }

        internal int RequestId
        {
            get
            {
                return this.requestId;
            }
            set
            {
                this.requestId = value;
            }
        }

        /// <summary>
        /// Returns true if this BuildRequest came from a task, rather than
        /// the Host Engine itself.
        /// </summary>
        internal bool IsGeneratedRequest
        {
            get
            {
                return handleId != EngineCallback.invalidEngineHandle;
            }
        }

        /// <summary>
        /// This is set to true if the build request was sent from the parent process
        /// </summary>
        internal bool IsExternalRequest
        {
            get
            {
                return isExternalRequest;
            }
            set
            {
                isExternalRequest = value;
            }
        }

        internal bool UnloadProjectsOnCompletion
        {
            get
            {
                return this.unloadProjectsOnCompletion;
            }
        }

        internal bool UseResultsCache
        {
            get
            {
                return this.useResultsCache;
            }
            set
            {
                this.useResultsCache = value;
            }
        }

        internal string DefaultTargets
        {
            get
            {
                return this.defaultTargets;
            }
            set
            {
                this.defaultTargets = value;
            }
        }

        internal string InitialTargets
        {
            get
            {
                return this.initialTargets;
            }
            set
            {
                this.initialTargets = value;
            }
        }

        internal BuildEventContext ParentBuildEventContext
        {
            get
            {
                return buildEventContext;
            }

            set
            {
                buildEventContext = value;
            }
        }


        internal string ToolsetVersion
        {
            get
            {
                return toolsetVersion;
            }
            set
            {
                this.toolsetVersion = value;
            }
        }

        internal InvalidProjectFileException BuildException
        {
            get
            {
                return buildException;
            }
            set
            {
                buildException = value;
            }
        }

        internal bool ToolsVersionPeekedFromProjectFile
        {
            get
            {
                return toolsVersionPeekedFromProjectFile;
            }
            set
            {
                toolsVersionPeekedFromProjectFile = value;
            }
        }

        /// <summary>
        /// True if the build results in this requests have been restored from the cache
        /// (in which case there's no point in caching them again)
        /// </summary>
        internal bool RestoredFromCache
        {
            get
            {
                return this.restoredFromCache;
            }
        }

        // Temp timing data properties
        internal long StartTime
        {
            get { return startTime; }
            set { startTime = value; }
        }
        
        internal long ProcessingStartTime
        {
            get { return processingStartTime; }
            set { processingStartTime = value; }
        }
        
        internal long ProcessingTotalTime
        {
            get { return processingTotalTime; }
            set { processingTotalTime = value; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Restore the default values which do not travel over the wire
        /// </summary>
        internal void RestoreNonSerializedDefaults()
        {
            this.outputsByTarget = new Hashtable();
            this.resultByTarget = new Hashtable(StringComparer.OrdinalIgnoreCase);
            this.projectToBuild = null;
            this.buildSettings = BuildSettings.None;
            this.fireProjectStartedFinishedEvents = true;
            this.nodeIndex = EngineCallback.invalidNode;
            this.buildCompleted = false;
            this.buildSucceeded = false;
            this.defaultTargets = null;
            this.initialTargets = null;
            this.restoredFromCache = false;
            this.isExternalRequest = false;
            this.parentHandleId = EngineCallback.invalidEngineHandle;
            this.projectId = 0;
            this.startTime = 0;
            this.taskTime = 0;
            this.processingTotalTime = 0;
        }

        /// <summary>
        /// Initialize this build request with a cached build result
        /// </summary>
        /// <param name="cachedResult"></param>
        internal void InitializeFromCachedResult(BuildResult cachedResult)
        {
            this.OutputsByTarget = cachedResult.OutputsByTarget;
            this.BuildSucceeded = cachedResult.EvaluationResult;
            this.BuildCompleted = true;
            this.DefaultTargets = cachedResult.DefaultTargets;
            this.InitialTargets = cachedResult.InitialTargets;
            this.projectId = cachedResult.ProjectId;
            this.restoredFromCache = true;
        }

        internal BuildResult GetBuildResult()
        {
            // Calculate the time spent on this build request
            int totalTime  = 0;
            int engineTime = 0;
            int taskTimeMs = 0;
            if ( startTime != 0 )
            {
               TimeSpan totalTimeSpan = new TimeSpan(DateTime.Now.Ticks - startTime );
               totalTime = (int)totalTimeSpan.TotalMilliseconds;
            }
            if (processingTotalTime != 0)
            {
                TimeSpan processingTimeSpan = new TimeSpan(processingTotalTime);
                engineTime = (int)processingTimeSpan.TotalMilliseconds;
            }
            if (taskTime != 0)
            {
                TimeSpan taskTimeSpan = new TimeSpan(taskTime);
                taskTimeMs = (int)taskTimeSpan.TotalMilliseconds;
            }
            return new BuildResult(outputsByTarget, resultByTarget, buildSucceeded, handleId, requestId, projectId, useResultsCache, defaultTargets, initialTargets, totalTime, engineTime, taskTimeMs);
        }

        /// <summary>
        /// Provides unique identifers for the caching system so we can retrieve this set of targets 
        /// at a later time. This list should be either a null array or a list of strings which are not null.
        /// </summary>
        /// <returns></returns>
        internal string GetTargetNamesList()
        {
            string list = null;
            if (targetNames != null)
            {
                if (targetNames.Length == 1)
                {
                    list = targetNames[0];
                }
                else
                {
                    StringBuilder targetsBuilder = new StringBuilder();
                    foreach (string target in targetNames)
                    {
                        //We are making sure that null targets are not concatonated because they do not count as a valid target
                        ErrorUtilities.VerifyThrowArgumentNull(target, "target should not be null");
                        targetsBuilder.Append(target);
                        targetsBuilder.Append(';');
                    }
                    list = targetsBuilder.ToString();
                }
            }

            return list;
        }

        /// <summary>
        /// This method is called after a task finishes execution in order to add the time spent executing
        /// the task to the total used by the build request
        /// </summary>
        /// <param name="executionTime">execution time of the last task</param>
        internal void AddTaskExecutionTime(long executionTime)
        {
            taskTime += executionTime;
        }

        #endregion

        #region Member data

        private int requestId;
        private int handleId;
        private string projectFileName;
        private string[] targetNames;
        private BuildPropertyGroup globalProperties;
        private string toolsetVersion;
        private bool unloadProjectsOnCompletion;
        private bool useResultsCache;
        // This is the event context of the task / host which made the buildRequest
        // the buildEventContext is used to determine who the parent project is
        private BuildEventContext buildEventContext;

        #region CustomSerializationToStream
        internal void WriteToStream(BinaryWriter writer)
        {
            writer.Write((Int32)requestId);
            writer.Write((Int32)handleId);
            #region ProjectFileName
            if (projectFileName == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(projectFileName);
            }
            #endregion
            #region TargetNames
            //Write Number of HashItems
            if (targetNames == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write((Int32)targetNames.Length);
                foreach (string targetName in targetNames)
                {
                    if (targetName == null)
                    {
                        writer.Write((byte)0);
                    }
                    else
                    {
                        writer.Write((byte)1);
                        writer.Write(targetName);
                    }
                }
            }
            #endregion
            #region GlobalProperties
            // Write the global properties
            if (globalProperties == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                globalProperties.WriteToStream(writer);
            }
            #endregion
            #region ToolsetVersion
            if (toolsetVersion == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(toolsetVersion);
            }
            #endregion
            writer.Write(unloadProjectsOnCompletion);
            writer.Write(useResultsCache);
            #region BuildEventContext
            if (buildEventContext == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write((Int32)buildEventContext.NodeId);
                writer.Write((Int32)buildEventContext.ProjectContextId);
                writer.Write((Int32)buildEventContext.TargetId);
                writer.Write((Int32)buildEventContext.TaskId);
            }
            #endregion
            #region ToolsVersionPeekedFromProjectFile
            // We need to pass this over shared memory because where ever this project is being built needs to know
            // if the tools version was an override or was retreived from the project file
            if (!this.toolsVersionPeekedFromProjectFile)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
            }
            #endregion
        }

        internal static BuildRequest CreateFromStream(BinaryReader reader)
        {
            BuildRequest request = new BuildRequest();
            request.requestId = reader.ReadInt32();
            request.handleId = reader.ReadInt32();
            #region ProjectFileName
            if (reader.ReadByte() == 0)
            {
                request.projectFileName = null;
            }
            else
            {
                request.projectFileName = reader.ReadString();
            }
            #endregion
            #region TargetNames
            if (reader.ReadByte() == 0)
            {
                request.targetNames = null;
            }
            else
            {
                int numberOfTargetNames = reader.ReadInt32();
                request.targetNames = new string[numberOfTargetNames];
                for (int i = 0; i < numberOfTargetNames; i++)
                {
                    if (reader.ReadByte() == 0)
                    {
                        request.targetNames[i] = null;
                    }
                    else
                    {
                        request.targetNames[i] = reader.ReadString();
                    }
                }
            }
            #endregion
            #region GlobalProperties
            if (reader.ReadByte() == 0)
            {
                request.globalProperties = null;
            }
            else
            {
                request.globalProperties = new BuildPropertyGroup();
                request.globalProperties.CreateFromStream(reader);
            }
            #endregion
            #region ToolsetVersion
            if (reader.ReadByte() == 0)
            {
                request.toolsetVersion = null;
            }
            else
            {
                request.toolsetVersion = reader.ReadString();
            }
            #endregion
            request.unloadProjectsOnCompletion = reader.ReadBoolean();
            request.useResultsCache = reader.ReadBoolean();
            #region BuildEventContext
            if (reader.ReadByte() == 0)
            {
                request.buildEventContext = null;
            }
            else
            {
                // Re create event context
                int nodeId = reader.ReadInt32();
                int projectContextId = reader.ReadInt32();
                int targetId = reader.ReadInt32();
                int taskId = reader.ReadInt32();
                request.buildEventContext = new BuildEventContext(nodeId, targetId, projectContextId, taskId);
            }
            #endregion
            #region ToolsVersionPeekedFromProjectFile
            // We need to pass this over shared memory because where ever this project is being built needs to know
            // if the tools version was an override or was retreived from the project file
            if (reader.ReadByte() == 0)
            {
                request.toolsVersionPeekedFromProjectFile = false;
            }
            else
            {
                request.toolsVersionPeekedFromProjectFile = true;
            }
            #endregion
            return request;
        }
        #endregion

        private InvalidProjectFileException buildException;
        private string defaultTargets;
        private string initialTargets;
        private IDictionary outputsByTarget;
        private Hashtable resultByTarget;
        private bool buildCompleted;
        private bool buildSucceeded;
        private Hashtable globalPropertiesPassedByTask;
        private int nodeIndex;
        private Engine parentEngine;
        private Project projectToBuild;
        private bool fireProjectStartedFinishedEvents;
        private BuildSettings buildSettings;
        private bool restoredFromCache;
        private bool isExternalRequest;
        private int parentHandleId;
        private int parentRequestId;
        private int projectId;
        // Timing data - used to profile the build
        private long startTime;
        private long processingStartTime;
        private long processingTotalTime;
        private long taskTime;
        // We peeked at the tools version from the project file because the tools version was null
        private bool toolsVersionPeekedFromProjectFile;
        #endregion
    }
}
