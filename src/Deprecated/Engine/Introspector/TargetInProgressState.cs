// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is used to construct and contain the state of an inprogress targets. The primary data
    /// includes build requests blocked until this target completes and build requests that must complete 
    /// before this target can make forward process.
    /// </summary>
    internal class TargetInProgessState
    {
        #region Constructors

        internal TargetInProgessState()
        {
        }

        internal TargetInProgessState
        (
            EngineCallback engineCallback,
            Target target, 
            List<ProjectBuildState> waitingBuildStates,
            ProjectBuildState initiatingRequest,
            BuildRequest [] outstandingBuildRequests,
            string projectName
        )
        {
            this.targetId = new TargetIdWrapper(target);
            this.outstandingBuildRequests = outstandingBuildRequests;
            // For each waiting build context try to find the parent target
            this.parentBuildRequests = new List<BuildRequest>();
            this.parentTargets = new List<TargetIdWrapper>();
            this.projectName = projectName;

            // Process the waiting contexts if there are any
            if (waitingBuildStates != null)
            {
                for (int i = 0; i < waitingBuildStates.Count; i++)
                {
                    ProcessBuildContext(engineCallback, waitingBuildStates[i], target);
                }
            }
            // Process the initiating context
            ProcessBuildContext(engineCallback, initiatingRequest, target);
        }

        /// <summary>
        /// Figure out the parent target or the parent build request for the given context
        /// </summary>
        private void ProcessBuildContext(EngineCallback engineCallback, ProjectBuildState buildContext, Target target)
        {
            BuildRequest parentRequest = null;
            TargetIdWrapper parentName = FindParentTarget(engineCallback, buildContext, target, out parentRequest);

            if (parentName != null)
            {
                parentTargets.Add(parentName);
            }
            if (parentRequest != null)
            {
                parentBuildRequests.Add(parentRequest);
            }
        }
        #endregion

        #region Properties

        /// <summary>
        /// Unique identifier for the target
        /// </summary>
        internal TargetIdWrapper TargetId
        {
            get
            {
                return this.targetId;
            }
        }

        /// <summary>
        /// List of unique identifiers for the targets that are blocked until the current 
        /// target completes
        /// </summary>
        internal List<TargetIdWrapper> ParentTargets
        {
            get
            {
                return this.parentTargets;
            }
        }

        /// <summary>
        /// List of build requests that are blocked until the current 
        /// target completes
        /// </summary>
        internal List<BuildRequest> ParentBuildRequests
        {
            get
            {
                return this.parentBuildRequests;
            }
        }

        /// <summary>
        /// Array of build requests that must complete before the current 
        /// target can make forward process
        internal BuildRequest[] OutstandingBuildRequests
        {
            get
            {
                return this.outstandingBuildRequests;
            }
        }

        /// <summary>
        /// An array of unique identifiers for the targets that generated the build requests (parentBuildRequests)
        /// that are blocked until the current target completes. This array can only be calculated given
        /// the target information for all the nodes in the system.
        /// </summary>
        internal TargetIdWrapper[] ParentTargetsForBuildRequests
        {
            get
            {
                return this.parentTargetsForBuildRequests;
            }
            set
            {
                this.parentTargetsForBuildRequests = value;
            }
        }

        /// <summary>
        /// True if the target was requested by the host. The value is only valid for targets on the
        /// parent node.
        /// </summary>
        internal bool RequestedByHost
        {
            get
            {
                if (targetId.nodeId == 0)
                {
                    return requestedByHost;
                }
                return false;
            }
        }

        /// <summary>
        /// Name of the project containing the target (only used for logging)
        /// </summary>
        internal string ProjectName
        {
            get
            {
                return projectName;
            }
        }
        #endregion

        #region Methods

        /// <summary>
        /// Given a build state try to find the parent target that caused this build state to
        /// come into being either via dependent, on error relationship or via IBuildEngine call
        /// </summary>
        internal TargetIdWrapper FindParentTarget
        (
            EngineCallback engineCallback,
            ProjectBuildState buildContext,
            Target target, 
            out BuildRequest parentRequest
        )
        {
            // We need to find the parent target
            parentRequest = null;

            // Skip build states that have already been filled
            if (buildContext.CurrentBuildContextState == ProjectBuildState.BuildContextState.RequestFilled)
            {
                return null;
            }

            // Check if the target was called due to a onerror or depends on call
            if (buildContext.ContainsBlockingTarget(target.Name))
            {
                // Figure out the record for the parent target
                Project containingProject = target.ParentProject;
                Target parentTarget = containingProject.Targets[buildContext.GetParentTarget(target.Name)];
                return new TargetIdWrapper(parentTarget);
            }
            else
            {
                // The build context must have formed due to IBuildEngine call
                ErrorUtilities.VerifyThrow(
                    String.Compare(EscapingUtilities.UnescapeAll(buildContext.NameOfTargetInProgress), target.Name, StringComparison.OrdinalIgnoreCase) == 0,
                    "The target should be the in progress target for the context");
                // This target is called due to IBuildEngine or host request
                return FindParentTargetForBuildRequest(engineCallback, buildContext.BuildRequest, out parentRequest);
            }
        }

        /// <summary>
        /// Given a build request try to find the target that caused it to come into being
        /// </summary>
        private TargetIdWrapper FindParentTargetForBuildRequest
        (
            EngineCallback engineCallback,
            BuildRequest triggeringBuildRequest,
            out BuildRequest parentTriggeringRequest
        )
        {
            parentTriggeringRequest = null;

            // If request is non-external and generated due to IBuildEngine call try
            // to find the target that caused the IBuildEngine call
            if (triggeringBuildRequest.IsGeneratedRequest && !triggeringBuildRequest.IsExternalRequest)
            {
                ExecutionContext executionContext =
                   engineCallback.GetExecutionContextFromHandleId(triggeringBuildRequest.HandleId);

                // If the parent context is not a routing context than we can
                // get the parent target from it
                if (executionContext is TaskExecutionContext)
                {
                    return new TargetIdWrapper(((TaskExecutionContext)executionContext).ParentTarget);
                }
                // If the parent context if a routing context the parent target is not available 
                // on the current node, so store the request instead
                else
                {
                    parentTriggeringRequest = triggeringBuildRequest;
                }
            }
            // If the request is external to the node - store the request since the parent target
            // is not available
            else if (triggeringBuildRequest.IsExternalRequest)
            {
                parentTriggeringRequest = triggeringBuildRequest;
            }
            else
            {
                requestedByHost = true;
            }

            return null;
        }


        /// <summary>
        /// This function checks if the given ProjectBuildState is caused by a given parent target (via
        /// a dependency, onerror or IBuildEngine relationship)
        /// </summary>
        internal bool CheckBuildContextForParentMatch
        (
            EngineCallback engineCallback,
            TargetIdWrapper parentId,
            Target target,
            ProjectBuildState projectBuildState
        )
        {
            BuildRequest parentRequest = null;
            TargetInProgessState.TargetIdWrapper parentName =
                FindParentTarget(engineCallback, projectBuildState, target, out parentRequest);

            if (parentName != null && parentName.Equals(parentId))
            {
                return true;
            }

            if (parentRequest != null)
            {
                for (int j = 0; j < parentBuildRequests.Count; j++)
                {
                    if (parentRequest.HandleId == parentBuildRequests[j].HandleId &&
                        parentRequest.RequestId == parentBuildRequests[j].RequestId)
                    {
                        if (parentTargetsForBuildRequests[j].Equals(parentId))
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
            return false;
        }

        #endregion

        #region Data

        // Unique identifier for the target
        TargetIdWrapper targetId;
        // List of targets waiting on the current target
        List<TargetIdWrapper> parentTargets;
        // List of build requests waiting on the current target
        List<BuildRequest> parentBuildRequests;
        // List of the build requests the target is waiting on
        BuildRequest[] outstandingBuildRequests;
        // Mapping between list of build requests waiting on the current target and targets
        // from which these build reuquests originated
        TargetIdWrapper [] parentTargetsForBuildRequests;
        // Name of the project containing the target (only used for logging)
        string projectName;
        // Set to true if the target had a been requested by host (direct requests from host only occur on
        // parent node)
        bool requestedByHost;
        #endregion

        #region CustomSerializationToStream
        internal void WriteToStream(BinaryWriter writer)
        {
            #region TargetId
            if (targetId == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                targetId.WriteToStream(writer);
            }
            #endregion
            #region ParentTargets
            if (parentTargets == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write((Int32)parentTargets.Count);
                foreach (TargetIdWrapper target in parentTargets)
                {
                    if (target == null)
                    {
                        writer.Write((byte)0);
                    }
                    else
                    {
                        writer.Write((byte)1);
                        target.WriteToStream(writer);
                    }
                }
            }
            #endregion
            #region ParentBuildRequests
            if (parentBuildRequests == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write((Int32)parentBuildRequests.Count);
                foreach (BuildRequest request in parentBuildRequests)
                {
                    if (request == null)
                    {
                        writer.Write((byte)0);
                    }
                    else
                    {
                        writer.Write((byte)1);
                        request.WriteToStream(writer);
                    }
                }
            }
            #endregion
            #region OutstandingBuildRequests
            if (outstandingBuildRequests == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write((Int32)outstandingBuildRequests.Length);
                for (int i = 0; i < outstandingBuildRequests.Length; i++)
                {
                    if (outstandingBuildRequests[i] == null)
                    {
                        writer.Write((byte)0);
                    }
                    else
                    {
                        writer.Write((byte)1);
                        outstandingBuildRequests[i].WriteToStream(writer);
                    }
                }
            }
            #endregion
            #region ParentTargetsForBuildRequests
            if (parentTargetsForBuildRequests == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write((Int32)parentTargetsForBuildRequests.Length);
                for (int i = 0; i < parentTargetsForBuildRequests.Length; i++)
                {
                    if (parentTargetsForBuildRequests[i] == null)
                    {
                        writer.Write((byte)0);
                    }
                    else
                    {
                        writer.Write((byte)1);
                        parentTargetsForBuildRequests[i].WriteToStream(writer);
                    }
                }
            }
            #endregion
            #region ProjectName
            if (projectName == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(projectName);
            }
            #endregion
        }

        internal void CreateFromStream(BinaryReader reader)
        {
            #region TargetId
            if (reader.ReadByte() == 0)
            {
                targetId = null;
            }
            else
            {
                targetId = new TargetIdWrapper();
                targetId.CreateFromStream(reader);
            }
            #endregion
            #region ParentTargets
            if (reader.ReadByte() == 0)
            {
                parentTargets = null;
            }
            else
            {
                int numberOfTargets = reader.ReadInt32();
                parentTargets = new List<TargetIdWrapper>(numberOfTargets);
                for (int i = 0; i < numberOfTargets; i++)
                {
                    if (reader.ReadByte() == 0)
                    {
                        parentTargets.Add(null);
                    }
                    else
                    {
                        TargetIdWrapper wrapper = new TargetIdWrapper();
                        wrapper.CreateFromStream(reader);
                        parentTargets.Add(wrapper);
                    }
                }
            }
            #endregion
            #region ParentBuildRequests
            if (reader.ReadByte() == 0)
            {
                parentBuildRequests = null;
            }
            else
            {
                int numberOfRequests = reader.ReadInt32();
                parentBuildRequests = new List<BuildRequest>(numberOfRequests);
                for (int i = 0; i < numberOfRequests; i++)
                {
                    if (reader.ReadByte() == 0)
                    {
                        parentBuildRequests.Add(null);
                    }
                    else
                    {
                        parentBuildRequests.Add(BuildRequest.CreateFromStream(reader));
                    }
                }
            }
            #endregion
            #region OutstandingBuildRequests
            if (reader.ReadByte() == 0)
            {
                outstandingBuildRequests = null;
            }
            else
            {
                int numberOfBuildRequests = reader.ReadInt32();
                outstandingBuildRequests = new BuildRequest[numberOfBuildRequests];
                for (int i = 0; i < numberOfBuildRequests; i++)
                {
                    if (reader.ReadByte() == 0)
                    {
                        outstandingBuildRequests[i] = null;
                    }
                    else
                    {
                        outstandingBuildRequests[i] = BuildRequest.CreateFromStream(reader);
                    }
                }
            }
            #endregion
            #region ParentTargetsForBuildRequests
            if (reader.ReadByte() == 0)
            {
                parentTargetsForBuildRequests = null;
            }
            else
            {
                int numberOfTargetsForBuildRequests = reader.ReadInt32();
                parentTargetsForBuildRequests = new TargetIdWrapper[numberOfTargetsForBuildRequests];
                for (int i = 0; i < numberOfTargetsForBuildRequests; i++)
                {
                    if (reader.ReadByte() == 0)
                    {
                        parentTargetsForBuildRequests[i] = null;
                    }
                    else
                    {
                        TargetIdWrapper wrapper = new TargetIdWrapper();
                        wrapper.CreateFromStream(reader);
                        parentTargetsForBuildRequests[i] = wrapper;
                    }
                }
            }
            #endregion
            #region ProjectName
            if (reader.ReadByte() == 0)
            {
                projectName = null;
            }
            else
            {
                projectName = reader.ReadString();
            }
            #endregion
        }
        #endregion

        /// <summary>
        /// A class that contains information to uniquely identify a target
        /// </summary>
        internal class TargetIdWrapper
        {
            internal TargetIdWrapper()
            {
            }

            internal TargetIdWrapper(Target target)
            {
                this.name = target.Name;
                this.projectId = target.ParentProject.Id;
                this.nodeId = target.ParentEngine.NodeId;
                this.id = target.Id;
            }

            /// <summary>
            /// Override the equals operator to give valuetype comparison semantics
            /// </summary>
            public override bool Equals(object obj)
            {
                TargetIdWrapper other = obj as TargetIdWrapper;
                if (other != null)
                {
                    if (other.projectId == projectId && other.nodeId == nodeId &&
                        (String.Compare(other.name, name, StringComparison.OrdinalIgnoreCase) == 0))
                    {
                        return true;
                    }
                    return false;
                }

                return base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return projectId;
            }

            // Target name
            internal string name;
            // Id for the parent project 
            internal int projectId;
            // Id for the node where the target exists
            internal int nodeId;
            // Target Id
            internal int id;

            #region CustomSerializationToStream
            internal void WriteToStream(BinaryWriter writer)
            {
                if (name == null)
                {
                    writer.Write((byte)0);
                }
                else
                {
                    writer.Write((byte)1);
                    writer.Write(name);
                }

                writer.Write((Int32)projectId);
                writer.Write((Int32)nodeId);
                writer.Write((Int32)id);
            }

            internal void CreateFromStream(BinaryReader reader)
            {
                if (reader.ReadByte() == 0)
                {
                    name = null;
                }
                else
                {
                    name = reader.ReadString();
                }

                projectId = reader.ReadInt32();
                nodeId = reader.ReadInt32();
                id = reader.ReadInt32();
            }
            #endregion
        }
    }
}
