// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is a wrapper around a worker thread that executes a task
    /// </summary>
    internal class TaskWorkerThread
    {
        enum NodeLoopExecutionMode
        {
            /// <summary>
            /// This is a mode of a thread that is not executing a task but is responsible for picking up
            /// work items as they arrive to the queue
            /// </summary>
            BaseActiveThread = 0,
            /// <summary>
            /// This is a mode of a thread that was in BaseActiveThread mode and processes a callback via
            /// IBuildEngine interface. If a work item arrives while the thread is in this morning it will pass
            /// the ownership of the queue to another thread
            /// </summary>
            WaitingActiveThread = 1,
            /// <summary>
            /// This is a mode of a thread that is not watching the work item queue and is waiting for
            /// results in order to return back to task execution.
            /// </summary>
            WaitingPassiveThread = 2
        }

        #region Constructors
        /// <summary>
        /// This constructor creates a worker thread which is immediately ready to be activated. Once
        /// activated the thread will execute tasks as they appear in the work item queue. Once the
        /// thread is blocked from executing tasks it will pass the ownership of the work item queue to another
        /// thread
        /// </summary>
        internal TaskWorkerThread(TaskExecutionModule parentModule, bool profileExecution)
        {
            this.parentModule = parentModule;

            // Initialize the data that only has to be set by the very first thread
            // created by the TEM
            this.exitTaskThreads = new ManualResetEvent(false);
            this.exitTaskThreadsCache = new ExitTaskCache(false);
            this.workerThreadQueue = new Queue<TaskWorkerThread>();
            this.handleIdToWorkerThread = new Hashtable();
            this.workItemQueue = new Queue<TaskExecutionState>();
            this.workItemInsertionEvent = new ManualResetEvent(false);
            this.waitingTasks = new Hashtable();
            this.profileExecution = profileExecution;

            InitializePerInstanceData();
        }

        /// <summary>
        /// This constructor is used by the class internally to create new instances when a thread
        /// becomes blocked by a user code callback.
        /// </summary>
        private TaskWorkerThread
        (
            TaskExecutionModule parentModule,
            ManualResetEvent exitTaskThreads,
            ExitTaskCache exitTaskThreadsCache,
            Queue<TaskWorkerThread> workerThreadQueue,
            Hashtable handleIdToWorkerThread,
            Queue<TaskExecutionState> workItemQueue,
            ManualResetEvent workItemInsertionEvent,
            Hashtable waitingTasks,
            bool profileExecution
        )
        {
            this.parentModule = parentModule;
            this.exitTaskThreads = exitTaskThreads;
            this.exitTaskThreadsCache = exitTaskThreadsCache;
            this.workerThreadQueue = workerThreadQueue;
            this.handleIdToWorkerThread = handleIdToWorkerThread;
            this.workItemQueue = workItemQueue;
            this.workItemInsertionEvent = workItemInsertionEvent;
            this.waitingTasks = waitingTasks;
            this.profileExecution = profileExecution;

            InitializePerInstanceData();
        }

        private void InitializePerInstanceData()
        {
            // Create events private to this thread
            this.activationEvent = new ManualResetEvent(false);
            this.localDoneNoticeEvent = new ManualResetEvent(false);
            this.threadActive = false;
            this.postedBuildResults = new LinkedList<BuildResult>();
            this.currentWorkitem = null;

            // Clear out the handles cache
            BaseActiveThreadWaitHandles = null;
            WaitingActiveThreadWaitHandles = null;
            WaitingPassiveThreadWaitHandles = null;

            // Start the thread that will be processing the events
            ThreadStart threadState = new ThreadStart(this.MainThreadLoop);
            Thread taskThread = new Thread(threadState);
            taskThread.Name = "MSBuild Task Worker";
            taskThread.SetApartmentState(ApartmentState.STA);
            taskThread.Start();
        }
        #endregion

        #region Properties
        /// <summary>
        /// This event is triggered by the node when a done notice is received
        /// </summary>
        internal ManualResetEvent LocalDoneNoticeEvent
        {
            get
            {
                return this.localDoneNoticeEvent;
            }
        }

        internal int WorkItemCount
        {
            get
            {
                // UNDONE this access depends on thread safety of workItemQueue.Count
                return this.workItemQueue.Count;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// This a base loop of a worker thread. The worker thread is asleep waiting for either an
        /// event indicating that it should shut down or that it should become active and take
        /// ownership of the work item queue
        /// </summary>
        private void MainThreadLoop()
        {
            // Create an array of event to the node thread responds
            WaitHandle[] waitHandles = new WaitHandle[2];
            waitHandles[0] = exitTaskThreads;
            waitHandles[1] = activationEvent;

            bool continueExecution = true;

            parentModule.IncrementOverallThreadCount();

            try
            {
                while (continueExecution)
                {
                    // Wait for the next work item or an exit command
                    int eventType = WaitHandle.WaitAny(waitHandles);

                    if (eventType == 0)
                    {
                        // Exit node event
                        continueExecution = false;
                    }
                    else
                    {
                        activationEvent.Reset();
                        // Start the base loop, this will return once this is no longer the active thread
                        NodeActionLoop(NodeLoopExecutionMode.BaseActiveThread, EngineCallback.invalidEngineHandle, null);
                        // Add this thread to the inactive list
                        lock (workerThreadQueue)
                        {
                            workerThreadQueue.Enqueue(this);
                        }
                    }
                }
            }
            finally
            {
                parentModule.DecrementOverallThreadCount();
            }
        }

        /// <summary>
        /// Don't wait on system objects if we don't have to - see if we have work to do.
        /// </summary>
        /// <param name="executionMode"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private bool WaitAnyFast(NodeLoopExecutionMode executionMode, out int index)
        {
            index = 0;

            if (exitTaskThreadsCache.exitTaskThreads)
            {
                return true;
            }

            if (executionMode == NodeLoopExecutionMode.BaseActiveThread)
            {
                lock (workItemQueue)
                {
                    if (workItemQueue.Count > 0)
                    {
                        index = 1;
                        return true;
                    }
                }
            }
            else if (executionMode == NodeLoopExecutionMode.WaitingActiveThread)
            {
                lock (workItemQueue)
                {
                    if (workItemQueue.Count > 0)
                    {
                        index = 1;
                        return true;
                    }
                }
                lock (postedBuildResults)
                {
                    if (postedBuildResults.Count > 0)
                    {
                        index = 2;
                        return true;
                    }
                }
            }
            else if (executionMode == NodeLoopExecutionMode.WaitingPassiveThread)
            {
                lock (postedBuildResults)
                {
                    if (postedBuildResults.Count > 0)
                    {
                        index = 1;
                        return true;
                    }
                }
            }
            else
            {
                ErrorUtilities.VerifyThrow(false, "Unexpected NodeLoopExecutionMode");
            }

            return false;
        }

        /// <summary>
        /// This function calculates the array of events the thread should wait on depending on its
        /// execution mode
        /// </summary>
        /// <param name="executionMode">Current execution mode</param>
        /// <returns>Array of handles to wait on</returns>
        private WaitHandle [] GetHandlesArray( NodeLoopExecutionMode executionMode )
        {
            WaitHandle[] waitHandles = null;

            if (executionMode == NodeLoopExecutionMode.BaseActiveThread)
            {
                if (BaseActiveThreadWaitHandles == null)
                {
                    BaseActiveThreadWaitHandles = new WaitHandle[2];
                    BaseActiveThreadWaitHandles[0] = exitTaskThreads;
                    BaseActiveThreadWaitHandles[1] = workItemInsertionEvent;
                }
                waitHandles = BaseActiveThreadWaitHandles;
            }
            else if (executionMode == NodeLoopExecutionMode.WaitingActiveThread)
            {
                if (WaitingActiveThreadWaitHandles == null)
                {
                    WaitingActiveThreadWaitHandles = new WaitHandle[3];
                    WaitingActiveThreadWaitHandles[0] = exitTaskThreads;
                    WaitingActiveThreadWaitHandles[1] = workItemInsertionEvent;
                    WaitingActiveThreadWaitHandles[2] = localDoneNoticeEvent;
                }
                waitHandles = WaitingActiveThreadWaitHandles;
            }
            else if (executionMode == NodeLoopExecutionMode.WaitingPassiveThread)
            {
                if (WaitingPassiveThreadWaitHandles == null)
                {
                    WaitingPassiveThreadWaitHandles = new WaitHandle[2];
                    WaitingPassiveThreadWaitHandles[0] = exitTaskThreads;
                    WaitingPassiveThreadWaitHandles[1] = localDoneNoticeEvent;
                }
                waitHandles = WaitingPassiveThreadWaitHandles;
            }
            else
            {
                ErrorUtilities.VerifyThrow(false, "Unexpected NodeLoopExecutionMode");
            }

            return waitHandles;
        }

        /// <summary>
        /// This is the loop for all active threads. Depending on the current execution mode the thread
        /// will listen to different events. There is only one thread at the time that owns the workitem
        /// queue and listens for tasks to be executed. There is also only one thread at a time that is
        /// execution a task. That thread owns the current directory and the environment block.
        /// </summary>
        private void NodeActionLoop
        (
            NodeLoopExecutionMode executionMode,
            int handleId,
            BuildResult [] buildResults
        )
        {
            // Create an array of event to the node thread responds
            WaitHandle[] waitHandles = GetHandlesArray(executionMode);

            int resultCount = 0;
            long entryTime = 0;

            // A thread that is waiting for a done notification is no longer
            // actively executing a task so the active cound needs to be decreased
            if (executionMode != NodeLoopExecutionMode.BaseActiveThread)
            {
                parentModule.DecrementActiveThreadCount();
                // If requested measure the time spent waiting for the results
                if (profileExecution)
                {
                    entryTime = DateTime.Now.Ticks;
                }
            }

            bool continueExecution = true;
            while (continueExecution)
            {
                int eventType;

                // Try and avoid the wait on kernel objects if possible.
                if (!WaitAnyFast(executionMode, out eventType))
                {
                    eventType = WaitHandle.WaitAny(waitHandles);
                }

                if (Engine.debugMode)
                {
                    Console.WriteLine("TaskWorkerThread: HandleId " + handleId + " waking up due to event type " + eventType);
                }

                // Node exit event - all threads need to exit
                if (eventType == 0)
                {
                    continueExecution = false;
                }
                // New work item has appeared in the queue
                else if (eventType == 1 && executionMode != NodeLoopExecutionMode.WaitingPassiveThread )
                {
                    ErrorUtilities.VerifyThrow(
                                    executionMode == NodeLoopExecutionMode.WaitingActiveThread ||
                                    executionMode == NodeLoopExecutionMode.BaseActiveThread,
                                    "Only active threads should receive work item events");

                    if (executionMode == NodeLoopExecutionMode.BaseActiveThread)
                    {
                        // Wait until all there are no other active threads, we
                        // always transition from 0 to 1 atomically before executing the task
                        parentModule.WaitForZeroActiveThreadCount();

                        TaskExecutionState taskToExecute = null;
                        lock (workItemQueue)
                        {
                            taskToExecute = workItemQueue.Dequeue();
                            // We may get a single event for multiple messages so only reset the event
                            // if the queue is empty
                            if (workItemQueue.Count == 0)
                            {
                                workItemInsertionEvent.Reset();
                            }
                        }

                        // Execute the task either directly or on a child thread
                        ErrorUtilities.VerifyThrow(taskToExecute != null, "Expected a workitem");

                        handleIdToWorkerThread[taskToExecute.HandleId] = this;
                        currentWorkitem = taskToExecute;

                        // Actually execute the task (never throws - all exceptions are captured)
                        taskToExecute.ExecuteTask();

                        currentWorkitem = null;
                        handleIdToWorkerThread.Remove(taskToExecute.HandleId);

                        // Indicate that this thread is no longer active
                        parentModule.DecrementActiveThreadCount();
                    }
                    else
                    {
                        // Change the thread execution mode since it will no longer be
                        // listening to the work item queue
                        executionMode = NodeLoopExecutionMode.WaitingPassiveThread;
                        threadActive = false;
                        waitHandles = GetHandlesArray(executionMode);

                        TaskWorkerThread workerThread = null;
                        lock (workerThreadQueue)
                        {
                            if (workerThreadQueue.Count != 0)
                            {
                                //Console.WriteLine("REUSING a thread");
                                workerThread = workerThreadQueue.Dequeue();
                            }
                        }
                        if (workerThread == null)
                        {
                            //Console.WriteLine("CREATING a thread");
                            workerThread = new TaskWorkerThread(parentModule, exitTaskThreads, exitTaskThreadsCache, workerThreadQueue, handleIdToWorkerThread,
                                                                workItemQueue, workItemInsertionEvent, waitingTasks, profileExecution);
                        }

                        workerThread.ActivateThread();
                    }
                }
                else if ((eventType == 1 && executionMode == NodeLoopExecutionMode.WaitingPassiveThread) ||
                         (eventType == 2 && executionMode == NodeLoopExecutionMode.WaitingActiveThread))
                {
                    // There maybe multiple results in the list so we need to loop over it 
                    // and store the results
                    int originalResultCount = resultCount;
                    lock (postedBuildResults)
                    {
                        LinkedListNode<BuildResult> currentNode = postedBuildResults.First;
                        while (currentNode != null)
                        {
                            BuildResult buildResult = currentNode.Value;
                            ErrorUtilities.VerifyThrow(
                                            buildResult.RequestId < buildResults.Length,
                                            "The request ids should be inside the array");
                            buildResults[buildResult.RequestId] = buildResult;
                            // Increment the result count to indicate that we got another result
                            resultCount++;
                            // Go to the next entry in the list (most of the time there will be just one entry)
                            currentNode = currentNode.Next;
                        }
                        postedBuildResults.Clear();
                        // Reset the handle now that we done with the events
                        int handleIndex = executionMode == NodeLoopExecutionMode.WaitingPassiveThread ? 1 : 2;
                        ((ManualResetEvent)waitHandles[handleIndex]).Reset();
                    }
                    ErrorUtilities.VerifyThrow(originalResultCount < resultCount, "We should have found at least 1 result");
                    // If we received results for all the requests we need to exit
                    if (resultCount == buildResults.Length)
                    {
                        continueExecution = false;
                    }
                }
                // Check if we need to update the state
                if (executionMode == NodeLoopExecutionMode.BaseActiveThread && !threadActive)
                {
                    continueExecution = false;
                }
            }

            ErrorUtilities.VerifyThrow
                (resultCount == 0 || executionMode != NodeLoopExecutionMode.BaseActiveThread,
                 "The base thread should never return a value");

            // If a thread exits this loop it is back to actively executing the task,
            // so the active thread count has to be increased
            if (executionMode != NodeLoopExecutionMode.BaseActiveThread)
            {
                parentModule.WaitForZeroActiveThreadCount();
                // Sent the time spent waiting for results to the ExecutionState so that the task execution time can be measured correctly
                if (profileExecution)
                {
                    this.currentWorkitem.NotifyOfWait(entryTime);
                }
            }
        }

        private TaskWorkerThread GetWorkerThreadForHandleId(int handleId)
        {
            return (TaskWorkerThread)handleIdToWorkerThread[handleId];
        }

        /// <summary>
        /// This method is called to cause a thread to become active and take ownership of the workitem
        /// queue
        /// </summary>
        internal void ActivateThread()
        {
            threadActive = true;
            activationEvent.Set();
        }

        /// <summary>
        /// This function is called when the task executes a callback via IBuildEngine interface. A thread
        /// that currently owns the workitem queue will continue to own it, unless a work item comes in while
        /// it is inside the callback. A thread that enters the callback no longer owns the current directory and
        /// environment block, but it will always regain them before returning to the task.
        /// </summary>
        internal void WaitForResults
        (
            int handleId,
            BuildResult[] buildResults,
            BuildRequest [] buildRequests
        )
        {
            TaskWorkerThread workerThread = GetWorkerThreadForHandleId(handleId);
            ErrorUtilities.VerifyThrow(workerThread != null, "Worker thread should be in the table");
            WaitingTaskData taskData = new WaitingTaskData(buildRequests, buildResults);
            lock (waitingTasks)
            {
                waitingTasks.Add(handleId, taskData);
            }
            workerThread.NodeActionLoop(workerThread.threadActive ? NodeLoopExecutionMode.WaitingActiveThread :
                                        NodeLoopExecutionMode.WaitingPassiveThread,
                                        handleId, buildResults);
            lock (waitingTasks)
            {
                waitingTasks.Remove(handleId);
            }
        }


        internal int [] GetWaitingTasksData(List<BuildRequest[]> outstandingRequests)
        {
            int[] waitingTasksArray;
            lock (waitingTasks)
            {
                waitingTasksArray = new int[waitingTasks.Keys.Count];
                int i = 0;
                foreach (DictionaryEntry entry in waitingTasks)
                {
                    // Store the node proxy 
                    waitingTasksArray[i] = (int)entry.Key;
                    // Loop through the build requests and add uncomplete requests to the list
                    WaitingTaskData taskData = (WaitingTaskData)entry.Value;
                    List<BuildRequest> requests = new List<BuildRequest>();
                    for (int j = 0; j < taskData.buildRequests.Length; j++)
                    {
                        if (taskData.buildResults[j] == null)
                        {
                            requests.Add(taskData.buildRequests[j]);
                        }
                    }
                    outstandingRequests.Add(requests.ToArray());
                    // Move to the next output entry
                    i++;
                }
            }
            return waitingTasksArray;
        }

        internal void PostWorkItem( TaskExecutionState workItem)
        {
            lock (workItemQueue)
            {
                workItemQueue.Enqueue(workItem);
                workItemInsertionEvent.Set();
            }
        }

        internal void PostBuildResult( BuildResult buildResult)
        {
            TaskWorkerThread workerThread = GetWorkerThreadForHandleId(buildResult.HandleId);

            if (workerThread != null)
            {
                lock (workerThread.postedBuildResults)
                {
                    workerThread.postedBuildResults.AddLast(new LinkedListNode<BuildResult>(buildResult));
                    workerThread.LocalDoneNoticeEvent.Set();
                }
            }
        }

        internal void Shutdown()
        {
            exitTaskThreads.Set();
            exitTaskThreadsCache.exitTaskThreads = true;
        }

        #endregion

        #region Data
        // Per instance data
        private ManualResetEvent activationEvent;
        private ManualResetEvent localDoneNoticeEvent;
        private bool threadActive;
        private LinkedList<BuildResult> postedBuildResults;
        private TaskExecutionState currentWorkitem;
        private bool profileExecution;

        // Private cache arrays of handles
        private WaitHandle[] BaseActiveThreadWaitHandles;
        private WaitHandle[] WaitingActiveThreadWaitHandles;
        private WaitHandle[] WaitingPassiveThreadWaitHandles;

        // Data shared between worked threads for one TEM, this data is initialized by the first 
        // thread
        private ManualResetEvent exitTaskThreads;          // Used to signal all threads to exit
        private ExitTaskCache exitTaskThreadsCache;        // cached value to avoid waiting on the kernel event
        private Queue<TaskWorkerThread> workerThreadQueue; // Queue of idle worker thread ready to be activated
        private Hashtable handleIdToWorkerThread;           // Table mapping in progress Ids to worker threads
        private Queue<TaskExecutionState> workItemQueue;   // Queue of workitems that need to be executed
        private ManualResetEvent workItemInsertionEvent;   // Used to signal a new work item
        private Hashtable waitingTasks;                    // Hastable containing information about in progress
                                                           // task, used for determining if all threads are blocked
        private TaskExecutionModule parentModule;          // A pointer to the parent TEM

        #endregion

        #region Private struct
        private class WaitingTaskData
        {
            internal WaitingTaskData(BuildRequest[] buildRequests, BuildResult[] buildResults)
            {
                this.buildRequests = buildRequests;
                this.buildResults = buildResults;
            }

            internal BuildRequest[] buildRequests;
            internal BuildResult[] buildResults;
        }

        private class ExitTaskCache
        {
            internal ExitTaskCache(bool value)
            {
                this.exitTaskThreads = value;
            }

            internal bool exitTaskThreads;
        }
        #endregion
    }
}
