// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Globalization;

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// Stores and manages projects and targets events for logging purposes
    /// </summary>
    internal class BuildEventManager
    {
        #region Data
        private Dictionary<BuildEventContext, ProjectStartedEventMinimumFields> _projectStartedEvents;
        private Dictionary<BuildEventContext, TargetStartedEventMinimumFields> _targetStartedEvents;
        private Dictionary<string, int> _projectTargetKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int> _projectKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static ComparerContextNodeId<BuildEventContext> s_compareContextNodeId = new ComparerContextNodeId<BuildEventContext>();
        private static ComparerContextNodeIdTargetId<BuildEventContext> s_compareContextNodeIdTargetId = new ComparerContextNodeIdTargetId<BuildEventContext>();
        private int _projectIncrementKey;
        #endregion

        #region Constructors
        internal BuildEventManager()
        {
            _projectStartedEvents = new Dictionary<BuildEventContext, ProjectStartedEventMinimumFields>(s_compareContextNodeId);
            _targetStartedEvents = new Dictionary<BuildEventContext, TargetStartedEventMinimumFields>(s_compareContextNodeIdTargetId);
            _projectIncrementKey = 0;
        }
        #endregion

        #region Methods
        /// <summary>
        ///  Adds a new project to the list of project started events which have been fired
        /// </summary>
        internal void AddProjectStartedEvent(ProjectStartedEventArgs e, bool requireTimestamp)
        {   //Parent event can be null if this is the root project
            ProjectStartedEventMinimumFields parentEvent = GetProjectStartedEvent(e.ParentProjectBuildEventContext);
            lock (_projectStartedEvents)
            {
                if (!_projectStartedEvents.ContainsKey(e.BuildEventContext))
                {
                    int projectTargetKeyLocal = 1;
                    int projectIncrementKeyLocal = 1;
                    // If we haven't seen this project before (by full path) then
                    // allocate a new key for it and save it away
                    if (!_projectKey.ContainsKey(e.ProjectFile))
                    {
                        _projectIncrementKey += 1;

                        _projectKey[e.ProjectFile] = _projectIncrementKey;
                        projectIncrementKeyLocal = _projectIncrementKey;
                    }
                    else
                    {
                        // We've seen this project before, so retrieve it
                        projectIncrementKeyLocal = _projectKey[e.ProjectFile];
                    }

                    // If we haven't seen any entrypoint for the current project (by full path) then
                    // allocate a new entry point key
                    if (!_projectTargetKey.ContainsKey(e.ProjectFile))
                    {
                        _projectTargetKey[e.ProjectFile] = projectTargetKeyLocal;
                    }
                    else
                    {
                        // We've seen this project before, but not this entrypoint, so increment
                        // the entrypoint key that we have.
                        projectTargetKeyLocal = _projectTargetKey[e.ProjectFile] + 1;
                        _projectTargetKey[e.ProjectFile] = projectTargetKeyLocal;
                    }

                    _projectStartedEvents.Add(e.BuildEventContext, new ProjectStartedEventMinimumFields(projectIncrementKeyLocal, projectTargetKeyLocal, e, parentEvent, requireTimestamp));
                }
            }
        }

        /// <summary>
        ///  Adds a new target to the list of project started events which have been fired
        /// </summary>
        internal void AddTargetStartedEvent(TargetStartedEventArgs e, bool requireTimeStamp)
        {
            if (!_targetStartedEvents.ContainsKey(e.BuildEventContext))
            {
                _targetStartedEvents.Add(e.BuildEventContext, new TargetStartedEventMinimumFields(e, requireTimeStamp));
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
                return Array.Empty<string>();
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
            if (_projectStartedEvents.ContainsKey(e))
            {
                buildEvent = _projectStartedEvents[e];
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
            if (_targetStartedEvents.ContainsKey(e))
            {
                buildEvent = _targetStartedEvents[e];
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
                _projectStartedEvents.Remove(e);
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
                _targetStartedEvents.Remove(e);
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
        private DateTime _timeStamp;
        private string _targetNames;
        private string _projectFile;
        private bool _showProjectFinishedEvent;
        private bool _errorInProject;
        private int _projectId;
        private ProjectFullKey _projectFullKey;
        private BuildEventContext _buildEventContext;
        private ProjectStartedEventMinimumFields _parentProjectStartedEvent;
        #endregion

        #region Properties

        internal DateTime TimeStamp
        {
            get
            {
                return _timeStamp;
            }
        }

        internal int ProjectKey
        {
            get
            {
                return _projectFullKey.ProjectKey;
            }
        }

        internal int EntryPointKey
        {
            get
            {
                return _projectFullKey.EntryPointKey;
            }
        }

        internal string FullProjectKey
        {
            get
            {
                return _projectFullKey.ToString();
            }
        }

        internal ProjectStartedEventMinimumFields ParentProjectStartedEvent
        {
            get
            {
                return _parentProjectStartedEvent;
            }
        }

        internal string TargetNames
        {
            get
            {
                return _targetNames;
            }
        }

        internal int ProjectId
        {
            get
            {
                return _projectId;
            }
        }

        internal string ProjectFile
        {
            get
            {
                return _projectFile;
            }
        }

        internal bool ShowProjectFinishedEvent
        {
            get
            {
                return _showProjectFinishedEvent;
            }

            set
            {
                _showProjectFinishedEvent = value;
            }
        }

        internal bool ErrorInProject
        {
            get
            {
                return _errorInProject;
            }

            set
            {
                _errorInProject = value;
            }
        }

        internal BuildEventContext ProjectBuildEventContext
        {
            get
            {
                return _buildEventContext;
            }
        }
        #endregion

        #region Constructors
        internal ProjectStartedEventMinimumFields(int projectKey, int entryPointKey, ProjectStartedEventArgs startedEvent, ProjectStartedEventMinimumFields parentProjectStartedEvent, bool requireTimeStamp)
        {
            _targetNames = startedEvent.TargetNames;
            _projectFile = startedEvent.ProjectFile;
            _showProjectFinishedEvent = false;
            _errorInProject = false;
            _projectId = startedEvent.ProjectId;
            _buildEventContext = startedEvent.BuildEventContext;
            _parentProjectStartedEvent = parentProjectStartedEvent;
            _projectFullKey = new ProjectFullKey(projectKey, entryPointKey);
            if (requireTimeStamp)
            {
                _timeStamp = startedEvent.Timestamp;
            }
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
        private DateTime _timeStamp;
        private string _targetName;
        private string _targetFile;
        private string _projectFile;
        private string _parentTarget;
        private bool _showTargetFinishedEvent;
        private bool _errorInTarget;
        private string _message;
        private BuildEventContext _buildEventContext;
        #endregion

        #region Properties
        internal DateTime TimeStamp
        {
            get
            {
                return _timeStamp;
            }
        }

        internal string TargetName
        {
            get
            {
                return _targetName;
            }
        }

        internal string TargetFile
        {
            get
            {
                return _targetFile;
            }
        }

        internal string ProjectFile
        {
            get
            {
                return _projectFile;
            }
        }

        internal string Message
        {
            get
            {
                return _message;
            }
        }

        internal bool ShowTargetFinishedEvent
        {
            get
            {
                return _showTargetFinishedEvent;
            }

            set
            {
                _showTargetFinishedEvent = value;
            }
        }

        internal bool ErrorInTarget
        {
            get
            {
                return _errorInTarget;
            }

            set
            {
                _errorInTarget = value;
            }
        }
        internal BuildEventContext ProjectBuildEventContext
        {
            get
            {
                return _buildEventContext;
            }
        }

        internal string ParentTarget
        {
            get
            {
                return _parentTarget;
            }
        }
        #endregion

        #region Constructors
        internal TargetStartedEventMinimumFields(TargetStartedEventArgs startedEvent, bool requireTimeStamp)
        {
            _targetName = startedEvent.TargetName;
            _targetFile = startedEvent.TargetFile;
            _projectFile = startedEvent.ProjectFile;
            this.ShowTargetFinishedEvent = false;
            _errorInTarget = false;
            _message = startedEvent.Message;
            _buildEventContext = startedEvent.BuildEventContext;
            if (requireTimeStamp)
            {
                _timeStamp = startedEvent.Timestamp;
            }
            _parentTarget = startedEvent.ParentTarget;
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
        private BuildEventContext _entryPointContext;
        private string _targetName;
        private static ComparerContextNodeId<BuildEventContext> s_eventComparer = new ComparerContextNodeId<BuildEventContext>();
        #endregion

        #region Constructor
        internal ErrorWarningSummaryDictionaryKey(BuildEventContext entryPoint, string targetName)
        {
            _entryPointContext = entryPoint;
            _targetName = targetName == null ? string.Empty : targetName;
        }
        #endregion

        #region Properties
        internal BuildEventContext EntryPointContext
        {
            get
            {
                return _entryPointContext;
            }
        }

        internal string TargetName
        {
            get
            {
                return _targetName;
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
            return s_eventComparer.Equals(_entryPointContext, key.EntryPointContext) && (String.Compare(_targetName, key.TargetName, StringComparison.OrdinalIgnoreCase) == 0);
        }

        public override int GetHashCode()
        {
            return (_entryPointContext.GetHashCode() + _targetName.GetHashCode());
        }
        #endregion

    }

    /// <summary>
    /// Structure that holds both project and entrypoint keys
    /// </summary>
    internal class ProjectFullKey
    {
        #region Data
        private int _projectKey;
        private int _entryPointKey;
        #endregion

        #region Properties
        internal int ProjectKey
        {
            get { return _projectKey; }
            set { _projectKey = value; }
        }

        internal int EntryPointKey
        {
            get { return _entryPointKey; }
            set { _entryPointKey = value; }
        }
        #endregion

        #region Constructor
        internal ProjectFullKey(int projectKey, int entryPointKey)
        {
            _projectKey = projectKey;
            _entryPointKey = entryPointKey;
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
                fullProjectKey = String.Format(CultureInfo.InvariantCulture, "{0}", _projectKey);
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

            if (_entryPointKey > 1)
            {
                fullProjectKey = String.Format(CultureInfo.InvariantCulture, "{0}:{1}", _projectKey, _entryPointKey);
            }
            else
            {
                fullProjectKey = String.Format(CultureInfo.InvariantCulture, "{0}", _projectKey);
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
                return ((compareKey._projectKey == _projectKey) && (compareKey._entryPointKey == _entryPointKey));
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return (_projectKey + (_entryPointKey << 16));
        }
        #endregion
    }
}
