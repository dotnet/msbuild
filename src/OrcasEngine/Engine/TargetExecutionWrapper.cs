// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Diagnostics;   // for the debugger display attribute
using System.Collections;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is responsible for executing the target. Target only executs once within a project,
    /// so this class comes into existance for the execution and is thrown away once the execution is
    /// complete. It wraps all of the data and methods needed to execute a target. The execution
    /// is done via state machine with three primary states - BuildingDependencies, RunningTasks,
    /// BuildingErrorClause. This states map to the primary actions that are performed during target
    /// execution. The execution is blocking in single threaded mode and is iterative in multi-threaded
    /// mode.
    /// </summary>
    [DebuggerDisplay("Target (Name = { Name }, State = { inProgressBuildState })")]
    internal class TargetExecutionWrapper
    {
        #region Constructors
        internal TargetExecutionWrapper
        (
            Target targetClass,
            ArrayList taskElementList,
            List<string> targetParameters,
            XmlElement targetElement,
            Expander expander,
            BuildEventContext targetBuildEventContext
        )
        {
            // Initialize the data about the target XML that has been calculated in the target class
            this.targetClass   = targetClass;
            this.parentEngine  = targetClass.ParentEngine;
            this.parentProject = targetClass.ParentProject;
            this.targetElement   = targetElement;
            this.taskElementList = taskElementList;
            this.targetParameters = targetParameters;
            this.targetBuildEventContext = targetBuildEventContext;

            // Expand the list of depends on targets
            dependsOnTargetNames = expander.ExpandAllIntoStringList(targetClass.DependsOnTargets, targetClass.DependsOnTargetsAttribute);

            // Starting to build the target
            inProgressBuildState = InProgressBuildState.StartingBuild;
            // No messages have been logged
            loggedTargetStart = false;
        }
        #endregion

        #region Data
        // Local cache of data from the target being executed
        private Target targetClass;
        private Engine parentEngine;
        private Project parentProject;
        private XmlElement targetElement;
        private ArrayList taskElementList;
        private List<string> targetParameters;
        private ProjectBuildState initiatingBuildContext;
        private BuildEventContext targetBuildEventContext;

        // Current state of the execution
        private InProgressBuildState inProgressBuildState;
        private bool overallSuccess;
        // the outputs of the target as BuildItems (if it builds successfully)
        private List<BuildItem> targetOutputItems;
        private List<ProjectBuildState> waitingTargets;

        // Names of the dependent targets
        private List<string> dependsOnTargetNames;
        private int currentDependentTarget;
        // Names of the error targets (lazily initialized on error)
        private List<string> onErrorTargets;
        private int currentErrorTarget;

        // Array of buckets and the index of current bucket
        private ArrayList buckets;
        private int currentBucket;

        private bool haveRunANonIntrinsicTask = false;

        // Lookup containing project content used to 
        // initialize the target batches
        private Lookup projectContent;
        private LookupEntry placeholderForClonedProjectContent;

        // State for execution within a particular bucket
        private DependencyAnalysisResult howToBuild;
        private Lookup lookupForInference;
        private Lookup lookupForExecution;
        private string projectFileOfTaskNode;
        private int currentTask;
        private int skippedNodeCount;
        private bool targetBuildSuccessful;
        private bool exitBatchDueToError;
        private bool loggedTargetStart;
        #endregion

        #region Properties
        internal ProjectBuildState InitiatingBuildContext
        {
            get
            {
                return this.initiatingBuildContext;
            }
        }

        internal bool BuildingRequiredTargets
        {
            get
            {
                return (inProgressBuildState == InProgressBuildState.BuildingDependencies ||
                        inProgressBuildState == InProgressBuildState.BuildingErrorClause);
            }
        }

        #endregion

        #region Methods
        internal void ContinueBuild
        (
            ProjectBuildState buildContext, TaskExecutionContext taskExecutionContext
        )
        {
            // Verify that the target is in progress
            ErrorUtilities.VerifyThrow(inProgressBuildState != InProgressBuildState.NotInProgress, "Not in progress");

            bool exitedDueToError = true;

            try
            {
                // In the single threaded mode we want to avoid looping all the way back to the 
                // engine because there is no need for to be interruptable to address
                // other build requests. Instead we loop inside this function untill the target is 
                // fully built.
                do
                {
                    // Transition the state machine appropriatly
                    if (inProgressBuildState == InProgressBuildState.RunningTasks)
                    {
                        ContinueRunningTasks(buildContext, taskExecutionContext, false);
                    }
                    else if (inProgressBuildState == InProgressBuildState.BuildingDependencies)
                    {
                        ContinueBuildingDependencies(buildContext);
                    }
                    else if (inProgressBuildState == InProgressBuildState.StartingBuild)
                    {
                        initiatingBuildContext = buildContext;
                        inProgressBuildState = InProgressBuildState.BuildingDependencies;
                        currentDependentTarget = 0;
                        ExecuteDependentTarget(buildContext);
                    }
                    else if (inProgressBuildState == InProgressBuildState.BuildingErrorClause)
                    {
                        ContinueBuildingErrorClause(buildContext);
                    }

                    // In the single threaded mode we need to pull up the outputs of the previous 
                    // step
                    if (parentEngine.Router.SingleThreadedMode &&
                        inProgressBuildState == InProgressBuildState.RunningTasks)
                    {
                        taskExecutionContext = parentEngine.GetTaskOutputUpdates();
                    }
                } while (parentEngine.Router.SingleThreadedMode && inProgressBuildState == InProgressBuildState.RunningTasks);

                // Indicate that we exited successfully
                exitedDueToError = false;
            }
            finally
            {
                if (exitedDueToError)
                {
                    inProgressBuildState = InProgressBuildState.NotInProgress;
                    NotifyBuildCompletion(Target.BuildState.CompletedUnsuccessfully, buildContext);
                }
            }
        }

        /// <summary>
        /// Mark the target data structures and notify waiting targets since the target has completed
        /// </summary>
        internal void NotifyBuildCompletion
        (
            Target.BuildState stateOfBuild,
            ProjectBuildState errorContext
        )
        {
            targetClass.UpdateTargetStateOnBuildCompletion(stateOfBuild, targetOutputItems);

            if (initiatingBuildContext.NameOfBlockingTarget == null)
            {
                initiatingBuildContext.BuildRequest.ResultByTarget[targetClass.Name] = stateOfBuild;
            }

            if (!parentEngine.Router.SingleThreadedMode)
            {
                // Notify targets that have been waiting on the execution
                NotifyWaitingTargets(errorContext);
            }
        }

        #region Methods for building dependencies ( InProgressBuildState.BuildingDependencies )

        private void ContinueBuildingDependencies (ProjectBuildState buildContext)
        {
            // Verify that the target is in the right state
            ErrorUtilities.VerifyThrow(inProgressBuildState == InProgressBuildState.BuildingDependencies, "Wrong state");
            // Check if all dependent targets have been evaluated
            ErrorUtilities.VerifyThrow(currentDependentTarget < dependsOnTargetNames.Count, "No dependent targets left");

            // Verify that the target we were waiting on has completed building
            string nameDependentTarget = dependsOnTargetNames[currentDependentTarget];

            ErrorUtilities.VerifyThrow(
                parentProject.Targets[nameDependentTarget].TargetBuildState != Target.BuildState.InProgress &&
                parentProject.Targets[nameDependentTarget].TargetBuildState != Target.BuildState.NotStarted ||
                buildContext.CurrentBuildContextState == ProjectBuildState.BuildContextState.ExceptionThrown,
                "This target should only be updated once the dependent target is completed");

            if (buildContext.CurrentBuildContextState == ProjectBuildState.BuildContextState.ExceptionThrown)
            {
                inProgressBuildState = InProgressBuildState.NotInProgress;
                // Call the parent project to update targets waiting on us
                NotifyBuildCompletion(Target.BuildState.CompletedUnsuccessfully, buildContext);
                return;
            }
            // If the dependent target failed to build we need to execute the onerrorclause (if there is one)
            // or mark this target as failed (if there is not an error clause)
            else if (parentProject.Targets[nameDependentTarget].TargetBuildState == Target.BuildState.CompletedUnsuccessfully)
            {
                // Transition the state machine into building the error clause state
                InitializeOnErrorClauseExecution();
                inProgressBuildState = InProgressBuildState.BuildingErrorClause;
                ExecuteErrorTarget(buildContext);
                return;
            }

            // Now that the previous dependent target has been build we need to move to the next dependent target if 
            // there is one
            currentDependentTarget++;

            // Execute the current target or transition to a different state if necessary
            ExecuteDependentTarget(buildContext);
        }

        private void ExecuteDependentTarget
        (
            ProjectBuildState buildContext
        )
        {
            if (currentDependentTarget < dependsOnTargetNames.Count)
            {
                // Get the Target object for the dependent target.
                string nameDependentTarget = dependsOnTargetNames[currentDependentTarget];
                Target targetToBuild = parentProject.Targets[nameDependentTarget];

                // If we couldn't find the dependent Target object, we have a problem. 
                ProjectErrorUtilities.VerifyThrowInvalidProject(targetToBuild != null, targetClass.DependsOnTargetsAttribute,
                    "TargetDoesNotExist", nameDependentTarget);

                // Update the name of the blocking target
                buildContext.AddBlockingTarget(nameDependentTarget);
            }
            else
            {
                // We completed building the dependencies so we need to start running the tasks
                dependsOnTargetNames = null;
                inProgressBuildState = InProgressBuildState.RunningTasks;
                ContinueRunningTasks(buildContext, null, true);
            }
        }
        #endregion

        #region Methods for build error targets ( InProgressBuildState.BuildingErrorClause )
        private void ContinueBuildingErrorClause (ProjectBuildState buildContext)
        {
            // Verify that the target is in the right state
            ErrorUtilities.VerifyThrow(inProgressBuildState == InProgressBuildState.BuildingErrorClause, "Wrong state");
            // Check if all dependent targets have been evaluated
            ErrorUtilities.VerifyThrow(currentErrorTarget < onErrorTargets.Count, "No error targets left");

            // Verify that the target we were waiting on has completed building
            string nameErrorTarget = onErrorTargets[currentErrorTarget];

            ErrorUtilities.VerifyThrow(
                parentProject.Targets[nameErrorTarget].TargetBuildState != Target.BuildState.InProgress &&
                parentProject.Targets[nameErrorTarget].TargetBuildState != Target.BuildState.NotStarted ||
                buildContext.CurrentBuildContextState == ProjectBuildState.BuildContextState.ExceptionThrown,
                "This target should only be updated once the error target is completed");

            if (buildContext.CurrentBuildContextState == ProjectBuildState.BuildContextState.ExceptionThrown)
            {
                inProgressBuildState = InProgressBuildState.NotInProgress;
                // Call the parent project to update targets waiting on us
                NotifyBuildCompletion(Target.BuildState.CompletedUnsuccessfully, buildContext);
                return;
            }

            // We don't care if the target has completed successfully, we simply move on to the next one
            currentErrorTarget++;

            ExecuteErrorTarget(buildContext);
        }

        private void ExecuteErrorTarget
        (
            ProjectBuildState buildContext
        )
        {
            if (onErrorTargets != null && currentErrorTarget < onErrorTargets.Count)
            {
                // Get the Target object for the dependent target.
                string nameErrorTarget = onErrorTargets[currentErrorTarget];
                Target targetToBuild = parentProject.Targets[nameErrorTarget];

                // If we couldn't find the on error Target object, we have a problem. 
                ProjectErrorUtilities.VerifyThrowInvalidProject(targetToBuild != null, targetElement,
                    "TargetDoesNotExist", nameErrorTarget);

                // Update the name of the blocking target
                buildContext.AddBlockingTarget(nameErrorTarget);
            }
            else
            {
                // We completed building the error targets so this target is now failed and we have no more work to do
                onErrorTargets = null;
                inProgressBuildState = InProgressBuildState.NotInProgress;
                // Call the parent project to update targets waiting on us
                NotifyBuildCompletion(Target.BuildState.CompletedUnsuccessfully, null);
            }
        }

        /// <summary>
        /// Creates a list of targets to execute for the OnErrorClause
        /// </summary>
        private void InitializeOnErrorClauseExecution()
        {
            // Give default values;
            currentErrorTarget = 0;
            onErrorTargets = null;

            // Loop through each of the child nodes of the <target> element.
            List<XmlElement> childElements = ProjectXmlUtilities.GetValidChildElements(targetElement);

            foreach (XmlElement childElement in childElements)
            {
                switch (childElement.Name)
                {
                    case XMakeElements.onError:
                        ProjectXmlUtilities.VerifyThrowProjectNoChildElements(childElement);

                        XmlAttribute condition = null;
                        XmlAttribute executeTargets = null;

                        foreach (XmlAttribute onErrorAttribute in childElement.Attributes)
                        {
                            switch (onErrorAttribute.Name)
                            {
                                case XMakeAttributes.condition:
                                    condition = childElement.Attributes[XMakeAttributes.condition];
                                    break;
                                case XMakeAttributes.executeTargets:
                                    executeTargets = childElement.Attributes[XMakeAttributes.executeTargets];
                                    break;
                                default:
                                    ProjectXmlUtilities.ThrowProjectInvalidAttribute(onErrorAttribute);
                                    break;
                            }
                        }

                        ProjectErrorUtilities.VerifyThrowInvalidProject(executeTargets != null, childElement, "MissingRequiredAttribute", XMakeAttributes.executeTargets, XMakeElements.onError);

                        Expander expander = new Expander(this.parentProject.evaluatedProperties, this.parentProject.evaluatedItemsByName);

                        bool runErrorTargets = true;
                        if (condition != null)
                        {
                            if
                            (
                                !Utilities.EvaluateCondition
                                (
                                    condition.InnerText, condition, expander,
                                    null, ParserOptions.AllowProperties | ParserOptions.AllowItemLists,
                                    parentEngine.LoggingServices, targetBuildEventContext
                                )
                            )
                            {
                                runErrorTargets = false;
                            }
                        }

                        if (runErrorTargets)
                        {
                            if (onErrorTargets == null)
                            {
                                onErrorTargets = expander.ExpandAllIntoStringList(executeTargets.InnerText, executeTargets);
                            }
                            else
                            {
                                onErrorTargets.AddRange(expander.ExpandAllIntoStringList(executeTargets.InnerText, executeTargets));
                            }
                        }
                        break;

                    default:
                        // Ignore
                        break;
                }
            }
        }

        #endregion

        #region Methods for running tasks ( InProgressBuildState.RunningTasks )

        private void ContinueRunningTasks
        (
            ProjectBuildState buildContext, TaskExecutionContext taskExecutionContext,
            bool startingFirstTask
        )
        {
            bool exitDueToError = true;
            try
            {
                // If this is the first task - initialize for running it 
                if (startingFirstTask)
                {
                    InitializeForRunningTargetBatches();
                }

                // If run a task then process its outputs
                if (currentTask != targetElement.ChildNodes.Count && !startingFirstTask)
                {
                    ProcessTaskOutputs(taskExecutionContext);
                }

                // Check if we processed the last node in a batch or terminated the batch due to error
                if (currentTask == targetElement.ChildNodes.Count || exitBatchDueToError)
                {
                    FinishRunningSingleTargetBatch();

                    // On failure transition into unsuccessful state
                    if (!targetBuildSuccessful)
                    {
                        overallSuccess = false;
                        FinishRunningTargetBatches(buildContext);
                        // Transition the state machine into building the error clause state
                        InitializeOnErrorClauseExecution();
                        inProgressBuildState = InProgressBuildState.BuildingErrorClause;
                        ExecuteErrorTarget(buildContext);
                        exitDueToError = false;
                        return;
                    }

                    //Check if this was the last bucket 
                    if (currentBucket == buckets.Count)
                    {
                        FinishRunningTargetBatches(buildContext);
                        inProgressBuildState = InProgressBuildState.NotInProgress;
                        // Notify targets that are waiting for the results
                        NotifyBuildCompletion(Target.BuildState.CompletedSuccessfully, null);
                        exitDueToError = false;
                        return;
                    }

                    // Prepare the next bucket
                    InitializeForRunningSingleTargetBatch();
                }

                // Execute the current task
                ExecuteCurrentTask(buildContext);

                exitDueToError = false;
            }
            catch (InvalidProjectFileException e)
            {
                // Make sure the Invalid Project error gets logged *before* TargetFinished.  Otherwise,
                // the log is confusing.
                this.parentEngine.LoggingServices.LogInvalidProjectFileError(targetBuildEventContext, e);
                throw;
            }
            finally
            {
                if (exitDueToError && loggedTargetStart)
                {
                    // Log that the target has failed
                    parentEngine.LoggingServices.LogTargetFinished(
                        targetBuildEventContext,
                        targetClass.Name,
                        this.parentProject.FullFileName,
                        targetClass.ProjectFileOfTargetElement,
                        false);
                }
            }
        }

        private void InitializeForRunningTargetBatches()
        {
            // Make sure the <target> node has been given to us.
            ErrorUtilities.VerifyThrow(targetElement != null,
                "Need an XML node representing the <target> element.");

            // Make sure this really is the <target> node.
            ProjectXmlUtilities.VerifyThrowElementName(targetElement, XMakeElements.target);

            overallSuccess = true;

            projectContent = new Lookup(parentProject.evaluatedItemsByName, parentProject.evaluatedItems, parentProject.evaluatedProperties, parentProject.ItemDefinitionLibrary);

            // If we need to use the task thread - ie, we encounter a non-intrinsic task - we will need to make sure
            // the task thread only sees clones of the project items and properties. We insert a scope to allow us to
            // do that later. See comment in InitializeForRunningFirstNonIntrinsicTask()
            placeholderForClonedProjectContent = projectContent.EnterScope();
            buckets = BatchingEngine.PrepareBatchingBuckets(targetElement, targetParameters, projectContent);

            currentBucket = 0;

            // Initialize the first bucket
            InitializeForRunningSingleTargetBatch();
        }

        private void InitializeForRunningSingleTargetBatch()
        {
            // Verify that the target is in the right state
            ErrorUtilities.VerifyThrow(inProgressBuildState == InProgressBuildState.RunningTasks, "Wrong state");
            // Check if the current task number is valid
            ErrorUtilities.VerifyThrow(currentBucket < buckets.Count, "No buckets left");

            Hashtable changedTargetInputs = null;
            Hashtable upToDateTargetInputs = null;
            howToBuild = DependencyAnalysisResult.FullBuild;
            ItemBucket bucket = (ItemBucket)buckets[currentBucket];

            // For the first batch of a target use the targets original targetID. for each batch after the first one use a uniqueId to identity the target in the batch
            if (currentBucket != 0)
            {
                targetBuildEventContext = new BuildEventContext(targetBuildEventContext.NodeId, parentEngine.GetNextTargetId(), targetBuildEventContext.ProjectContextId, targetBuildEventContext.TaskId);
            }

            // Flag the start of the target.
            parentEngine.LoggingServices.LogTargetStarted(
                targetBuildEventContext,
                targetClass.Name,
                this.parentProject.FullFileName,
                targetClass.ProjectFileOfTargetElement);
            loggedTargetStart = true;

            // Figure out how we should build the target
            TargetDependencyAnalyzer dependencyAnalyzer = new TargetDependencyAnalyzer(parentProject.ProjectDirectory, targetClass, parentEngine.LoggingServices, targetBuildEventContext);
            howToBuild = dependencyAnalyzer.PerformDependencyAnalysis(bucket, out changedTargetInputs, out upToDateTargetInputs);

            targetBuildSuccessful = true;
            exitBatchDueToError = false;

            // If we need to build the target - initialize the data structure for
            // running the tasks
            if ((howToBuild != DependencyAnalysisResult.SkipNoInputs) &&
                (howToBuild != DependencyAnalysisResult.SkipNoOutputs))
            {
                // Within each target batch items are divided into lookup and execution; they must be 
                // kept separate: enforce this by cloning and entering scope
                lookupForInference = bucket.Lookup;
                lookupForExecution = bucket.Lookup.Clone();

                lookupForInference.EnterScope();
                lookupForExecution.EnterScope();

                // if we're doing an incremental build, we need to effectively run the task twice -- once
                // to infer the outputs for up-to-date input items, and once to actually execute the task;
                // as a result we need separate sets of item and property collections to track changes
                if (howToBuild == DependencyAnalysisResult.IncrementalBuild)    
                {
                    // subset the relevant items to those that are up-to-date
                    foreach (DictionaryEntry upToDateTargetInputsEntry in upToDateTargetInputs)
                    {
                        lookupForInference.PopulateWithItems((string)upToDateTargetInputsEntry.Key, (BuildItemGroup)upToDateTargetInputsEntry.Value);
                    }

                    // subset the relevant items to those that have changed
                    foreach (DictionaryEntry changedTargetInputsEntry in changedTargetInputs)
                    {
                        lookupForExecution.PopulateWithItems((string)changedTargetInputsEntry.Key, (BuildItemGroup)changedTargetInputsEntry.Value);
                    }
                }

                projectFileOfTaskNode = XmlUtilities.GetXmlNodeFile(targetElement, parentProject.FullFileName);

                // count the tasks in the target
                currentTask = 0;
                skippedNodeCount = 0;
            }
            else
            {
                currentTask = targetElement.ChildNodes.Count;
            }
        }

        /// <summary>
        /// Called before the first non-intrinsic task is run by this object.
        /// </summary>
        private void InitializeForRunningFirstNonIntrinsicTask()
        {
            // We need the task thread to see cloned project content for two reasons:
            // (1) clone items because BuildItemGroups storage is a List, which
            // is not safe to read from (task thread) and write to (engine thread) concurrently.
            // Project properties are in a virtual property group which stores its properties in a hashtable, however
            // (2) we must clone both items and properties so that project items and properties modified by a
            // target called by this target are not visible to this target (Whidbey behavior)
            //
            // So, we populate the empty scope we inserted earlier with clones of the items and properties, and we
            // mark it so that lookups truncate their walk at this scope, and don't reach the real items and properties below.
            // Later, back on the engine thread, we'll leave this scope and the task changes will go into the project.
            Hashtable items = new Hashtable(parentProject.evaluatedItemsByName.Count, StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in parentProject.evaluatedItemsByName)
            {
                BuildItemGroup group = (BuildItemGroup)entry.Value;
                BuildItemGroup clonedGroup = group.ShallowClone();
                items.Add(entry.Key, clonedGroup);
            }
            BuildPropertyGroup properties = parentProject.evaluatedProperties.ShallowClone();

            placeholderForClonedProjectContent.Items = items;
            placeholderForClonedProjectContent.Properties = properties;
            placeholderForClonedProjectContent.TruncateLookupsAtThisScope = true;
        }

        /// <summary>
        /// Executes all tasks in the target linearly from beginning to end, for one batch of the target.
        /// </summary>
        private void ExecuteCurrentTask(ProjectBuildState buildContext)
        {
            // Check if this is an empty target
            if (currentTask == targetElement.ChildNodes.Count)
            {
                // This is an empty target so we should transition into completed state
                ContinueRunningTasks(buildContext, null, false);
                return;
            }

            // Get the current child nodes of the <Target> element.
            XmlNode targetChildNode = targetElement.ChildNodes[currentTask];

            // Handle XML comments under the <target> node (just ignore them) and
            // also skip OnError tags because they are processed separately and later.
            // Also evaluate any intrinsic tasks immediately and continue.
            while ((targetChildNode.NodeType == XmlNodeType.Comment) ||
                   (targetChildNode.NodeType == XmlNodeType.Whitespace) ||
                   (targetChildNode.Name == XMakeElements.onError) ||
                   (IntrinsicTask.IsIntrinsicTaskName(targetChildNode.Name)))
            {
                if (IntrinsicTask.IsIntrinsicTaskName(targetChildNode.Name))
                {
                    ExecuteIntrinsicTask((XmlElement)targetChildNode);
                }
                else
                {
                    skippedNodeCount++;
                }

                currentTask++;

                // Check if this was the last task in the target
                if (currentTask == targetElement.ChildNodes.Count)
                {
                    // Transition into appropriate state
                    ContinueRunningTasks(buildContext, null, false);
                    return;
                }

                targetChildNode = targetElement.ChildNodes[currentTask];
            }

            // Any child node other than a task element or <OnError> is not supported.
            ProjectXmlUtilities.VerifyThrowProjectXmlElementChild(targetChildNode);

            // Make <ItemDefinitionGroup> illegal inside targets, so we can possibly allow it in future.
            ProjectErrorUtilities.VerifyThrowInvalidProject(!String.Equals(targetChildNode.Name, XMakeElements.itemDefinitionGroup, StringComparison.Ordinal),
                targetElement, "ItemDefinitionGroupNotLegalInsideTarget", targetChildNode.Name, XMakeElements.target);

            ErrorUtilities.VerifyThrow(taskElementList.Count > (currentTask - skippedNodeCount),
                "The TaskElementCollection in this target doesn't have the same number of BuildTask objects as the number of actual task elements.");
            // Send the task for execution 
            SubmitNonIntrinsicTask(
                (XmlElement)targetChildNode,
                ((BuildTask)taskElementList[(currentTask - skippedNodeCount)]).HostObject,
                buildContext);

            return;
        }

        private TaskExecutionMode DetermineExecutionMode()
        {
            TaskExecutionMode executionMode;
            if ((howToBuild == DependencyAnalysisResult.SkipUpToDate) ||
                (howToBuild == DependencyAnalysisResult.IncrementalBuild))
            {
                executionMode = TaskExecutionMode.InferOutputsOnly;
            }
            else
            {
                executionMode = TaskExecutionMode.ExecuteTaskAndGatherOutputs;
            }

            // execute the task using the items that need to be (re)built
            if ((howToBuild == DependencyAnalysisResult.FullBuild) ||
                (howToBuild == DependencyAnalysisResult.IncrementalBuild))
            {
                executionMode = executionMode | TaskExecutionMode.ExecuteTaskAndGatherOutputs;
            }
            return executionMode;
        }

        /// <summary>
        /// Create a new build event context for tasks
        /// </summary>
        private BuildEventContext PrepareBuildEventContext(bool setInvalidTaskId)
        {
            BuildEventContext buildEventContext = new BuildEventContext
                                            (
                                                targetBuildEventContext.NodeId,
                                                targetBuildEventContext.TargetId,
                                                targetBuildEventContext.ProjectContextId,
                                                setInvalidTaskId ? BuildEventContext.InvalidTaskId : parentEngine.GetNextTaskId()
                                            );

            return buildEventContext;
        }

        private void ExecuteIntrinsicTask(XmlElement taskNode)
        {
            // Intrinsic tasks should have their messages logged in the context of the target as they will not have task started or finished events so use an invalid taskID
            BuildEventContext buildEventContext = PrepareBuildEventContext(true);
            TaskExecutionMode executionMode = DetermineExecutionMode();

            IntrinsicTask task = new IntrinsicTask(taskNode, 
                                                   parentEngine.LoggingServices,
                                                   buildEventContext,
                                                   parentProject.ProjectDirectory,
                                                   parentProject.ItemDefinitionLibrary);
            if ((executionMode & TaskExecutionMode.InferOutputsOnly) != TaskExecutionMode.Invalid)
            {
                task.ExecuteTask(lookupForInference);
            }
            if ((executionMode & TaskExecutionMode.ExecuteTaskAndGatherOutputs) != TaskExecutionMode.Invalid)
            {
                task.ExecuteTask(lookupForExecution);
            }
        }

        /// <summary>
        /// Create a TaskExecutionState structure which contains all the information necessary
        /// to execute the task and send this information over to the TEM for task execution
        /// </summary>
        internal void SubmitNonIntrinsicTask
        (
            XmlElement taskNode,
            ITaskHost hostObject,
            ProjectBuildState buildContext
        )
        {
            if (!haveRunANonIntrinsicTask)
            {
                InitializeForRunningFirstNonIntrinsicTask();
                haveRunANonIntrinsicTask = true;
            }

            TaskExecutionMode executionMode = DetermineExecutionMode();

            // A TaskExecutionMode of ExecuteTaskAndGatherOutputs should have its messages logged in the context of the task and therefore should have a valid taskID
            // A TaskExecutionMode of InferOutputs or Invalid should have its messages logged in the context of the target and therefore should have an invalid taskID
            BuildEventContext buildEventContext = PrepareBuildEventContext(executionMode == TaskExecutionMode.ExecuteTaskAndGatherOutputs ? false: true);

            // Create the task execution context
            int handleId = parentEngine.EngineCallback.CreateTaskContext(parentProject, targetClass, buildContext,
                                                                         taskNode, EngineCallback.inProcNode, buildEventContext);

            // Create the task execution state
            TaskExecutionState taskState =
                new TaskExecutionState
                    (
                        executionMode,
                        lookupForInference,
                        lookupForExecution,
                        taskNode,
                        hostObject,
                        projectFileOfTaskNode,
                        parentProject.FullFileName,
                        parentProject.ProjectDirectory,
                        handleId,
                        buildEventContext
                    );

            // Send the request for task execution to the node
            parentEngine.NodeManager.ExecuteTask(taskState);
        }

        private void ProcessTaskOutputs(TaskExecutionContext executionContext)
        {
            // Get the success or failure
            if (targetBuildSuccessful)
            {
                if (!executionContext.TaskExecutedSuccessfully)
                {
                    targetBuildSuccessful = false;
                    // Check if the task threw an unhandled exception during its execution
                    if (executionContext.ThrownException != null)
                    {
                        // The stack trace for remote task InvalidProjectFileException can be ignored
                        // since it is not recorded and the exception will be caught inside the project
                        // class
                        if (executionContext.ThrownException is InvalidProjectFileException)
                        {
                            throw executionContext.ThrownException;
                        }
                        else
                        {
                            // The error occured outside of the user code (it may still be caused
                            // by bad user input), the build should be terminated. The exception
                            // will be logged as a fatal build error in engine. The exceptions caused
                            // by user code are converted into LogFatalTaskError messages by the TaskEngine
                            RemoteErrorException.Throw(executionContext.ThrownException, 
                                                       targetBuildEventContext, 
                                                       "RemoteErrorDuringTaskExecution",
                                                       parentProject.FullFileName, 
                                                       targetClass.Name);
                        }
                    }
                    // We need to disable the execution of the task if it was previously enabled,
                    // and if were only doing execution we can stop processing at the point the
                    // error occurred. If the task fails (which implies that ContinueOnError != 'true'), then do 
                    // not execute the remaining tasks because they may depend on the completion 
                    // of this task.
                    ErrorUtilities.VerifyThrow(howToBuild == DependencyAnalysisResult.FullBuild ||
                                                howToBuild == DependencyAnalysisResult.IncrementalBuild,
                                                "We can only see a failure for an execution stage");
                    if (howToBuild != DependencyAnalysisResult.FullBuild)
                        howToBuild = DependencyAnalysisResult.SkipUpToDate;
                    else
                        exitBatchDueToError = true;
                }
            }

            currentTask++;
        }

        private void FinishRunningSingleTargetBatch
        (
        )
        {
            if ((howToBuild != DependencyAnalysisResult.SkipNoInputs) &&
                (howToBuild != DependencyAnalysisResult.SkipNoOutputs))
            {
                // publish all output items and properties to the target scope;
                // inference and execution are now combined
                // roll up the outputs in the right order -- inferred before generated
                // NOTE: this order is important because when we infer outputs, we are trying
                // to produce the same results as would be produced from a full build; as such
                // if we're doing both the infer and execute steps, we want the outputs from
                // the execute step to override the outputs of the infer step -- this models
                // the full build scenario more correctly than if the steps were reversed
                lookupForInference.LeaveScope();
                lookupForExecution.LeaveScope();
            }

            // Flag the completion of the target.
            parentEngine.LoggingServices.LogTargetFinished(
                targetBuildEventContext,
                targetClass.Name,
                this.parentProject.FullFileName,
                targetClass.ProjectFileOfTargetElement,
                (overallSuccess && targetBuildSuccessful));
            loggedTargetStart = false;

            // Get the next bucket
            currentBucket++;
        }

        private void FinishRunningTargetBatches(ProjectBuildState buildContext)
        {
            // first, publish all task outputs to the project level
            foreach (ItemBucket bucket in buckets)
            {
                bucket.Lookup.LeaveScope();
            }         

            // and also leave the extra scope we created with the cloned project items
            projectContent.LeaveScope();

            // if all batches of the target build successfully
            if (overallSuccess)
            {
                // then, gather the target outputs
                // NOTE: it is possible that the target outputs computed at this point will be different from the target outputs
                // used for dependency analysis, but we assume that's what the user intended
                GatherTargetOutputs();

                // Only contexts which are generated from an MSBuild task could need 
                // the outputs of the target, such contexts have a non-null evaluation
                // request
                if (buildContext.BuildRequest.OutputsByTarget != null &&
                    buildContext.NameOfBlockingTarget == null)
                {
                    ErrorUtilities.VerifyThrow(
                        String.Compare(EscapingUtilities.UnescapeAll(buildContext.NameOfTargetInProgress), targetClass.Name, StringComparison.OrdinalIgnoreCase) == 0,
                        "The name of the target in progress is inconsistent with the target being built");

                    ErrorUtilities.VerifyThrow(targetOutputItems != null,
                        "If the target built successfully, we must have its outputs.");

                    buildContext.BuildRequest.OutputsByTarget[targetClass.Name] = targetOutputItems.ToArray();
                }
            }
        }

        /// <summary>
        /// Gathers the target's outputs, per its output specification (if any).
        /// </summary>
        /// <remarks>
        /// This method computes the target's outputs using the items currently available in the project; depending on when this
        /// method is called, it may compute a different set of outputs -- as a result, we only want to gather the target's
        /// outputs once, and cache them until the target's build state is reset.
        /// </remarks>
        private void GatherTargetOutputs()
        {
            // allocate storage for target outputs -- if the target has no outputs this list will remain empty
            targetOutputItems = new List<BuildItem>();

            XmlAttribute targetOutputsAttribute = targetElement.Attributes[XMakeAttributes.outputs];

            // Hack to help the 3.5 engine at least pretend to still be able to build on top of 
            // the 4.0 targets.  In cases where there is no Outputs attribute, just a Returns attribute, 
            // we can approximate the correct behaviour by making the Returns attribute our "outputs" attribute. 
            if (targetOutputsAttribute == null)
            {
                targetOutputsAttribute = targetElement.Attributes[XMakeAttributes.returns];
            }

            if (targetOutputsAttribute != null)
            {
                // NOTE: we need to gather the outputs in batches, because the output specification may reference item metadata
                foreach (ItemBucket bucket in BatchingEngine.PrepareBatchingBuckets(targetElement, targetParameters, new Lookup(parentProject.evaluatedItemsByName, parentProject.evaluatedProperties, parentProject.ItemDefinitionLibrary)))
                {
                    targetOutputItems.AddRange(bucket.Expander.ExpandAllIntoBuildItems(targetOutputsAttribute.Value, targetOutputsAttribute));
                }
            }
        }

        #endregion

        #region Methods for managing the wait states

        /// <summary>
        /// Add a build context that should get a result of the target once it is finished
        /// </summary>
        internal void AddWaitingBuildContext(ProjectBuildState buildContext)
        {
            if (waitingTargets == null)
            {
                waitingTargets = new List<ProjectBuildState>();
            }
            parentEngine.Scheduler.NotifyOfBlockedRequest(buildContext.BuildRequest);
            waitingTargets.Add(buildContext);
        }

        /// <summary>
        /// Get the list of build contexts currently waiting on the target
        /// </summary>
        internal List<ProjectBuildState> GetWaitingBuildContexts()
        {
            return waitingTargets;
        }

        /// <summary>
        /// Iterate over the contexts waiting for the target - triggering updates for each of them since the target 
        /// is complete
        /// </summary>
        internal void NotifyWaitingTargets(ProjectBuildState errorContext)
        {
            // If there was a failure (either unhandled exception or a cycle) the stack will
            // not unwind properly (i.e. via ContinueBuild call). Therefore the initiating request
            // must be notified the target completed if the error occurred in another context
            if (errorContext != null)
            {
                AddWaitingBuildContext(initiatingBuildContext);
            }

            // Notify the target within the same project that are waiting for current target
            // These targets are in the process of either building dependencies or error targets
            // or part of a sequential build context
            while (waitingTargets != null && waitingTargets.Count != 0)
            {
                //Grab the first context
                ProjectBuildState buildContext = waitingTargets[0];
                waitingTargets.RemoveAt(0);

                //Don't report any messages within the context in which the error occured. That context
                //is addressed as the base of the stack 
                if (buildContext == errorContext ||
                    buildContext.CurrentBuildContextState == ProjectBuildState.BuildContextState.RequestFilled)
                {
                    continue;
                }
                
                parentEngine.Scheduler.NotifyOfUnblockedRequest(buildContext.BuildRequest);

                ErrorUtilities.VerifyThrow(
                    buildContext.CurrentBuildContextState == ProjectBuildState.BuildContextState.WaitingForTarget ||
                    buildContext == initiatingBuildContext,
                    "This context should be waiting for a target to be evaluated");

                if (buildContext.NameOfBlockingTarget == null)
                {
                    ErrorUtilities.VerifyThrow(
                        String.Compare(EscapingUtilities.UnescapeAll(buildContext.NameOfTargetInProgress), targetClass.Name, StringComparison.OrdinalIgnoreCase) == 0,
                        "The name of the target in progress is inconsistent with the target being built");

                    // This target was part of a sequential request so we need to notify the parent project
                    // to start building the next target in the sequence
                    if (Engine.debugMode)
                    {
                        Console.WriteLine("Finished " + buildContext.BuildRequest.ProjectFileName + ":" + targetClass.Name + " for node:" +
                                            buildContext.BuildRequest.NodeIndex + " HandleId " + buildContext.BuildRequest.HandleId);
                    }
                }
                else
                {
                    // The target on the waiting list must be waiting for this target to complete due to
                    // a dependent or onerror relationship between targets
                    ErrorUtilities.VerifyThrow(
                        String.Compare(buildContext.NameOfBlockingTarget, targetClass.Name, StringComparison.OrdinalIgnoreCase) == 0,
                        "This target should only be updated once the dependent target is completed");

                    if (Engine.debugMode)
                    {
                        Console.WriteLine("Finished " + targetClass.Name + " notifying " + EscapingUtilities.UnescapeAll(buildContext.NameOfTargetInProgress));
                    }
                }

                // Post a dummy context to the queue to cause the target to run in this context
                TaskExecutionContext taskExecutionContext = 
                    new TaskExecutionContext(parentProject, null, null, buildContext, 
                                             EngineCallback.invalidEngineHandle, EngineCallback.inProcNode, null);
                parentEngine.PostTaskOutputUpdates(taskExecutionContext);
            }
        }
        #endregion
        #endregion

        #region Enums
        internal enum InProgressBuildState
        {
            // This target is not in the process of building
            NotInProgress,

            // The target is being started but no work has been done
            StartingBuild,

            // This target is in process of building dependencies
            BuildingDependencies,

            // This target is in process of building the error clause
            BuildingErrorClause,

            // This target is current running the tasks for each bucket
            RunningTasks
        }
        #endregion
    }
}
