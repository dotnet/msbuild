// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is responsible for representing the task execution subsystem to the engine. This
    /// class can be instantiated in a different appdomain from the engine and doesn't share
    /// any pointers/data with the engine(except for the argument to the functions).
    /// </summary>
    internal class TaskExecutionModule
    {
        #region Constructors

        /// <summary>
        /// The TaskExecutionModule is a the external view into a subsystem responsible for executing user
        /// tasks. The subsystem consists of TaskWorkerThread, TaskEngine, TaskExecutionState and EngineProxy.
        /// The engine thread passes the TaskExecutionState to the TEM which after the task finishes passes the
        /// results back via the engineCallback.
        /// </summary>
        internal TaskExecutionModule
        (
            EngineCallback engineCallback, 
            TaskExecutionModuleMode moduleMode,
            bool profileExecution
        )
        {
            this.engineCallback = engineCallback;
            this.moduleMode = moduleMode;
            // By default start in breadthFirst traversal. This is done to gather enough work at the start of the build to get all the nodes at least working on something.
            this.breadthFirstTraversal = true;
            this.profileExecution = profileExecution;
            this.totalTaskTime = 0;
            // Get the node the TEM is running on, this is so the parent engine knows which node is requesting a traversal strategy change.
            nodeId = engineCallback.GetParentEngine().NodeId;

            SetBatchRequestSize();

            // In singleproc mode the task execution module executes tasks on the engine thread. In multi proc mode a new thread is 
            // created so the TEM can submit tasks to a worker queue which will run the tasks on a new thread.
            if (moduleMode != TaskExecutionModuleMode.SingleProcMode)
            {
                this.isRunningMultipleNodes = true;
                this.activeThreadCount = 0;
                this.overallThreadCount = 0;
                this.threadActiveCountEvent  = new ManualResetEvent(false);
                this.threadOverallCountEvent = new ManualResetEvent(false);
                this.lastTaskActivity = 0;

                // Create a worker thread and make it the active node thread
                workerThread = new TaskWorkerThread(this, profileExecution);
                workerThread.ActivateThread();
            }
            else
            {
                this.isRunningMultipleNodes = false;
            }
        }

        /// <summary>
        /// Sets the requestBatch size based on an environment variable set by the user.
        /// </summary>
        private void SetBatchRequestSize()
        {
            // The RequestBatchSize is how many buildrequests will be sent to the parent engine at once when the system
            // is running in multiproc mode. The idea of only sending a certain ammount of build requests was implemented
            // due to the parent engine being flooded with build requests if the tree being built is very broad.
            string requestBatchSizeEnvironmentVariable = Environment.GetEnvironmentVariable("MSBUILDREQUESTBATCHSIZE");
            if (!String.IsNullOrEmpty(requestBatchSizeEnvironmentVariable))
            {
                if (!int.TryParse(requestBatchSizeEnvironmentVariable, out batchRequestSize) || batchRequestSize < 1)
                {
                    // If an invalid RequestBatchSize is passed in set the batchRequestSize back to the default and log a warning.
                    batchRequestSize = defaultBatchRequestSize;
                    BuildEventContext buildEventContext = new BuildEventContext(
                                           nodeId,
                                           BuildEventContext.InvalidTargetId,
                                           BuildEventContext.InvalidProjectContextId,
                                           BuildEventContext.InvalidTaskId
                                           );

                    engineCallback.GetParentEngine().LoggingServices.LogWarning(buildEventContext, new BuildEventFileInfo(/* there is truly no file associated with this warning */ String.Empty), "BatchRequestSizeOutOfRange", requestBatchSizeEnvironmentVariable);
                }
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// This property allows a task to query whether or not the system is running in single process mode or multi process mode.
        /// Single process mode (IsRunningMultipleNodes = false) is where the engine is initialized with the number of cpus = 1 and the engine is not a child engine.
        /// The engine is in multi process mode (IsRunningMultipleNodes = true) when the engine is initialized with a number of cpus > 1 or the engine is a child engine.
        /// </summary>
        internal bool IsRunningMultipleNodes
        {
            get
            {
                return isRunningMultipleNodes;
            }
        }

        /// <summary>
        /// Specifies the traversal type for IBuildEngine callbacks. If true multiple build requests will be sent
        /// to the engine if false the requests will be sent one at a time.
        /// </summary>
        internal bool UseBreadthFirstTraversal
        {
            get
            {
                return breadthFirstTraversal;
            }
            set
            {
                breadthFirstTraversal = value;
            }
        }

        /// <summary>
        /// Returns true if the TEM doesn't have a thread in user code and there are no pending 
        /// workitems
        /// </summary>
        internal bool IsIdle
        {
            get
            {
                return (activeThreadCount == 0 && workerThread.WorkItemCount == 0);
            }
        }

        /// <summary>
        /// Return total time spent executing the tasks by this TEM. This value is only valid if the TEM is created with 
        /// profileExecution set to true, otherwise this value will be 0
        /// </summary>
        internal long TaskExecutionTime
        {
            get
            {
                return totalTaskTime;
            }
        }

        #endregion
        #region Method used internally inside the TEM boundary (i.e. not called from the engine)

        /// <summary>
        /// This method passes the task outputs to the engine, it is virtual for testing purposes to 
        /// create a mock TEM
        /// </summary>
        virtual internal void PostTaskOutputs
        (
            int handleId,
            bool taskExecutedSuccessfully,
            Exception thrownException,
            long executionTime
        )
        {
            totalTaskTime += executionTime;
            engineCallback.PostTaskOutputs(handleId, taskExecutedSuccessfully, thrownException, executionTime);
        }

        /// <summary>
        /// This function implements the callback via the IBuildEngine interface
        /// </summary>
        /// <returns>result of call to engine</returns>
        virtual internal bool BuildProjectFile
        (
            int handleId, 
            string[] projectFileNames, 
            string[] targetNames, 
            IDictionary[] globalPropertiesPerProject,
            IDictionary[] targetOutputsPerProject,
            EngineLoggingServices loggingServices,
            string [] toolsVersions,
            bool useResultsCache,
            bool unloadProjectsOnCompletion, 
            BuildEventContext taskContext
        )
        {
            if (projectFileNames.Length == 0)
            {
                // Nothing to do, just return success
                return true;
            }

            string currentDir = FileUtilities.GetCurrentDirectoryStaticBuffer(currentDirectoryBuffer);

            if (Engine.debugMode)
            {
                string targetName = targetNames == null ? "null" : targetNames[0];

                bool remoteNode = false;
                for (int r = 0; r < projectFileNames.Length; r++)
                {
                    string fullProjectName = projectFileNames[r] != null ?
                       projectFileNames[r] : "null";
                    Console.WriteLine("RemoteNode: " + remoteNode + " Project " + fullProjectName + " T:" + targetName + " NodeProdyId# " + handleId + " Time " + DateTime.Now.ToLongTimeString());
                    if (globalPropertiesPerProject[r] != null)
                    {
                        foreach (DictionaryEntry entry in globalPropertiesPerProject[r])
                        {
                            Console.WriteLine(currentDir + " :GLOBAL " + entry.Key + "=" + entry.Value.ToString());
                        }
                    }
                }
            }

            BuildRequest[] buildRequests = new BuildRequest[projectFileNames.Length];
            for (int i = 0; i < buildRequests.Length; i++)
            {
                // We need to get the full path to the project before we call back
                // into the engine which has no control over current path
                string fullProjectName = projectFileNames[i] != null ?
                    Path.GetFullPath(projectFileNames[i]) : null;

                buildRequests[i] = new BuildRequest(handleId, fullProjectName, targetNames, globalPropertiesPerProject[i],
                                                    toolsVersions[i], i, useResultsCache, unloadProjectsOnCompletion);
                ErrorUtilities.VerifyThrow(buildRequests[i].IsGeneratedRequest == true, "Should not be sending non generated requests from TEM to engine");
                buildRequests[i].ParentBuildEventContext = taskContext;
            }

            BuildResult[] buildResultsLocal = new BuildResult[projectFileNames.Length];

            if (moduleMode == TaskExecutionModuleMode.SingleProcMode)
            {
                for (int i = 0; i < projectFileNames.Length; i++)
                {
                    // If we are running in a single threaded mode we need to
                    // re-enter the main build loop on the current thread in order
                    // to build the requested project, because the main build is below
                    // us on the stack
                    engineCallback.PostBuildRequestsToHost(new BuildRequest[] { buildRequests[i] });
                    buildResultsLocal[i] = engineCallback.GetParentEngine().EngineBuildLoop(buildRequests[i]);
                    buildResultsLocal[i].ConvertToTaskItems();
                }
            }
            else
            {
                WaitForBuildResults(handleId, buildResultsLocal, buildRequests);
            }

            // Store the outputs in the hashtables provided by the caller
            bool overallResult = true;
            for (int i = 0; i < buildResultsLocal.Length; i++)
            {
                // Users of the Object Model can pass in null targetOutputs for projects they do not want outputs for
                // therefore we need to make sure that there are targetoutputs and the users want the results
                if (buildResultsLocal[i] != null)
                {
                    if (buildResultsLocal[i].OutputsByTarget != null && targetOutputsPerProject[i] != null)
                    {
                        foreach (DictionaryEntry entry in buildResultsLocal[i].OutputsByTarget)
                        {
                            targetOutputsPerProject[i].Add(entry.Key, entry.Value);
                        }
                        overallResult = overallResult && buildResultsLocal[i].EvaluationResult;
                    }
                }
                else
                {
                    // The calculation was terminated prior to receiving the result
                    overallResult = false;
                }
            }

            // We're now returning from an IBuildEngine callback;
            // set the current directory back to what the tasks expect
            if (Directory.GetCurrentDirectory() != currentDir)
            {
                Directory.SetCurrentDirectory(currentDir);
            }

            if (Engine.debugMode)
            {
                bool remoteNode = false;
                string targetName = targetNames == null ? "null" : targetNames[0];
                Console.WriteLine("RemoteNode: " + remoteNode + " T:" + targetName + " HandleId# " + handleId + " Result " + overallResult);
            }

            return overallResult;
        }
       
       /// <summary>
       /// Once the buildRequests from the EngineCallback have been created they are sent to this method which will
       /// post the build requests to the parent engine and then wait on the results to come back.
       /// This method uses either a breadthFirst or depthFirst traversal strategy when sending buildRequests to the parent engine.
       /// This method will start in breadthFirst traversal. It will continue to use this strategy until one of two events occur:
       ///     1. The parent node sents a message indicating the TEM should switch to depthFirst traversal. 
       ///     2. The number of buildRequests is larger than the batchRequestSize.
       /// In both of these cases the system will go from a breadthFirstTraversal to a depthFirst Traversal. In the second case
       /// a message will be sent to the parent engine to switch the system to depthFirst traversal as the system is starting to 
       /// be overloaded with work.
       /// In a depth first strategy the buildRequests will be sent to the parent engine one at a time and waiting for results for
       /// each buildRequest sent. In a breadthFirst traversal strategy some number of the buildrequests will be sent to the parent engine
       /// in a batch of requests. The system will then wait on the results of ALL the build requests sent before continuing
       /// to send more build requests.
       /// </summary>
       private void WaitForBuildResults(int handleId, BuildResult[] buildResultsLocal, BuildRequest[] buildRequests)
        {
            // If the traversal strategy is breadth first and the number of requests is less than the batchRequestSize
            // or if there is only 1 build request then send ALL build requests to the parent engine and wait on the results.
            if ((breadthFirstTraversal == true && buildRequests.Length < batchRequestSize) || buildRequests.Length == 1)
            {
                engineCallback.PostBuildRequestsToHost(buildRequests);
                workerThread.WaitForResults(handleId, buildResultsLocal, buildRequests);
            }
            else
            {
                int currentRequestIndex = 0; // Which build request is being processed
                int numberOfRequestsToSend = 0; // How many buildRequests are going to be sent based on the number of buildRequests remaining and the build request batch size.
                
                // Arrays that will be used to partion the buildRequests array when sending batches of builds requests at a time.
                BuildRequest[] wrapperArrayBreadthFirst = new BuildRequest[batchRequestSize];
                BuildResult[] resultsArrayBreadthFirst = new BuildResult[batchRequestSize];

                // Pre allocate these arrays as they will always be only one element in size. They are assigned to and filled when doing a depth first traversal.
                BuildRequest[] wrapperArrayDepthFirst = new BuildRequest[1];
                BuildResult[] resultsArrayDepthFirst = new BuildResult[1];

                // While there are still requests to send
                while (currentRequestIndex < buildRequests.Length)
                {
                    // If there is a breadth first traversal and there are more than batchRequestSize build requests, send the first batchRequestSize, then do the rest depth first
                    if (breadthFirstTraversal == true)
                    {
                        // Figure out how many requests to send, either the full batch size or only part of a batch
                        numberOfRequestsToSend = (buildRequests.Length - currentRequestIndex) <batchRequestSize ? (buildRequests.Length - currentRequestIndex) : batchRequestSize;

                        // Initialize the wrapper array to how many requests are going to be sent
                        if (numberOfRequestsToSend != wrapperArrayBreadthFirst.Length)
                        {
                            wrapperArrayBreadthFirst = new BuildRequest[numberOfRequestsToSend];
                            resultsArrayBreadthFirst = new BuildResult[numberOfRequestsToSend];
                        }
                        
                        // Fill the wrapper array with one batch of build requests
                        for (int i = 0; i < numberOfRequestsToSend; i++)
                        {
                            wrapperArrayBreadthFirst[i] = buildRequests[currentRequestIndex + i];
                            wrapperArrayBreadthFirst[i].RequestId = i;
                            resultsArrayBreadthFirst[i] = null;
                        }

                        engineCallback.PostBuildRequestsToHost(wrapperArrayBreadthFirst);
                        
                        // Only switch from breadth to depth if there are more thanbatchRequestSize items
                        if ((buildRequests.Length - currentRequestIndex) > batchRequestSize)
                        {
                            engineCallback.PostStatus(nodeId, new NodeStatus(false /* use depth first traversal*/), false /* don't block waiting on the send */);
                            breadthFirstTraversal = false;
                        }
                        
                        workerThread.WaitForResults(handleId, resultsArrayBreadthFirst, wrapperArrayBreadthFirst);
                        Array.Copy(resultsArrayBreadthFirst, 0, buildResultsLocal, currentRequestIndex, numberOfRequestsToSend);
                        currentRequestIndex += numberOfRequestsToSend;
                    }

                    // Proceed with depth first traversal
                    while ((currentRequestIndex < buildRequests.Length) && !breadthFirstTraversal)
                    {
                        wrapperArrayDepthFirst[0] = buildRequests[currentRequestIndex];
                        buildRequests[currentRequestIndex].RequestId = 0;
                        resultsArrayDepthFirst[0] = null;

                        engineCallback.PostBuildRequestsToHost(wrapperArrayDepthFirst);
                        workerThread.WaitForResults(handleId, resultsArrayDepthFirst, wrapperArrayDepthFirst);
                        //Copy the result from an intermediate array to the full array
                        buildResultsLocal[currentRequestIndex] = resultsArrayDepthFirst[0];
                        //Check if the call failed (null result was returned)
                        if (buildResultsLocal[currentRequestIndex] == null)
                        {
                            return;
                        }

                        //Move to the next request
                        currentRequestIndex++;
                    }
                }
            }
        }

        /// <summary>
        /// Call into the engine to figure out the line and column number of the task XML node in the original
        /// project context
        /// </summary>
        virtual internal void GetLineColumnOfXmlNode(int handleId, out int lineNumber, out int columnNumber)
        {
            engineCallback.GetLineColumnOfXmlNode(handleId, out lineNumber, out columnNumber);
        }

        /// <summary>
        /// Gets the global tasks registry defined by the *.tasks files.
        /// </summary>
        /// <returns>Global/default tasks registry.</returns>
        internal ITaskRegistry GetDefaultTasksRegistry(int handleId)
        {
            return engineCallback.GetEngineTaskRegistry(handleId);
        }

        /// <summary>
        /// Gets the tasks registry for the given project.
        /// </summary>
        /// <returns>Project task registry.</returns>
        internal ITaskRegistry GetProjectTasksRegistry(int handleId)
        {
            return engineCallback.GetProjectTaskRegistry(handleId);
        }

        /// <summary>
        /// Gets the path to the tools used for the particular task
        /// </summary>
        internal string GetToolsPath(int handleId)
        {
            return engineCallback.GetToolsPath(handleId);
        }

        internal bool RethrowTaskExceptions()
        {
            return (moduleMode == TaskExecutionModuleMode.SingleProcMode);
        }

        #endregion

        #region Methods called from the engine
        /// <summary>
        /// Called to execute a task within a target. This method instantiates the task, sets its parameters, 
        /// and executes it. 
        /// </summary>
        /// <param name="taskState"></param>
        public void ExecuteTask(TaskExecutionState taskState)
        {
            // Fill out the local fields of the task state
            taskState.ParentModule = this;
            taskState.LoggingService = engineCallback.GetParentEngine().LoggingServices;
            taskState.ProfileExecution = profileExecution;

            // If we running in single proc mode, we should execute this task on the current thread
            if (moduleMode == TaskExecutionModuleMode.SingleProcMode)
            {
               taskState.ExecuteTask();
            }
            else
            {
                // In multiproc mode post the work item to the workerThread queue so it can be executed by the worker thread.
                workerThread.PostWorkItem(taskState);
            }
        }

        /// <summary>
        /// Uses the parent engine to get the next unique TaskID.
        /// </summary>
        /// <returns></returns>
        internal int GetNextTaskId()
        {
            return engineCallback.GetParentEngine().GetNextTaskId();
        }

        /// <summary>
        /// This method lets the engine provide node with results of an evaluation it was waiting on.
        /// </summary>
        /// <param name="buildResult"></param>
        internal void PostBuildResults(BuildResult buildResult)
        {
            // Do nothing in the single proc mode
            if (moduleMode == TaskExecutionModuleMode.SingleProcMode)
            {
                return;
            }

            buildResult.ConvertToTaskItems();
            workerThread.PostBuildResult(buildResult);
        }

        /// <summary>
        /// This function returns the last time TEM was active executing a task
        /// </summary>
        internal long LastTaskActivity()
        {
            if (moduleMode != TaskExecutionModuleMode.SingleProcMode)
            {
                if (activeThreadCount == 0 && workerThread.WorkItemCount == 0)
                {
                    return lastTaskActivity;
                }
                else
                {
                    return DateTime.Now.Ticks;
                }
            }
            return DateTime.Now.Ticks;
        }

        internal int[] GetWaitingTaskData(List<BuildRequest []> outstandingRequests)
        {
            if (moduleMode != TaskExecutionModuleMode.SingleProcMode)
            {
                return workerThread.GetWaitingTasksData(outstandingRequests);
            }
            return new int [0];
        }

        internal void Shutdown()
        {
            if (moduleMode != TaskExecutionModuleMode.SingleProcMode)
            {
                workerThread.Shutdown();

                while (overallThreadCount != 0)
                {
                    threadOverallCountEvent.WaitOne();
                    threadOverallCountEvent.Reset();
                }

                if (profileExecution)
                {
                    int taskTimeMs = 0;

                    if (totalTaskTime != 0)
                    {
                        TimeSpan taskTimeSpan = new TimeSpan(totalTaskTime);
                        taskTimeMs = (int)taskTimeSpan.TotalMilliseconds;
                    }
                    Console.WriteLine("Node time spent " + taskTimeMs);
                }
            }
        }
        #endregion

        #region Methods used by worker threads

        internal void IncrementOverallThreadCount()
        {
            Interlocked.Increment(ref overallThreadCount);
        }

        internal void DecrementOverallThreadCount()
        {
            Interlocked.Decrement(ref overallThreadCount);
            threadOverallCountEvent.Set();
        }

        internal void DecrementActiveThreadCount()
        {
            Interlocked.Decrement(ref activeThreadCount);
            threadActiveCountEvent.Set();
        }

        internal void WaitForZeroActiveThreadCount()
        {
            while (Interlocked.CompareExchange(ref activeThreadCount, 1, 0) != 0)
            {
                threadActiveCountEvent.WaitOne();
                threadActiveCountEvent.Reset();
            }
            lastTaskActivity = DateTime.Now.Ticks;
        }

        #endregion

        #region Methods used for unittest only
        /// <summary>
        /// ONLY for unit testing
        /// </summary>
        internal TaskExecutionModuleMode GetExecutionModuleMode()
        {
           // The Execution module mode is used to determine if they system is running under single proc or multiproc for the purposes of creating a new thread
           // to execute tasks on.
           return moduleMode;
        }

        /// <summary>
        ///  ONLY for unit testing
        /// </summary>
        internal TaskWorkerThread GetWorkerThread()
        {
            return workerThread;
        }
        #endregion

        #region Member data
        /// <summary>
        /// Callback interface to communicate with the engine
        /// </summary>
        private EngineCallback engineCallback;
        /// <summary>
        /// The mode in which the TEM is running
        /// </summary>
        TaskExecutionModuleMode moduleMode;
        /// <summary>
        /// The class used to execute user tasks.
        /// </summary>
        TaskWorkerThread workerThread;

        // Data shared between all worker threads within the TEM
        /// <summary>
        /// Total count of worker threads both active and inactive
        /// </summary>
        private int overallThreadCount;
        /// <summary>
        /// Event indicated a decrease in overallThreadCount due to an exit of a thread
        /// </summary>
        private ManualResetEvent threadOverallCountEvent;
        /// <summary>
        /// Count of active thread (i.e. threads in user code). Has to be either 0 or 1
        /// </summary>
        private int activeThreadCount;
        /// <summary>
        /// Event indicating a decrease in activeThreadCount due to a thread leaving user code
        /// </summary>
        private ManualResetEvent threadActiveCountEvent;
        /// <summary>
        /// Time stamp of last execution of user code. Only valid if activeThreadCount == 0.
        /// </summary>
        private long lastTaskActivity;
        /// <summary>
        /// Specifies the traversal type for callbacks. If true multiple build requests will be sent
        /// to the engine if false the requests will be sent one at a time.
        /// </summary>
        private bool breadthFirstTraversal;
        /// <summary>
        /// Specifies if the timing data on task execution should be collected
        /// </summary>
        private bool profileExecution;
        /// <summary>
        /// Total time spent executing task code. Only valid if profileExecution is true.
        /// </summary>
        private long totalTaskTime;

        /// <summary>
        /// This property allows a task to query whether or not the system is running in single process mode or multi process mode.
        /// Single process mode (IsRunningMultipleNodes = false) is where the engine is initialized with the number of cpus = 1 and the engine is not a child engine.
        /// The engine is in multi process mode (IsRunningMultipleNodes = true) when the engine is initialized with a number of cpus > 1 or the engine is a child engine.
        /// </summary>
        private bool isRunningMultipleNodes;

        private static StringBuilder currentDirectoryBuffer = new StringBuilder(270);

        /// <summary>
        /// In a multiproc build this is the maximum number of build requests which will be sent at a time to the parent engine
        /// A default of 10 was an arbitrary number but turned out to be a good balance between being too small 
        /// causing the system to run out of work too quickly and being too big and flooding the system with requests.
        /// </summary>
        private const int defaultBatchRequestSize = 10;
        private int batchRequestSize = defaultBatchRequestSize;
        
        /// <summary>
        /// The nodeId of the node the TaskExecutionModule is running on
        /// </summary>
        private int nodeId = -1;

        #endregion

        #region Enums

        internal enum TaskExecutionModuleMode
        {
            /// <summary>
            /// In this mode the tasks should be executed on the calling thread
            /// </summary>
            SingleProcMode = 0,
            /// <summary>
            /// In this mode the tasks should be executed on a different thread and the execute calls
            /// should return immediately. The messages due to the task are not flushed.
            /// </summary>
            MultiProcFullNodeMode = 1,
        }

        #endregion
    }
}
