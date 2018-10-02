// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using Microsoft.Build.Framework;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Diagnostics;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Controls where projects must be built.
    /// </summary>
    public enum NodeAffinity
    {
        /// <summary>
        /// The project may only be scheduled on the in-proc node.  This happens automatically if there is a host object or if a ProjectInstance
        /// was specified.  A host may wish to specify it if they know a task depends explicitly on shared static data or other host-provided
        /// objects.
        /// </summary>
        InProc,

        /// <summary>
        /// The project may only be scheduled on an out-of-proc node.  A host may wish to specify this if it is known the project being built
        /// could contaminate the host environment (or the host contaminates the environment while a build is proceeding.)
        /// </summary>
        OutOfProc,

        /// <summary>
        /// The project may be scheduled anywhere.
        /// </summary>
        Any
    }

    /// <summary>
    /// Implementation of HostServices that
    /// mediates access from the build to the host.
    /// </summary>
    [DebuggerDisplay("#Entries={_hostObjectMap.Count}")]
    public class HostServices
    {
        /// <summary>
        /// Collection storing host objects for particular project/task/target combinations.
        /// </summary>
        private Dictionary<string, HostObjects> _hostObjectMap;

        /// <summary>
        /// A mapping of project file names to their node affinities.  An entry for String.Empty means that
        /// all projects which don't otherwise have an affinity should use that affinity.
        /// </summary>
        private Dictionary<string, NodeAffinity> _projectAffinities;

        /// <summary>
        /// Gets any host object applicable to this task name
        /// where the task appears within a target and project with the specified names.
        /// If no host object exists, returns null.
        /// </summary>
        public ITaskHost GetHostObject(string projectFile, string targetName, string taskName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectFile, "projectFile");
            ErrorUtilities.VerifyThrowArgumentNull(targetName, "targetName");
            ErrorUtilities.VerifyThrowArgumentNull(taskName, "taskName");

            HostObjects hostObjects;
            if (_hostObjectMap == null || !_hostObjectMap.TryGetValue(projectFile, out hostObjects))
            {
                return null;
            }

            ITaskHost hostObject = hostObjects.GetAnyMatchingHostObject(targetName, taskName);

            return hostObject;
        }

        /// <summary>
        /// Register a host object for a particular task/target pair.
        /// Overwrites any existing host object.
        /// </summary>
        public void RegisterHostObject(string projectFile, string targetName, string taskName, ITaskHost hostObject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectFile, "projectFile");
            ErrorUtilities.VerifyThrowArgumentNull(targetName, "targetName");
            ErrorUtilities.VerifyThrowArgumentNull(taskName, "taskName");

            // We can only set the host object to a non-null value if the affinity for the project is not out of proc, or if it is, it is only implicitly 
            // out of proc, in which case it will become in-proc after this call completes.  See GetNodeAffinity.
            bool isExplicit;
            bool hasExplicitOutOfProcAffinity = (GetNodeAffinity(projectFile, out isExplicit) == NodeAffinity.OutOfProc) && (isExplicit == true);
            ErrorUtilities.VerifyThrowInvalidOperation(!hasExplicitOutOfProcAffinity || hostObject == null, "InvalidHostObjectOnOutOfProcProject");
            _hostObjectMap = _hostObjectMap ?? new Dictionary<string, HostObjects>(StringComparer.OrdinalIgnoreCase);

            HostObjects hostObjects;
            if (!_hostObjectMap.TryGetValue(projectFile, out hostObjects))
            {
                hostObjects = new HostObjects();
                _hostObjectMap[projectFile] = hostObjects;
            }

            hostObjects.RegisterHostObject(targetName, taskName, hostObject);
        }

        /// <summary>
        /// Unregister the project's host objects, if any and remove any node affinities associated with it.
        /// </summary>
        public void UnregisterProject(string projectFullPath)
        {
            if (projectFullPath != null)
            {
                if (_hostObjectMap != null && _hostObjectMap.ContainsKey(projectFullPath))
                {
                    _hostObjectMap.Remove(projectFullPath);
                }

                if (_projectAffinities != null && _projectAffinities.ContainsKey(projectFullPath))
                {
                    _projectAffinities.Remove(projectFullPath);
                }
            }
        }

        /// <summary>
        /// Retrieves the node affinity for a particular project file.
        /// </summary>
        public NodeAffinity GetNodeAffinity(string projectFile)
        {
            bool isExplicit;
            return GetNodeAffinity(projectFile, out isExplicit);
        }

        /// <summary>
        /// Sets the node affinity for a particular project file.
        /// </summary>
        /// <param name="projectFile">
        /// The project file.  If set to String.Empty, all projects will use the specified affinity.  If set to null, all affinities will be cleared.
        /// </param>
        /// <param name="nodeAffinity">The <see cref="NodeAffinity"/> to set.</param>
        public void SetNodeAffinity(string projectFile, NodeAffinity nodeAffinity)
        {
            if (projectFile == null)
            {
                _projectAffinities = null;
            }
            else
            {
                if (HasHostObject(projectFile))
                {
                    ErrorUtilities.VerifyThrowInvalidOperation(nodeAffinity == NodeAffinity.InProc, "InvalidAffinityForProjectWithHostObject");
                }

                if (_projectAffinities == null)
                {
                    _projectAffinities = new Dictionary<string, NodeAffinity>(StringComparer.OrdinalIgnoreCase);
                }

                _projectAffinities[projectFile] = nodeAffinity;
            }
        }

        /// <summary>
        /// Updates the host object table when a project is renamed.
        /// Old full path may be null.
        /// </summary>
        public void OnRenameProject(string oldFullPath, string newFullPath)
        {
            HostObjects hostObjects;
            if (oldFullPath != null && _hostObjectMap != null && _hostObjectMap.TryGetValue(oldFullPath, out hostObjects))
            {
                _hostObjectMap.Remove(oldFullPath);
                _hostObjectMap[newFullPath] = hostObjects;
            }
        }

        /// <summary>
        /// Returns true if there is any host object registered for this project file.
        /// </summary>
        internal bool HasHostObject(string projectFile)
        {
            if (_hostObjectMap == null)
            {
                return false;
            }

            HostObjects hostObjects;

            if (!_hostObjectMap.TryGetValue(projectFile, out hostObjects))
            {
                return false;
            }

            return hostObjects.HasRegisteredHostObjects;
        }

        /// <summary>
        /// Retrieves the node affinity for a particular project file.
        /// </summary>
        private NodeAffinity GetNodeAffinity(string projectFile, out bool isExplicit)
        {
            isExplicit = false;

            // Projects with a registered host object must build in-proc
            if (HasHostObject(projectFile))
            {
                return NodeAffinity.InProc;
            }

            // Now see if a specific affinity has been provided.
            if (_projectAffinities != null)
            {
                NodeAffinity affinity = NodeAffinity.Any;

                if (_projectAffinities.TryGetValue(projectFile, out affinity))
                {
                    isExplicit = true;
                    return affinity;
                }

                if (_projectAffinities.TryGetValue(String.Empty, out affinity))
                {
                    return affinity;
                }
            }

            // Attempts to find a specific affinity failed, so just go with Any. 
            return NodeAffinity.Any;
        }

        /// <summary>
        /// Bag holding host object information for a single project file.
        /// </summary>
        [DebuggerDisplay("#HostObjects={_hostObjects.Count}")]
        private class HostObjects
        {
            /// <summary>
            /// The mapping of targets and tasks to host objects.
            /// </summary>
            private Dictionary<TargetTaskKey, ITaskHost> _hostObjects;

            /// <summary>
            /// Constructor
            /// </summary>
            internal HostObjects()
            {
                _hostObjects = new Dictionary<TargetTaskKey, ITaskHost>(1);
            }

            /// <summary>
            /// Accessor which indicates if there are any registered host objects.
            /// </summary>
            internal bool HasRegisteredHostObjects
            {
                get
                {
                    return _hostObjects.Count > 0;
                }
            }

            /// <summary>
            /// Registers a host object for this project file
            /// </summary>
            internal void RegisterHostObject(string targetName, string taskName, ITaskHost hostObject)
            {
                if (hostObject == null)
                {
                    _hostObjects.Remove(new TargetTaskKey(targetName, taskName));
                }
                else
                {
                    _hostObjects[new TargetTaskKey(targetName, taskName)] = hostObject;
                }
            }

            /// <summary>
            /// Gets any host object for this project file matching the task and target names specified.
            /// </summary>
            internal ITaskHost GetAnyMatchingHostObject(string targetName, string taskName)
            {
                ITaskHost hostObject;
                _hostObjects.TryGetValue(new TargetTaskKey(targetName, taskName), out hostObject);

                return hostObject;
            }

            /// <summary>
            /// Equatable key for the table
            /// </summary>
            private struct TargetTaskKey : IEquatable<TargetTaskKey>
            {
                /// <summary>
                /// Target name
                /// </summary>
                private string _targetName;

                /// <summary>
                /// Task name
                /// </summary>
                private string _taskName;

                /// <summary>
                /// Constructor
                /// </summary>
                public TargetTaskKey(string targetName, string taskName)
                {
                    _targetName = targetName;
                    _taskName = taskName;
                }

                /// <summary>
                /// Implementation of IEquatable.
                /// </summary>
                public bool Equals(TargetTaskKey other)
                {
                    bool result = (String.Equals(_targetName, other._targetName, StringComparison.OrdinalIgnoreCase) &&
                                   String.Equals(_taskName, other._taskName, StringComparison.OrdinalIgnoreCase));

                    return result;
                }
            }
        }
    }
}