// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is an implementation of IEngineCallback which is used on the child nodes to receive calls from
    /// the child to the parent.
    /// </summary>
    internal class LocalNodeCallback : IEngineCallback
    {
        /// <summary>
        /// All the necessary data required for a reply to a call
        /// </summary>
        internal class ReplyData
        {
            // The call descriptor for the actual reply
            internal LocalReplyCallDescriptor reply;

            // wait event on which the requesting thread waits and which should be set by the replying thread
            internal ManualResetEvent waitEvent;
        }

        #region Constructors

        /// <summary>
        /// This class is an implementation of IEngineCallback which is used on the child nodes
        /// </summary>
        internal LocalNodeCallback(ManualResetEvent exitCommunicationThreads, LocalNode localNode)
        {
            this.localNode = localNode;
            this.exitCommunicationThreads = exitCommunicationThreads;

            this.nodeCommandQueue = new DualQueue<LocalCallDescriptor>();
            this.nodeHiPriCommandQueue = new DualQueue<LocalCallDescriptor>();

            // Initalize the reply infrastructur
            this.repliesFromParent = new Hashtable();
        }

        internal void StartWriterThread(int nodeNumber)
        {
            this.writerThreadHasExited = false;

            this.sharedMemory =
                new SharedMemory
                (
                    LocalNodeProviderGlobalNames.NodeOutputMemoryName(nodeNumber),
                    SharedMemoryType.WriteOnly,
                    true
                );

            ErrorUtilities.VerifyThrow(this.sharedMemory.IsUsable,
                "Failed to create shared memory for engine callback.");

            // Start the thread that will be processing the calls to the parent engine
            ThreadStart threadState = new ThreadStart(this.SharedMemoryWriterThread);
            writerThread = new Thread(threadState);
            writerThread.Name = "MSBuild Child->Parent Writer";            
            writerThread.Start();
        }

        #endregion

        #region Methods

        /// <summary>
        /// This method is used to post calls to the parent engine by the Localnode class
        /// </summary>
        internal void PostMessageToParent(LocalCallDescriptor callDescriptor, bool waitForCompletion)
        {
            nodeCommandQueue.Enqueue(callDescriptor);

            try
            {
                if (waitForCompletion)
                {
                    // We should not be on the running on the callback writer thread
                    ErrorUtilities.VerifyThrow(Thread.CurrentThread != writerThread, "Should never call this function from the writer thread");

                    // We need to block until the event we posted has been processed, but if the writer thread
                    // exit due to an error the shared memory is no longer valid so there is no way to send the message
                    while (!writerThreadHasExited && nodeCommandQueue.Count > 0)
                    {
                        nodeCommandQueue.QueueEmptyEvent.WaitOne(1000, false);

                        // Check if the communication threads are supposed to exit
                        if (exitCommunicationThreads.WaitOne(0, false))
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Clear the current queue since something in the queue has caused a problem
                nodeCommandQueue.Clear();
                // Try to send the exception back to the parent
                localNode.ReportNonFatalCommunicationError(e);
            }
        }

        /// <summary>
        /// This method is used to post replies from the parent engine by the Localnode class
        /// </summary>
        internal void PostReplyFromParent(LocalReplyCallDescriptor reply)
        {
            int requestingCallNumber = reply.RequestingCallNumber;

            lock (repliesFromParent)
            {
                ReplyData replyData = (ReplyData) repliesFromParent[requestingCallNumber];
                ErrorUtilities.VerifyThrow(replyData != null && replyData.waitEvent != null, 
                    "We must have an event for this call at this point");

                replyData.reply = reply;
                replyData.waitEvent.Set();
            }
        }

        /// <summary>
        /// This method is reset the state of shared memory when the node is reused for a different
        /// build
        /// </summary>
        internal void Reset()
        {
            // Make sure there nothing left over in the command queues
            nodeCommandQueue.Clear();
            nodeHiPriCommandQueue.Clear();

            sharedMemory.Reset();
            ErrorUtilities.VerifyThrow(nodeCommandQueue.Count == 0, "Expect all event to be flushed");
        }

        internal Thread GetWriterThread()
        {
            return writerThread;
        }

        /// <summary>
        /// This method is used to write to shared memory
        /// </summary>
        private void SharedMemoryWriterThread()
        {
            // Create an array of event to the node thread responds
            WaitHandle[] waitHandles = new WaitHandle[3];
            waitHandles[0] = exitCommunicationThreads;
            waitHandles[1] = nodeCommandQueue.QueueReadyEvent;
            waitHandles[2] = nodeHiPriCommandQueue.QueueReadyEvent;

            bool continueExecution = true;

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
                        sharedMemory.Write(nodeCommandQueue, nodeHiPriCommandQueue, true);
                    }
                }
            }
            catch (Exception e)
            {
                // Will rethrow the exception if necessary
                localNode.ReportFatalCommunicationError(e);
            }
            finally
            {
                writerThreadHasExited = true;
            }

            // Dispose of the shared memory buffer
            if (sharedMemory != null)
            {
                sharedMemory.Dispose();
                sharedMemory = null;
            }
        }

        #region IEngineCallback implementation
        /// <summary>
        /// This method is called by the node to post build
        /// requests into a queue in the parent engine
        /// </summary>
        public void PostBuildRequestsToHost(BuildRequest[] buildRequests)
        {
            LocalCallDescriptorForPostBuildRequests callDescriptor =
                new LocalCallDescriptorForPostBuildRequests(buildRequests);
            nodeCommandQueue.Enqueue(callDescriptor);
        }

        /// <summary>
        /// This method is used by the child node to post results of a build request back to the
        /// parent node. The parent node then decides if need to re-route the results to another node
        /// that requested the evaluation or if it will consume the result locally
        /// </summary>
        public void PostBuildResultToHost(BuildResult buildResult)
        {
            LocalCallDescriptorForPostBuildResult callDescriptor =
                new LocalCallDescriptorForPostBuildResult(buildResult);
            nodeCommandQueue.Enqueue(callDescriptor);
        }

        /// <summary>
        /// Submit the logging message to the engine queue.
        /// </summary>
        public void PostLoggingMessagesToHost(int nodeProviderId, NodeLoggingEvent[] nodeLoggingEventArray)
        {
            LocalCallDescriptorForPostLoggingMessagesToHost callDescriptor =
                new LocalCallDescriptorForPostLoggingMessagesToHost(nodeLoggingEventArray);
            nodeCommandQueue.Enqueue(callDescriptor);
        }

        /// <summary>
        /// Given a non-void call descriptor, calls it and retrieves the return value.
        /// </summary>
        /// <param name="callDescriptor"></param>
        /// <returns></returns>
        private object GetReplyForCallDescriptor(LocalCallDescriptor callDescriptor)
        {
            // ReplyFromParentArrived is a TLS field, so initialize it if it's empty
            if (replyFromParentArrived == null)
            {
                replyFromParentArrived = new ManualResetEvent(false);
            }

            replyFromParentArrived.Reset();
            int requestingCallNumber = callDescriptor.CallNumber;
            
            ReplyData replyData = new ReplyData();
            replyData.waitEvent = replyFromParentArrived;

            // Register our wait event for the call id
            lock (repliesFromParent)
            {
                repliesFromParent[requestingCallNumber] = replyData;
            }

            nodeCommandQueue.Enqueue(callDescriptor);

            replyFromParentArrived.WaitOne();

            LocalCallDescriptor reply = null;

            // Unregister the wait event
            lock (repliesFromParent)
            {
                // Get the reply
                reply = replyData.reply;
                ErrorUtilities.VerifyThrow(reply != null, "We must have a reply if the wait event was set");

                repliesFromParent.Remove(requestingCallNumber);
            }

            return reply.GetReplyData();
        }

        /// <summary>
        /// This method retrieves the cache entries from the master cache
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="names"></param>
        /// <param name="scopeName"></param>
        /// <param name="scopeProperties"></param>
        /// <returns></returns>
        public CacheEntry[] GetCachedEntriesFromHost(int nodeId, string[] names, string scopeName, BuildPropertyGroup scopeProperties, string scopeToolsVersion, CacheContentType cacheContentType)
        {
            LocalCallDescriptorForGettingCacheEntriesFromHost callDescriptor =
                new LocalCallDescriptorForGettingCacheEntriesFromHost(names, scopeName, scopeProperties, scopeToolsVersion, cacheContentType);

            return (CacheEntry[])GetReplyForCallDescriptor(callDescriptor);
        }

        /// <summary>
        /// Send the cache entries to the parent engine
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="entries"></param>
        /// <param name="scopeName"></param>
        /// <param name="scopeProperties"></param>
        public Exception PostCacheEntriesToHost(int nodeId, CacheEntry[] entries, string scopeName, BuildPropertyGroup scopeProperties, string scopeToolsVersion, CacheContentType cacheContentType)
        {
            LocalCallDescriptorForPostingCacheEntriesToHost callDescriptor =
                new LocalCallDescriptorForPostingCacheEntriesToHost(entries, scopeName, scopeProperties, scopeToolsVersion, cacheContentType);

            return (Exception)GetReplyForCallDescriptor(callDescriptor);
        }

        /// <summary>
        /// This method is called to post the status of the node. Because status is used
        /// to report errors and to respond to inactivity notices, we use a separate queue
        /// to deliver status event to the shared memory. Otherwise status maybe be delayed
        /// if it is stuck behind a large number of other events. We also wait for the status
        /// to be sent before returning.
        /// </summary>
        public void PostStatus(int nodeId, NodeStatus nodeStatus, bool blockUntilSent)
        {
            // We should not be on the running on the callback writer thread
            ErrorUtilities.VerifyThrow(Thread.CurrentThread != writerThread, "Should never call this function from the writer thread");

            LocalCallDescriptorForPostStatus callDescriptor =
                new LocalCallDescriptorForPostStatus(nodeStatus);
            nodeHiPriCommandQueue.Enqueue(callDescriptor);

            // We need to block until the event we posted has been processed, but if the writer thread
            // exit due to an error the shared memory is no longer valid so there is no way to send the message
            while (blockUntilSent && !writerThreadHasExited && nodeHiPriCommandQueue.Count > 0)
            {
                nodeHiPriCommandQueue.QueueEmptyEvent.WaitOne(1000, false);

                // Check if the communication threads are supposed to exit
                if (exitCommunicationThreads.WaitOne(0, false))
                {
                    break;
                }
            }
        }

        #endregion

        #endregion

        #region Data
        private LocalNode localNode;
        private DualQueue<LocalCallDescriptor> nodeCommandQueue;
        private DualQueue<LocalCallDescriptor> nodeHiPriCommandQueue;
        private SharedMemory sharedMemory;
        private ManualResetEvent exitCommunicationThreads;
        private bool writerThreadHasExited;
        private Thread writerThread;

        [ThreadStatic]
        private static ManualResetEvent replyFromParentArrived;

        // K: call id; V: ReplyData class with the wait event and reply call descriptor
        private Hashtable repliesFromParent;
        #endregion
    }
}
