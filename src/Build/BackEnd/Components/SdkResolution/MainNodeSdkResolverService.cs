// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd.Components.Logging;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// An implementation of <see cref="ISdkResolverService"/> that is hosted in the main node for multi-proc builds.  This instance of the service
    /// listens for requests from out-of-proc nodes so that SDK resolution is handled in a central location.  This instance is registered in <see cref="BuildComponentFactoryCollection.RegisterDefaultFactories"/>
    /// and can be overridden for different contexts.  This service calls the <see cref="SdkResolverService"/> to do any actual SDK resolution
    /// because the <see cref="SdkResolverService"/> is used for stand-alone evaluations where there is no build context available so caching
    /// is not an option.
    ///
    /// Since this object is a registered <see cref="IBuildComponent"/>, it is a singleton for the main process.  To get an instance of it, you
    /// must have access to an <see cref="IBuildComponentHost"/> and call <see cref="IBuildComponentHost.GetComponent"/> and pass <see cref="BuildComponentType.SdkResolverService"/>.
    /// </summary>
    internal sealed class MainNodeSdkResolverService : HostedSdkResolverServiceBase
    {
        /// <summary>
        /// An object used for locking in this class instance.
        /// </summary>
        private readonly object _lockObject = new object();

        /// <summary>
        /// A <see cref="Task"/> running in the background which handles requests from remote nodes.
        /// </summary>
        private Task _requestHandler;

        /// <summary>
        /// An event which is signaled when a request is received from a remote host.
        /// </summary>
        private ManualResetEvent _requestReceivedEvent;

        /// <summary>
        /// A list of requests from remote hosts which need to be processed.
        /// </summary>
        private ConcurrentQueue<SdkResolverRequest> _requests;

        private readonly ISdkResolverService _cachedSdkResolver = new CachingSdkResolverService();

        /// <summary>
        /// A factory which is registered to create an instance of this class.
        /// </summary>
        public static IBuildComponent CreateComponent(BuildComponentType type)
        {
            return new MainNodeSdkResolverService();
        }

        /// <inheritdoc cref="ISdkResolverService.ClearCache"/>
        public override void ClearCache(int submissionId)
        {
            _cachedSdkResolver.ClearCache(submissionId);
        }

        public override void ClearCaches()
        {
            _cachedSdkResolver.ClearCaches();
        }

        /// <inheritdoc cref="INodePacketHandler.PacketReceived"/>
        public override void PacketReceived(int node, INodePacket packet)
        {
            switch (packet.Type)
            {
                case NodePacketType.ResolveSdkRequest:
                    HandleRequest(node, packet as SdkResolverRequest);
                    break;
            }
        }

        /// <inheritdoc cref="ISdkResolverService.ResolveSdk"/>
        public override SdkResult ResolveSdk(int submissionId, SdkReference sdk, LoggingContext loggingContext, ElementLocation sdkReferenceLocation, string solutionPath, string projectPath)
        {
            ErrorUtilities.VerifyThrowInternalNull(sdk, nameof(sdk));
            ErrorUtilities.VerifyThrowInternalNull(loggingContext, nameof(loggingContext));
            ErrorUtilities.VerifyThrowInternalNull(sdkReferenceLocation, nameof(sdkReferenceLocation));
            ErrorUtilities.VerifyThrowInternalLength(projectPath, nameof(projectPath));

            return _cachedSdkResolver.ResolveSdk(submissionId, sdk, loggingContext, sdkReferenceLocation, solutionPath, projectPath);
        }

        /// <summary>
        /// Handles a request from a remote node.
        /// </summary>
        /// <param name="node">The ID of the remote node.</param>
        /// <param name="request">The <see cref="SdkResolverRequest"/> containing information about the SDK to resolve.</param>
        /// <remarks>This method must not directly handle requests because it would block requests from other nodes.  Instead, it simply
        /// adds requests to a queue which are processed by a background thread.</remarks>
        private void HandleRequest(int node, SdkResolverRequest request)
        {
            if (_requestHandler == null)
            {
                // Start the background thread which will process queued requests if it has not already been started.
                lock (_lockObject)
                {
                    if (_requestHandler == null)
                    {
                        // Create the event used to signal that a request was received
                        _requestReceivedEvent = new ManualResetEvent(initialState: false);

                        // Create the queue used to store requests that need to be processed
                        _requests = new ConcurrentQueue<SdkResolverRequest>();

                        // Create the thread which processes requests
                        _requestHandler = Task.Factory.StartNew(RequestHandlerPumpProc, TaskCreationOptions.LongRunning);
                        
                    }
                }
            }

            // Associate the node with the request
            request.NodeId = node;

            _requests.Enqueue(request);

            // Signal that one or more requests have been received
            _requestReceivedEvent.Set();
        }

        /// <summary>
        /// Processes all requests that are currently in the queue.
        /// </summary>
        private void ProcessRequests()
        {
            // Store a list of threads which are resolving SDKs
            List<Task> tasks = new List<Task>(_requests.Count);

            SdkResolverRequest item;

            while (_requests.TryDequeue(out item))
            {
                SdkResolverRequest request = item;

                // Start a thread to resolve an SDK and add it to the list of threads
                tasks.Add(Task.Run(() =>
                {
                    SdkResult response = null;
                    try
                    {
                        // Create an SdkReference from the request
                        SdkReference sdkReference = new SdkReference(request.Name, request.Version, request.MinimumVersion);

                        ILoggingService loggingService = Host.GetComponent(BuildComponentType.LoggingService) as ILoggingService;

                        // This call is usually cached so is very fast but can take longer for a new SDK that is downloaded.  Other queued threads for different SDKs will complete sooner and continue on which unblocks evaluations
                        response = ResolveSdk(request.SubmissionId, sdkReference, new EvaluationLoggingContext(loggingService, request.BuildEventContext, request.ProjectPath), request.ElementLocation, request.SolutionPath, request.ProjectPath);
                    }
                    catch (Exception e)
                    {
                        ILoggingService loggingService = Host.GetComponent(BuildComponentType.LoggingService) as ILoggingService;

                        EvaluationLoggingContext loggingContext = new EvaluationLoggingContext(loggingService, request.BuildEventContext, request.ProjectPath);

                        loggingService.LogFatalBuildError(loggingContext.BuildEventContext, e, new BuildEventFileInfo(request.ElementLocation));
                    }
                    finally
                    {
                        // Get the node manager and send the response back to the node that requested the SDK
                        INodeManager nodeManager = Host.GetComponent(BuildComponentType.NodeManager) as INodeManager;

                        nodeManager.SendData(request.NodeId, response);
                    }
                }));
            }

            // Wait for all tasks to complete
            Task.WaitAll(tasks.ToArray());
        }

        /// <summary>
        /// A background thread that waits for requests to be received.
        /// </summary>
        private void RequestHandlerPumpProc()
        {
            try
            {
                while (true)
                {
                    WaitHandle[] handles = new WaitHandle[] { ShutdownEvent, _requestReceivedEvent };

                    int waitId = WaitHandle.WaitAny(handles);
                    switch (waitId)
                    {
                        case 0:
                            return;

                        case 1:
                            _requestReceivedEvent.Reset();

                            ProcessRequests();
                            break;

                        default:
                            ErrorUtilities.ThrowInternalError("waitId {0} out of range.", waitId);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                ExceptionHandling.DumpExceptionToFile(e);
                throw;
            }
        }
    }
}
