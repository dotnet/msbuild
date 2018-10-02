// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Microsoft.Build.Framework;
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
        internal TaskWorkerThread()
        {
            // Create events private to this thread
            this.activationEvent = new ManualResetEvent(false);
            this.localDoneNoticeEvent = new ManualResetEvent(false);
            this.threadActive = false;
            this.targetEvaluationResults = new LinkedList<BuildResult>();

            // Clear out the handles cache
            BaseActiveThreadWaitHandles = null;
            WaitingActiveThreadWaitHandles = null;
            WaitingPassiveThreadWaitHandles = null;

            // Start the thread that will be processing the events
            ThreadStart threadState = new ThreadStart(this.MainThreadLoop);
            Thread taskThread = new Thread(threadState);
            //taskThread.SetApartmentState(ApartmentState.STA);
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
                    NodeActionLoop(NodeLoopExecutionMode.BaseActiveThread, -1, null);
                    // Add this thread to the inactive list
                    lock (workerThreadQueue)
                    {
                        workerThreadQueue.Enqueue(this);
                    }
                }
            }
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
        /// <param name="executionMode"></param>
        /// <param name="nodeProxyId"></param>
        /// <param name="requestResults"></param>
        private void NodeActionLoop(NodeLoopExecutionMode executionMode, int nodeProxyId, BuildResult [] requestResults)
        {
            // Create an array of event to the node thread responds
            WaitHandle[] waitHandles = GetHandlesArray(executionMode);

            int resultCount = 0;

            // A thread that is waiting for a done notification is no longer
            // actively executing a task so the active cound needs to be decreased
            if (executionMode != NodeLoopExecutionMode.BaseActiveThread)
            {
                Interlocked.Decrement(ref activeThreadCount);
                threadCountEvent.Set();
            }

            bool continueExecution = true;
            while (continueExecution)
            {
                int eventType = WaitHandle.WaitAny(waitHandles);

                if (Environment.GetEnvironmentVariable("MSBUILDDEBUG") == "1")
                {
                    Console.WriteLine("NodeProxy :" + nodeProxyId + " waking up due " + eventType);
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

                        // Wait until all there are no other active threads, we
                        // always transition from 0 to 1 atomically before executing the task
                        while (Interlocked.CompareExchange(ref activeThreadCount, 1, 0) != 0)
                        {
                            threadCountEvent.WaitOne();
                            threadCountEvent.Reset();
                        }

                        proxyIdToWorkerThread[taskToExecute.NodeProxyId] = this;

                        // Actually execute the task
                        taskToExecute.ExecuteTask();

                        proxyIdToWorkerThread.Remove(taskToExecute.NodeProxyId);

                        // Indicate that this thread is no longer active
                        Interlocked.Decrement(ref activeThreadCount);
                        threadCountEvent.Set();
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
                            workerThread = new TaskWorkerThread();
                        }

                        workerThread.ActivateThread();
                    }
                }
                else if (eventType == 1 && executionMode == NodeLoopExecutionMode.WaitingPassiveThread ||
                         eventType == 2 && executionMode == NodeLoopExecutionMode.WaitingActiveThread)
                {
                    // There maybe multiple results in the list so we need to loop over it 
                    // and store the results
                    int originalResultCount = resultCount;
                    lock (targetEvaluationResults)
                    {
                        //Console.WriteLine("Worker thread for: " + nodeProxyId + " Got results");
                        LinkedListNode<BuildResult> currentNode = targetEvaluationResults.First;
                        while (currentNode != null)
                        {   
                            BuildResult buildResult = currentNode.Value;
                            ErrorUtilities.VerifyThrow(
                                            buildResult.RequestId < requestResults.Length,
                                            "The request ids should be inside the array");
                            requestResults[buildResult.RequestId] = buildResult;
                            // Increment the result count to indicate that we got another result
                            resultCount++;
                            // Go to the next entry in the list (most of the time there will be just one entry)
                            currentNode = currentNode.Next;
                        }
                        targetEvaluationResults.Clear();
                        // Reset the handle now that we done with the events
                        int handleIndex = executionMode == NodeLoopExecutionMode.WaitingPassiveThread ? 1 : 2;
                        ((ManualResetEvent)waitHandles[handleIndex]).Reset();
                    }
                    ErrorUtilities.VerifyThrow(originalResultCount < resultCount, "We should have found at least 1 result");
                    // If we received results for all the requests we need to exit
                    if (resultCount == requestResults.Length)
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
                while (Interlocked.CompareExchange(ref activeThreadCount, 1, 0) != 0)
                {
                    threadCountEvent.WaitOne();
                    threadCountEvent.Reset();
                }
            }
        }

        private static TaskWorkerThread GetWorkerThreadForProxyId(int nodeProxyId)
        {
            ErrorUtilities.VerifyThrow(proxyIdToWorkerThread[nodeProxyId] != null, "Worker thread should be in the table");
            return (TaskWorkerThread)proxyIdToWorkerThread[nodeProxyId];
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
        /// <param name="nodeProxyId"></param>
        /// <param name="requestResults"></param>
        internal static void WaitForResults(int nodeProxyId, BuildResult[] requestResults)
        {
            TaskWorkerThread workerThread = TaskWorkerThread.GetWorkerThreadForProxyId(nodeProxyId);
            workerThread.NodeActionLoop(workerThread.threadActive ? NodeLoopExecutionMode.WaitingActiveThread : 
                                        NodeLoopExecutionMode.WaitingPassiveThread,
                                        nodeProxyId, requestResults);
        }

        internal static void PostWorkItem( TaskExecutionState workItem)
        {
            lock (workItemQueue)
            {
                workItemQueue.Enqueue(workItem);
                workItemInsertionEvent.Set();
            }
        }

        internal static void PostBuildResult( BuildResult buildResult)
        {
            TaskWorkerThread workerThread = TaskWorkerThread.GetWorkerThreadForProxyId(buildResult.NodeProxyId);

            lock (workerThread.targetEvaluationResults)
            {
                workerThread.targetEvaluationResults.AddLast(new LinkedListNode<BuildResult>(buildResult));
                workerThread.LocalDoneNoticeEvent.Set();
            }
        }

        internal static void Shutdown()
        {
            exitTaskThreads.Set();
        }
        #endregion

        #region Data

        private ManualResetEvent activationEvent;
        private ManualResetEvent localDoneNoticeEvent;
        private bool threadActive;

        // Private cache arrays of handles
        private WaitHandle[] BaseActiveThreadWaitHandles;
        private WaitHandle[] WaitingActiveThreadWaitHandles;
        private WaitHandle[] WaitingPassiveThreadWaitHandles;

        // Static data shared between all threads
        private static ManualResetEvent exitTaskThreads = new ManualResetEvent(false);
        private static Queue<TaskWorkerThread> workerThreadQueue = new Queue<TaskWorkerThread>();
        private static int activeThreadCount = 0;
        private static ManualResetEvent threadCountEvent = new ManualResetEvent(false);
        private static Hashtable proxyIdToWorkerThread = new Hashtable();

        private LinkedList<BuildResult> targetEvaluationResults;
        private static Queue<TaskExecutionState> workItemQueue = new Queue<TaskExecutionState>();
        private static ManualResetEvent workItemInsertionEvent = new ManualResetEvent(false);

        #endregion
    }
}
