// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Globalization;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Stores and manages projects and targets events for logging purposes
    /// </summary>
    internal class BuildEventManager
    {
        #region Data
        private Dictionary<BuildEventContext, ProjectStartedEventMinimumFields> projectStartedEvents;
        private Dictionary<BuildEventContext, TargetStartedEventMinimumFields> targetStartedEvents;
        private Dictionary<string, int> projectTargetKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int> projectKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static ComparerContextNodeId<BuildEventContext> compareContextNodeId = new ComparerContextNodeId<BuildEventContext>();
        private static ComparerContextNodeIdTargetId<BuildEventContext> compareContextNodeIdTargetId = new ComparerContextNodeIdTargetId<BuildEventContext>();
        private int projectIncrementKey;
        #endregion

        #region Constructors
        internal BuildEventManager()
        {
            projectStartedEvents = new Dictionary<BuildEventContext, ProjectStartedEventMinimumFields>(compareContextNodeId);
            targetStartedEvents = new Dictionary<BuildEventContext, TargetStartedEventMinimumFields>(compareContextNodeIdTargetId);
            projectIncrementKey = 0;
        }
        #endregion

        #region Methods
        /// <summary>
        ///  Adds a new project to the list of project started events which have been fired
        /// </summary>
        internal void AddProjectStartedEvent(ProjectStartedEventArgs e)
        {   //Parent event can be null if this is the root project
            ProjectStartedEventMinimumFields parentEvent = GetProjectStartedEvent(e.ParentProjectBuildEventContext);
            lock (projectStartedEvents)
            {
                if (!projectStartedEvents.ContainsKey(e.BuildEventContext))
                {
                    int projectTargetKeyLocal = 1;
                    int projectIncrementKeyLocal = 1;
                    // If we haven't seen this project before (by full path) then
                    // allocate a new key for it and save it away
                    if (!projectKey.ContainsKey(e.ProjectFile))
                    {
                        projectIncrementKey += 1;

                        projectKey[e.ProjectFile] = projectIncrementKey;
                        projectIncrementKeyLocal = projectIncrementKey;
                    }
                    else
                    {
                        // We've seen this project before, so retrieve it
                        projectIncrementKeyLocal = projectKey[e.ProjectFile];
                    }

                    // If we haven't seen any entrypoint for the current project (by full path) then
                    // allocate a new entry point key
                    if (!projectTargetKey.ContainsKey(e.ProjectFile))
                    {
                        projectTargetKey[e.ProjectFile] = projectTargetKeyLocal;
                    }
                    else
                    {
                        // We've seen this project before, but not this entrypoint, so increment
                        // the entrypoint key that we have.
                        projectTargetKeyLocal = projectTargetKey[e.ProjectFile] + 1;
                        projectTargetKey[e.ProjectFile] = projectTargetKeyLocal;
                    }

                    projectStartedEvents.Add(e.BuildEventContext, new ProjectStartedEventMinimumFields(projectIncrementKeyLocal, projectTargetKeyLocal, e, parentEvent));
                }
            }
        }

        /// <summary>
        ///  Adds a new target to the list of project started events which have been fired
        /// </summary>
        internal void AddTargetStartedEvent(TargetStartedEventArgs e)
        {
            if (!targetStartedEvents.ContainsKey(e.BuildEventContext))
            {
                targetStartedEvents.Add(e.BuildEventContext, new TargetStartedEventMinimumFields(e));
            }
        }

        /// <summary>
        /// Get a call stack of event contexts for a starting point event context
        /// </summary>
        internal List<ProjectStartedEventMinimumFields> GetProjectCallStack(BuildEventContext e)
        {
            List<ProjectStartedEventMinimumFields> stackTrace = new List<ProjectStartedEventMinimumFields>();

            ProjectStartedEventMinimumFields currentKey = GetProjectStartedEvent(e);

            // currentKey can be null if the stack trace is requested before the project started event has been seen
            // or if the call stack is requested by an event which is not associated with a project such as an event
            // from the engine itself
            if (currentKey != null)
            {
                //Add the event where the stack should start
                stackTrace.Add(currentKey);

                // Loop through the call tree until the root project started event has been found
                while (currentKey.ParentProjectStartedEvent != null)
                {
                    currentKey = currentKey.ParentProjectStartedEvent;
                    stackTrace.Add(currentKey);
                }
            }
            return stackTrace;
        }

        /// <summary>
        /// Set an error flag on all projects in the call stack of a given event context
        /// </summary>
        internal void SetErrorWarningFlagOnCallStack(BuildEventContext e)
        {
            List<ProjectStartedEventMinimumFields> projectStackTrace = GetProjectCallStack(e);
            foreach (ProjectStartedEventMinimumFields startedEvent in projectStackTrace)
            {
                // Can be null if the event occures before the project startedEvent or outside of a project
                if (startedEvent != null)
                {
                    startedEvent.ErrorInProject = true;
                }
            }
        }

        /// <summary>
        /// Retrieve the project call stack based on the starting point of buildEventContext e
        /// </summary>
        internal string[] ProjectCallStackFromProject(BuildEventContext e)
        {
            BuildEventContext currentKey = e;

            ProjectStartedEventMinimumFields startedEvent = GetProjectStartedEvent(currentKey);

            List<string> stackTrace = new List<string>();
            // If there is no started event then there should be no stack trace
            // this is a valid situation if the event occures in the engine or outside the context of a project
            // or the event is raised before the project started event
            if (startedEvent == null)
            {
                return new string[0];
            }

            List<ProjectStartedEventMinimumFields> projectStackTrace = GetProjectCallStack(e);
            foreach (ProjectStartedEventMinimumFields projectStartedEvent in projectStackTrace)
            {
                if (!string.IsNullOrEmpty(projectStartedEvent.TargetNames))
                {
                    stackTrace.Add(ResourceUtilities.FormatResourceString("ProjectStackWithTargetNames", projectStartedEvent.ProjectFile, projectStartedEvent.TargetNames, projectStartedEvent.FullProjectKey));
                }
                else
                {
                    stackTrace.Add(ResourceUtilities.FormatResourceString("ProjectStackWithDefaultTargets", projectStartedEvent.ProjectFile, projectStartedEvent.FullProjectKey));
                }
            }
            stackTrace.Reverse();
            return stackTrace.ToArray();
        }

        /// <summary>
        /// Get a deferred project started event based on a given event context
        /// </summary>
        internal ProjectStartedEventMinimumFields GetProjectStartedEvent(BuildEventContext e)
        {
            ProjectStartedEventMinimumFields buildEvent;
            if ( projectStartedEvents.ContainsKey(e) )
            {
                buildEvent = projectStartedEvents[e];
            }
            else
            {
                buildEvent = null;
            }
            return buildEvent;
        }

        /// <summary>
        ///  Get a deferred target started event based on a given event context
        /// </summary>
        internal TargetStartedEventMinimumFields GetTargetStartedEvent(BuildEventContext e)
        {
            TargetStartedEventMinimumFields buildEvent;
            if ( targetStartedEvents.ContainsKey(e))
            {
                buildEvent = targetStartedEvents[e];
            }
            else
            {
                buildEvent = null;
            }
            return buildEvent;
        }

        /// <summary>
        /// Will remove a project started event from the list of deferred project started events
        /// </summary>
        internal void RemoveProjectStartedEvent(BuildEventContext e)
        {
            ProjectStartedEventMinimumFields startedEvent = GetProjectStartedEvent(e);
            // Only remove the project from the event list if it is in the list, and no errors have occured in the project
            if (startedEvent != null && !startedEvent.ErrorInProject)
            {
                projectStartedEvents.Remove(e);
            }
        }

        /// <summary>
        /// Will remove a project started event from the list of deferred project started events
        /// </summary>
        internal void RemoveTargetStartedEvent(BuildEventContext e)
        {
            TargetStartedEventMinimumFields startedEvent = GetTargetStartedEvent(e);
            // Only remove the project from the event list if it is in the list, and no errors have occured in the project
            if (startedEvent != null && !startedEvent.ErrorInTarget)
            {
                targetStartedEvents.Remove(e);
            }
        }
        #endregion
    }

    /// <summary>
    /// Compares two event contexts on ProjectContextId and NodeId only
    /// </summary>
    internal class ComparerContextNodeId<T> : IEqualityComparer<T>
    {
        #region Methods
        public bool Equals(T x, T y)
        {
            BuildEventContext contextX = x as BuildEventContext;
            BuildEventContext contextY = y as BuildEventContext;

            if (contextX == null || contextY == null)
            {
                return false;
            }

            // Return true if the fields match:
            return (contextX.NodeId == contextY.NodeId)
                   && (contextX.ProjectContextId == contextY.ProjectContextId);
        }

        public int GetHashCode(T x)
        {
            BuildEventContext context = x as BuildEventContext;
            return (context.ProjectContextId + (context.NodeId << 24));
        }
        #endregion
    }

    /// <summary>
    /// Compares two event contexts based on the ProjectContextId, NodeId, and TargetId only
    /// </summary>
    internal class ComparerContextNodeIdTargetId<T> : IEqualityComparer<T>
    {
        #region Methods
        public bool Equals(T x, T y)
        {
            BuildEventContext contextX = x as BuildEventContext;
            BuildEventContext contextY = y as BuildEventContext;

            if (contextX == null || contextY == null)
            {
                return false;
            }

            // Return true if the fields match:
            return (contextX.NodeId == contextY.NodeId)
                   && (contextX.ProjectContextId == contextY.ProjectContextId)
                   && (contextX.TargetId == contextY.TargetId);
        }

        public int GetHashCode(T x)
        {
            BuildEventContext context = x as BuildEventContext;
            return (context.ProjectContextId + (context.NodeId << 24));
        }

        #endregion
    }

    /// <summary>
    /// This class stands in for a full project started event because it contains only the 
    /// minimum amount of inforomation needed for the logger
    /// </summary>
    internal class ProjectStartedEventMinimumFields
    {
        #region Data
        private DateTime timeStamp;
        private string targetNames;
        private string projectFile;
        private bool showProjectFinishedEvent;
        private bool errorInProject;
        private int projectId;
        private ProjectFullKey projectFullKey;
        private BuildEventContext buildEventContext;
        private ProjectStartedEventMinimumFields parentProjectStartedEvent;
        #endregion

        #region Properties

        internal DateTime TimeStamp
        {
            get
            {
                return timeStamp;
            }
        }

        internal int ProjectKey
        {
            get
            {
                return projectFullKey.ProjectKey;
            }
        }

        internal int EntryPointKey
        {
            get
            {
                return projectFullKey.EntryPointKey;
            }
        }

        internal string FullProjectKey
        {
            get
            {
                return projectFullKey.ToString();
            }
        }

        internal ProjectStartedEventMinimumFields ParentProjectStartedEvent
        {
            get
            {
                return parentProjectStartedEvent;
            }
        }

        internal string TargetNames
        {
            get
            {
                return targetNames;
            }
        }

        internal int ProjectId
        {
            get
            {
                return projectId;
            }
        }

        internal string ProjectFile
        {
            get
            {
                return projectFile;
            }
        }

        internal bool ShowProjectFinishedEvent
        {
            get
            {
                return showProjectFinishedEvent;
            }

            set
            {
                showProjectFinishedEvent = value;
            }
        }

        internal bool ErrorInProject
        {
            get
            {
                return this.errorInProject;
            }

            set
            {
                this.errorInProject = value;
            }
        }

        internal BuildEventContext ProjectBuildEventContext
        {
            get
            {
                return this.buildEventContext;
            }
        }
        #endregion

        #region Constructors
        internal ProjectStartedEventMinimumFields(int projectKey, int entryPointKey, ProjectStartedEventArgs startedEvent, ProjectStartedEventMinimumFields parentProjectStartedEvent)
        {
            this.targetNames = startedEvent.TargetNames;
            this.projectFile = startedEvent.ProjectFile;
            this.showProjectFinishedEvent = false;
            this.errorInProject = false;
            this.projectId = startedEvent.ProjectId;
            this.buildEventContext = startedEvent.BuildEventContext;
            this.parentProjectStartedEvent = parentProjectStartedEvent;
            this.projectFullKey = new ProjectFullKey(projectKey, entryPointKey);
            this.timeStamp = startedEvent.Timestamp;
        }
        #endregion
    }

    /// <summary>
    /// This class stands in for a full target started event because it contains only the 
    /// minimum amount of inforomation needed for the logger
    /// </summary>
    internal class TargetStartedEventMinimumFields
    {
        #region Data
        private DateTime timeStamp;
        private string targetName;
        private string targetFile;
        private bool showTargetFinishedEvent;
        private bool errorInTarget;
        private BuildEventContext buildEventContext;
        #endregion

        #region Properties
        internal DateTime TimeStamp
        {
            get
            {
                return timeStamp;
            }
        }

        internal string TargetName
        {
            get
            {
                return targetName;
            }
        }

        internal string TargetFile
        {
            get
            {
                return targetFile;
            }
        }

        internal bool ShowTargetFinishedEvent
        {
            get
            {
                return showTargetFinishedEvent;
            }

            set
            {
                showTargetFinishedEvent = value;
            }
        }

        internal bool ErrorInTarget
        {
            get
            {
                return this.errorInTarget;
            }

            set
            {
                this.errorInTarget = value;
            }
        }
        internal BuildEventContext ProjectBuildEventContext
        {
            get
            {
                return this.buildEventContext;
            }
        }
        #endregion

        #region Constructors
        internal TargetStartedEventMinimumFields(TargetStartedEventArgs startedEvent)
        {
            this.targetName = startedEvent.TargetName;
            this.targetFile = startedEvent.TargetFile;
            this.ShowTargetFinishedEvent = false;
            this.errorInTarget = false;
            this.buildEventContext = startedEvent.BuildEventContext;
            this.timeStamp = startedEvent.Timestamp;

        }
        #endregion
    }

    /// <summary>
    /// This class is used as a key to group warnings and errors by the project entry point and the target they 
    /// error or warning was in
    /// </summary>
    internal class ErrorWarningSummaryDictionaryKey
    {
        #region Data
        private BuildEventContext entryPointContext;
        private string targetName;
        private static ComparerContextNodeId<BuildEventContext> eventComparer = new ComparerContextNodeId<BuildEventContext>();
        #endregion

        #region Constructor
        internal ErrorWarningSummaryDictionaryKey(BuildEventContext entryPoint, string targetName)
        {
            this.entryPointContext = entryPoint;
            this.targetName = targetName == null ? string.Empty : targetName;
        }
        #endregion

        #region Properties
        internal BuildEventContext EntryPointContext
        {
            get
            {
                return entryPointContext;
            }
        }

        internal string TargetName
        {
            get
            {
                return targetName;
            }
        }

        #endregion

        #region Equality

        public override bool Equals(object obj)
        {
            ErrorWarningSummaryDictionaryKey key = obj as ErrorWarningSummaryDictionaryKey;
            if (key == null)
            {
                return false;
            }
           return  eventComparer.Equals(entryPointContext, key.EntryPointContext) && (String.Compare(targetName, key.TargetName, StringComparison.OrdinalIgnoreCase) == 0);
        }

        public override int GetHashCode()
        {
            return (entryPointContext.GetHashCode() + targetName.GetHashCode());
        }
        #endregion

    }

    /// <summary>
    /// Structure that holds both project and entrypoint keys
    /// </summary>
    internal class ProjectFullKey
    {
        #region Data
        private int projectKey;
        private int entryPointKey;
        #endregion

        #region Properties
        internal int ProjectKey
        {
            get { return projectKey; }
            set { projectKey = value; }
        }

        internal int EntryPointKey
        {
            get { return entryPointKey; }
            set { entryPointKey = value; }
        }
        #endregion

        #region Constructor
        internal ProjectFullKey(int projectKey, int entryPointKey)
        {
            this.projectKey = projectKey;
            this.entryPointKey = entryPointKey;
        }
        #endregion

        #region ToString
        /// <summary>
        /// Output the projectKey or the projectKey and the entrypointKey depending on the verbosity level of the logger
        /// </summary>
        
        public string ToString(LoggerVerbosity verbosity)
        {
            string fullProjectKey;

            if (verbosity > LoggerVerbosity.Normal)
            {
                fullProjectKey = this.ToString();
            }
            else
            {
                fullProjectKey = String.Format(CultureInfo.InvariantCulture, "{0}", projectKey);
            }

            return fullProjectKey;
        }
        /// <summary>
        /// The default of he ToString method should  be to output the projectKey or the projectKey and the entrypointKey depending if a
        /// entry point key exists or not
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string fullProjectKey;

            if (entryPointKey > 1)
            {
                fullProjectKey = String.Format(CultureInfo.InvariantCulture, "{0}:{1}", projectKey, entryPointKey);
            }
            else
            {
                fullProjectKey = String.Format(CultureInfo.InvariantCulture, "{0}", projectKey);
            }

            return fullProjectKey;
        }
        #endregion

        #region Equality
        public override bool Equals(object obj)
        {
            ProjectFullKey compareKey = obj as ProjectFullKey;
            if (compareKey != null)
            {
                
                return ((compareKey.projectKey == this.projectKey) && (compareKey.entryPointKey == this.entryPointKey));
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return (this.projectKey + (this.entryPointKey << 16));
        }
        #endregion
    }
}
