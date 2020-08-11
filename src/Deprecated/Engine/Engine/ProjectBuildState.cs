// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is used to maintain a state of execution of a build request. Once a build request is
    /// received it is wrapped in this class 
    /// </summary>
    [DebuggerDisplay("ProjectBuildState (NameOfTargetInProgress={NameOfTargetInProgress}, NameOfBlockingTarget={NameOfBlockingTarget}, BuildResult={BuildResult}, BuildComplete={BuildComplete})")]
    internal sealed class ProjectBuildState
    {
        #region Constructors

        /// <summary>
        /// Create a build request from the list of targets to build and build request object
        /// </summary>
        internal ProjectBuildState(BuildRequest buildRequest, ArrayList targetNamesToBuild, BuildEventContext buildEventContext)
        {
            this.buildRequest = buildRequest;
            this.indexOfTargetInProgress = 0;
            this.targetNamesToBuild = targetNamesToBuild;
            this.buildContextState = BuildContextState.StartingFirstTarget;
            this.projectBuildEventContext = buildEventContext;
        }

        #endregion

        #region Properties
        /// <summary>
        /// Current state of the context
        /// </summary>
        internal BuildContextState CurrentBuildContextState
        {
            get
            {
                return this.buildContextState;
            }
            set
            {
                this.buildContextState = value;
            }
        }

        /// <summary>
        /// True if the project build context has been completed either successfully or with a failure
        /// </summary>
        internal bool BuildComplete
        {
            get
            {
                return this.buildRequest.BuildCompleted;
            }
            set
            {
                this.buildRequest.BuildCompleted = value;
            }
        }

        /// <summary>
        /// True if the project build context has been completed successfully, false otherwise
        /// </summary>
        internal bool BuildResult
        {
            get
            {
                return this.buildRequest.BuildSucceeded;
            }
            set
            {
                this.buildRequest.BuildSucceeded = value;
            }
        }

        /// <summary>
        /// Name of the target that blocks the in progress target. Null there is no target
        /// blocking the inprogress target
        /// </summary>
        internal string NameOfBlockingTarget
        {
            get
            {
                return requiredTargets?.Count > 0 ?
                        this.requiredTargets.Peek() : null;
            }
        }

        /// <summary>
        /// Name of the target currently in progress
        /// </summary>
        internal string NameOfTargetInProgress
        {
            get
            {
                return (string)this.targetNamesToBuild[indexOfTargetInProgress];
            }
        }

        /// <summary>
        /// List of targets that need to be completed in order to complete the context
        /// </summary>
        internal ArrayList TargetNamesToBuild
        {
            get
            {
                return this.targetNamesToBuild;
            }
        }

        /// <summary>
        /// Build request that caused the context to come into existance (either from the host or generated)
        /// </summary>
        internal BuildRequest BuildRequest
        {
            get
            {
                return this.buildRequest;
            }
        }

        #endregion

        #region Methods
        /// <summary>
        /// Move to the next target in the context. Return null if there is no next target
        /// </summary>
        /// <returns></returns>
        internal string GetNextTarget()
        {
            if ((indexOfTargetInProgress + 1) < targetNamesToBuild.Count)
            {
                indexOfTargetInProgress++;
                return (string)targetNamesToBuild[indexOfTargetInProgress];
            }
            else
            {
                // All targets in this context have been build
                return null;
            }
        }

        /// <summary>
        /// Remove the top blocking target 
        /// </summary>
        internal void RemoveBlockingTarget()
        {
            ErrorUtilities.VerifyThrow(requiredTargets.Count > 0, "No target to remove");
            requiredTargets.Pop();
        }

        /// <summary>
        /// Add another blocking target 
        /// </summary>
        internal void AddBlockingTarget(string targetName)
        {
            if (requiredTargets == null)
            {
                requiredTargets = new Stack<string>();
            }
            buildContextState = BuildContextState.StartingBlockingTarget;
            requiredTargets.Push(targetName);
        }

        /// <summary>
        /// Marks the build context and build result as complete with the given result
        /// </summary>
        internal void RecordBuildCompletion(bool result)
        {
            buildContextState = ProjectBuildState.BuildContextState.BuildComplete;
            buildRequest.BuildCompleted = true;
            buildRequest.BuildSucceeded = result;
        }

        /// <summary>
        /// Marks the build context and build result appropriate for an exception thrown within
        /// a build context
        /// </summary>
        internal void RecordBuildException()
        {
            buildContextState = ProjectBuildState.BuildContextState.ExceptionThrown;
            buildRequest.BuildCompleted = true;
            buildRequest.BuildSucceeded = false;
        }

        #region Methods used for cycle detection only
        /// <summary>
        /// This method returns true if the top blocking target appears in the stack of
        /// blocking targets more than once, thus forming a cycle.
        /// </summary>
        internal bool ContainsCycle(string name)
        {
            bool containsCycle = false;
            if (requiredTargets?.Count > 1)
            {
                string topTarget = requiredTargets.Pop();
                ErrorUtilities.VerifyThrow(topTarget == name, "Requesting target should be on the top of stack");
                containsCycle = requiredTargets.Contains(name);
                requiredTargets.Push(topTarget);
            }
            if (!containsCycle && requiredTargets?.Count > 0)
            {
                containsCycle = 
                    (String.Equals(name, (string)targetNamesToBuild[indexOfTargetInProgress], StringComparison.OrdinalIgnoreCase));
            }
            return containsCycle;
        }

        /// <summary>
        /// This method return true if a given target name appears anywhere in the list of 
        /// blocking targets
        /// </summary>
        internal bool ContainsBlockingTarget(string name)
        {
            bool containsName = false;
            if (requiredTargets?.Count > 0)
            {
                containsName = requiredTargets.Contains(name);
            }
            return containsName;
        }

        /// <summary>
        /// This method is used by the target cycle detector to find the parent target for
        /// the given target. The parent only exists if there is at least 1 
        /// blocking target. If there is less than 1 blocking target the parent is determined
        /// by the orgin of the build request that caused this build context.
        /// </summary>
        internal string GetParentTarget(string name)
        {
            string parentName = null;
            if (requiredTargets?.Count > 0)
            {
                parentName = (string)targetNamesToBuild[indexOfTargetInProgress];

                if (requiredTargets.Count > 1)
                {
                    string[] requiredTargetsArray = requiredTargets.ToArray();

                    for (int i = requiredTargetsArray.Length-1; i >= 0; i--)
                    {
                        if (string.CompareOrdinal(requiredTargetsArray[i], name) != 0)
                        {
                            parentName = requiredTargetsArray[i];
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            return parentName;
        }

        /// <summary>
        /// Contains the event context for the build context, this is created when the ProjectBuildState is created
        /// </summary>
        internal BuildEventContext ProjectBuildEventContext
        {
            get
            {
                return this.projectBuildEventContext;
            }
        }
        #endregion

        #endregion

        #region Enums
        /// <summary>
        /// States of execution of a build request
        /// </summary>
        internal enum BuildContextState
        {
            /// <summary>
            /// Starting the first target within the request. Default initial state.
            /// </summary>
            StartingFirstTarget,
            /// <summary>
            /// Starting a target due to a depends on or on error relationship
            /// </summary>
            StartingBlockingTarget,
            /// <summary>
            /// The target which is needed is already in progress due to another request, so wait for a result
            /// </summary>
            WaitingForTarget,
            /// <summary>
            /// Currently in progress of building a needed target
            /// </summary>
            BuildingCurrentTarget,
            /// <summary>
            /// Cycle is detected and is caused by this request
            /// </summary>
            CycleDetected,
            /// <summary>
            /// There is an exception thrown during the execution of this request
            /// </summary>
            ExceptionThrown,
            /// <summary>
            /// All needed target have been completed or an error terminating the request has occurred
            /// </summary>
            BuildComplete,
            /// <summary>
            /// The result of the request has been sent back to the requesting party
            /// </summary>
            RequestFilled
        }
        #endregion

        #region Data
        // Stack of targets which need to be completed before the in progress target can continue
        Stack<string> requiredTargets;
        // BuildEventContext for the build context
        BuildEventContext projectBuildEventContext;
        // Index of the currently in progress target
        int indexOfTargetInProgress;
        // List of targets that need to be completed in order to complete the context
        // UNDONE should do the right thing and fully unescape before generating this list
        ArrayList targetNamesToBuild;
        // Build request that caused the context to come into existance (either from the host or generated)
        BuildRequest buildRequest;
        // Current state of the context
        BuildContextState buildContextState;
        #endregion
    }
}
