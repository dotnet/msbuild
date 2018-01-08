// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// An implementation of <see cref="ISdkResolverService"/> that is hosted in an out-of-proc node for multi-proc builds.  This instance of the service
    /// sends requests to the main node that SDK resolution is handled in a central location.  This instance is registered in <see cref="Microsoft.Build.Execution.OutOfProcNode"/>
    /// using a factory so that parameters can be passed to the constructor.  This service caches responses for a given build so that it can avoid sending
    /// a packet where possible.  The cache is always in effect here because the out-of-proc node is only used for builds.
    /// 
    /// Since this object is a registered <see cref="IBuildComponent"/>, it is a singleton for the main process.  To get an instance of it, you
    /// must have access to an <see cref="IBuildComponentHost"/> and call <see cref="IBuildComponentHost.GetComponent"/> and pass <see cref="BuildComponentType.SdkResolverService"/>.
    /// </summary>
    internal sealed class OutOfProcNodeSdkResolverService : HostedSdkResolverServiceBase
    {
        /// <summary>
        /// The cache of responses which is cleared between builds.
        /// </summary>
        private readonly ConcurrentDictionary<string, SdkResolverResponse> _responseCache = new ConcurrentDictionary<string, SdkResolverResponse>(MSBuildNameIgnoreCaseComparer.Default);

        /// <summary>
        /// An event to signal when a response has been received.
        /// </summary>
        private readonly AutoResetEvent _responseReceivedEvent = new AutoResetEvent(initialState: false);

        /// <summary>
        /// An object used to store the last response from a remote node.  Since evaluation is single threaded, this object is only set one at a time.
        /// </summary>
        private volatile SdkResolverResponse _lastResponse;

        /// <summary>
        /// Initializes a new instance of the OutOfProcNodeSdkResolverService class.
        /// </summary>
        /// <param name="sendPacket">A <see cref="Action{INodePacket}"/> to use when sending packets to the main node.</param>
        public OutOfProcNodeSdkResolverService(Action<INodePacket> sendPacket)
        {
            ErrorUtilities.VerifyThrowArgumentNull(sendPacket, nameof(sendPacket));

            SendPacket = sendPacket;
        }

        /// <inheritdoc cref="INodePacketHandler.PacketReceived"/>
        public override void PacketReceived(int node, INodePacket packet)
        {
            switch (packet.Type)
            {
                case NodePacketType.ResolveSdkResponse:
                    HandleResponse(packet as SdkResolverResponse);
                    break;
            }
        }

        /// <inheritdoc cref="ISdkResolverService.ResolveSdk"/>
        public override string ResolveSdk(int submissionId, SdkReference sdk, LoggingContext loggingContext, ElementLocation sdkReferenceLocation, string solutionPath, string projectPath)
        {
            // Get a cached response if possible, otherwise send the request
            SdkResolverResponse response = _responseCache.GetOrAdd(
                sdk.Name,
                key => RequestSdkPathFromMainNode(submissionId, sdk, loggingContext, sdkReferenceLocation, solutionPath, projectPath));

            if (!SdkResolverService.IsReferenceSameVersion(sdk, response.Version))
            {
                // MSB4240: Multiple versions of the same SDK "{0}" cannot be specified. The SDK version already specified at "{1}" will be used and the version will be "{2}" ignored.
                loggingContext.LogWarning(null, new BuildEventFileInfo(sdkReferenceLocation), "ReferencingMultipleVersionsOfTheSameSdk", sdk.Name, response.ElementLocation, sdk.Version);
            }

            return response.FullPath;
        }

        /// <inheritdoc cref="IBuildComponent.ShutdownComponent"/>
        public override void ShutdownComponent()
        {
            base.ShutdownComponent();

            // Clear the response cache
            _responseCache.Clear();
        }

        /// <summary>
        /// Handles a response from the main node.
        /// </summary>
        /// <param name="response"></param>
        private void HandleResponse(SdkResolverResponse response)
        {
            // Store the last response so the awaiting thread can use it
            _lastResponse = response;

            // Signal that a response has been received
            _responseReceivedEvent.Set();
        }

        private SdkResolverResponse RequestSdkPathFromMainNode(int submissionId, SdkReference sdk, LoggingContext loggingContext, ElementLocation sdkReferenceLocation, string solutionPath, string projectPath)
        {
            // Clear out the last response for good measure
            _lastResponse = null;

            // Create the SdkResolverRequest packet to send
            INodePacket packet = SdkResolverRequest.Create(submissionId, sdk, loggingContext.BuildEventContext, sdkReferenceLocation, solutionPath, projectPath);

            SendPacket(packet);

            // Wait for either the response or a shutdown event.  Either event means this thread should return
            WaitHandle.WaitAny(new WaitHandle[] {_responseReceivedEvent, ShutdownEvent});

            // Keep track of the element location of the reference
            _lastResponse.ElementLocation = sdkReferenceLocation;

            // Return the response which was set by another thread.  In the case of shutdown, it should be null.
            return _lastResponse;
        }
    }
}
