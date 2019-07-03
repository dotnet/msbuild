// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using Microsoft.Build.Framework;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices;
using System.Linq;
using Microsoft.Build.BackEnd;

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
    public class HostServices : ITranslatable
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

#if FEATURE_COM_INTEROP
        private Lazy<IRunningObjectTableWrapper> _runningObjectTable = new Lazy<IRunningObjectTableWrapper>(() => new RunningObjectTable());
#endif

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

            var monikerNameOrITaskHost =
                hostObjects.GetAnyMatchingMonikerNameOrITaskHost(targetName, taskName);

            if (monikerNameOrITaskHost == null)
            {
                return null;
            }
            else
            {
                if (monikerNameOrITaskHost.IsMoniker)
                {
#if FEATURE_COM_INTEROP

                    if (Environment.Is64BitProcess)
                    {
                        throw new PlatformNotSupportedException("GetHostObject with monikerName is only supported in 32 bit");
                    }

                    try
                    {
                        object objectFromRunningObjectTable =
                            _runningObjectTable.Value.GetObject(monikerNameOrITaskHost.MonikerName);
                        return (ITaskHost)objectFromRunningObjectTable;
                    }
                    catch (Exception ex) when (ex is COMException || ex is InvalidCastException)
                    {
                        throw new HostObjectException(projectFile, targetName, taskName, ex);
                    }
#else
                    throw new HostObjectException(
                        projectFile,
                        targetName,
                        taskName,
                        "FEATURE_COM_INTEROP is disabled (non full framework). Host object can only be ITaskHost");
#endif
                }
                else
                {
                    return monikerNameOrITaskHost.TaskHost;
                }
            }
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

            HostObjects hostObjects = GetHostObjectsFromMapByKeyOrCreateNew(projectFile);

            hostObjects.RegisterHostObject(targetName, taskName, hostObject);
        }

#if FEATURE_COM_INTEROP
        /// <summary>
        /// Register a remote host object for a particular task/target pair.
        /// The remote host object require registered in Running Object Table(ROT) already.
        /// Overwrites any existing host object.
        ///
        /// It's caller's responsibly:
        /// To maintain the live cycle of the host object.
        /// Register and unregister from ROT.
        /// Ensure the host object has appropriate COM interface that can be used in task.
        /// </summary>
        /// <param name="monikerName">the Moniker used to register host object in ROT</param>
        public void RegisterHostObject(string projectFile, string targetName, string taskName, string monikerName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectFile, "projectFile");
            ErrorUtilities.VerifyThrowArgumentNull(targetName, "targetName");
            ErrorUtilities.VerifyThrowArgumentNull(taskName, "taskName");
            ErrorUtilities.VerifyThrowArgumentNull(monikerName, "monikerName");

            if (Environment.Is64BitProcess)
            {
                throw new PlatformNotSupportedException("RegisterHostObject with monikerName is only supported in 32 bit");
            }

            _hostObjectMap = _hostObjectMap ?? new Dictionary<string, HostObjects>(StringComparer.OrdinalIgnoreCase);

            HostObjects hostObjects = GetHostObjectsFromMapByKeyOrCreateNew(projectFile);

            hostObjects.RegisterHostObject(targetName, taskName, monikerName);
        }
#endif

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
                if (HasInProcessHostObject(projectFile))
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
        /// Returns true if there is any in process host object registered for this project file.
        /// </summary>
        internal bool HasInProcessHostObject(string projectFile)
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

            return hostObjects.HasRegisteredInProcessHostObjects;
        }

        /// <summary>
        /// Retrieves the node affinity for a particular project file.
        /// </summary>
        private NodeAffinity GetNodeAffinity(string projectFile, out bool isExplicit)
        {
            isExplicit = false;

            // Projects with a registered host object must build in-proc
            if (HasInProcessHostObject(projectFile))
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

        private HostObjects GetHostObjectsFromMapByKeyOrCreateNew(string projectFile)
        {
            if (!_hostObjectMap.TryGetValue(projectFile, out var hostObjects))
            {
                hostObjects = new HostObjects();
                _hostObjectMap[projectFile] = hostObjects;
            }

            return hostObjects;
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                int count = translator.Reader.ReadInt32();

                var hostObjectMap = new Dictionary<string, HostObjects>();
                for (int i = 0; i < count; i++)
                {
                    var pairKey = translator.Reader.ReadString();
                    var hostObjectMapPairKeyTargetName = translator.Reader.ReadString();
                    var hostObjectMapPairKeyTaskName = translator.Reader.ReadString();
                    var hostObjectMapPairValueMonikerName = translator.Reader.ReadString();
                    var targetTaskKey = new HostObjects.TargetTaskKey(hostObjectMapPairKeyTargetName, hostObjectMapPairKeyTaskName);
                    if (!hostObjectMap.ContainsKey(pairKey))
                    {
                        hostObjectMap[pairKey] = new HostObjects();
                    }

                    if (!hostObjectMap[pairKey]._hostObjects.ContainsKey(targetTaskKey))
                    {
                        hostObjectMap[pairKey]._hostObjects.Add(targetTaskKey, new MonikerNameOrITaskHost(hostObjectMapPairValueMonikerName));
                    }
                }
                _hostObjectMap = hostObjectMap;
            }

            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                if (_hostObjectMap == null)
                {
                    translator.Writer.Write(0);
                }
                else
                {
                    var count = 0;
                    foreach (var pair in _hostObjectMap)
                    {
                        foreach (var hostObjectMapPair in pair.Value._hostObjects)
                        {
                            if (hostObjectMapPair.Value.IsMoniker)
                            {
                                count++;
                            }
                        }
                    }

                    translator.Writer.Write(count);

                    foreach (var pair in _hostObjectMap)
                    {
                        foreach (var hostObjectMapPair in pair.Value._hostObjects)
                        {
                            if (hostObjectMapPair.Value.IsMoniker)
                            {
                                translator.Writer.Write(pair.Key);
                                translator.Writer.Write(hostObjectMapPair.Key._targetName);
                                translator.Writer.Write(hostObjectMapPair.Key._taskName);
                                translator.Writer.Write(hostObjectMapPair.Value.MonikerName);
                            }
                        }
                    }
                }
            }
        }

#if FEATURE_COM_INTEROP
        /// <summary>
        /// Test only
        /// </summary>
        /// <param name="runningObjectTable"></param>
        internal void SetTestRunningObjectTable(IRunningObjectTableWrapper runningObjectTable)
        {
            _runningObjectTable = new Lazy<IRunningObjectTableWrapper>(() => runningObjectTable);
        }
#endif

        internal class MonikerNameOrITaskHost
        {
            public ITaskHost TaskHost { get; }
            public string MonikerName { get; }
            public bool IsTaskHost { get; } = false;
            public bool IsMoniker { get; } = false;
            public MonikerNameOrITaskHost(ITaskHost taskHost)
            {
                TaskHost = taskHost;
                IsTaskHost = true;
            }

            public MonikerNameOrITaskHost(string monikerName)
            {
                MonikerName = monikerName;
                IsMoniker = true;
            }
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
            internal Dictionary<TargetTaskKey, MonikerNameOrITaskHost> _hostObjects;

            /// <summary>
            /// Constructor
            /// </summary>
            internal HostObjects()
            {
                _hostObjects = new Dictionary<TargetTaskKey, MonikerNameOrITaskHost>(1);
            }

            /// <summary>
            /// Accessor which indicates if there are any registered in process host objects.
            /// </summary>
            internal bool HasRegisteredInProcessHostObjects
            {
                get
                {
                    return _hostObjects.Any(h => h.Value.IsTaskHost);
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
                    _hostObjects[new TargetTaskKey(targetName, taskName)] = new MonikerNameOrITaskHost(hostObject);
                }
            }

#if FEATURE_COM_INTEROP
            /// <summary>
            /// Registers a host object for this project file
            /// </summary>
            internal void RegisterHostObject(string targetName, string taskName, string monikerName)
            {
                if (monikerName == null)
                {
                    _hostObjects.Remove(new TargetTaskKey(targetName, taskName));
                }
                else
                {
                    _hostObjects[new TargetTaskKey(targetName, taskName)] = new MonikerNameOrITaskHost(monikerName);
                }
            }
#endif

            /// <summary>
            /// Gets any host object for this project file matching the task and target names specified.
            /// </summary>
            internal MonikerNameOrITaskHost GetAnyMatchingMonikerNameOrITaskHost(string targetName, string taskName)
            {
                if (_hostObjects.TryGetValue(new TargetTaskKey(targetName, taskName), out MonikerNameOrITaskHost hostObject))
                {
                    return hostObject;
                }

                return null;
            }

            /// <summary>
            /// Equatable key for the table
            /// </summary>
            internal struct TargetTaskKey : IEquatable<TargetTaskKey>
            {
                /// <summary>
                /// Target name
                /// </summary>
                internal string _targetName;

                /// <summary>
                /// Task name
                /// </summary>
                internal string _taskName;

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
