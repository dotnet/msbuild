// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using ILoggingService = Microsoft.Build.BackEnd.Logging.ILoggingService;
using NodeLoggingContext = Microsoft.Build.BackEnd.Logging.NodeLoggingContext;
using Microsoft.Build.BackEnd.Components.Caching;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This class represents an implementation of INode for out-of-proc nodes.
    /// </summary>
    internal class InProcNode : INode, INodePacketFactory
    {
        /// <summary>
        /// The build component host.
        /// </summary>
        private readonly IBuildComponentHost _componentHost;

        /// <summary>
        /// The environment at the time the build is started.
        /// </summary>
        private IDictionary<string, string> _savedEnvironment;

        /// <summary>
        /// The current directory at the time the build is started.
        /// </summary>
        private string _savedCurrentDirectory;

        /// <summary>
        /// The node logging context.
        /// </summary>
        private NodeLoggingContext _loggingContext;

        /// <summary>
        /// The build request engine.
        /// </summary>
        private readonly IBuildRequestEngine _buildRequestEngine;

        /// <summary>
        /// The queue of packets we have received but which have not yet been processed.
        /// </summary>
        private readonly ConcurrentQueue<INodePacket> _receivedPackets;

        /// <summary>
        /// The event which is set when we receive packets.
        /// </summary>
        private readonly AutoResetEvent _packetReceivedEvent;

        /// <summary>
        /// The event which is set when we should shut down.
        /// </summary>
        private readonly AutoResetEvent _shutdownEvent;

        /// <summary>
        /// The reason we are shutting down.
        /// </summary>
        private NodeEngineShutdownReason _shutdownReason;

        /// <summary>
        /// The exception, if any, which caused shutdown.
        /// </summary>
        private Exception _shutdownException;

        /// <summary>
        /// The node endpoint
        /// </summary>
        private readonly INodeEndpoint _nodeEndpoint;

        /// <summary>
        /// Handler for engine exceptions.
        /// </summary>
        private readonly EngineExceptionDelegate _engineExceptionEventHandler;

        /// <summary>
        /// Handler for new configuration requests.
        /// </summary>
        private readonly NewConfigurationRequestDelegate _newConfigurationRequestEventHandler;

        /// <summary>
        /// Handler for blocked request events.
        /// </summary>
        private readonly RequestBlockedDelegate _requestBlockedEventHandler;

        /// <summary>
        /// Handler for request completed events.
        /// </summary>
        private readonly RequestCompleteDelegate _requestCompleteEventHandler;

        /// <summary>
        /// Constructor.
        /// </summary>
        public InProcNode(IBuildComponentHost componentHost, INodeEndpoint inProcNodeEndpoint)
        {
            _componentHost = componentHost;
            _nodeEndpoint = inProcNodeEndpoint;
            _receivedPackets = new ConcurrentQueue<INodePacket>();
            _packetReceivedEvent = new AutoResetEvent(false);
            _shutdownEvent = new AutoResetEvent(false);

            _buildRequestEngine = componentHost.GetComponent(BuildComponentType.RequestEngine) as IBuildRequestEngine;

            _engineExceptionEventHandler = OnEngineException;
            _newConfigurationRequestEventHandler = OnNewConfigurationRequest;
            _requestBlockedEventHandler = OnNewRequest;
            _requestCompleteEventHandler = OnRequestComplete;
        }

        #region INode Members

        /// <summary>
        /// Starts up the node and processes messages until the node is requested to shut down.
        /// </summary>
        /// <param name="shutdownException">The exception which caused shutdown, if any.</param>
        /// <returns>The reason for shutting down.</returns>
        public NodeEngineShutdownReason Run(out Exception shutdownException)
        {
            try
            {
                _nodeEndpoint.OnLinkStatusChanged += OnLinkStatusChanged;
                _nodeEndpoint.Listen(this);

                var waitHandles = new WaitHandle[] { _shutdownEvent, _packetReceivedEvent };

                // Get the current directory before doing work. We need this so we can restore the directory when the node shuts down.
                _savedCurrentDirectory = NativeMethodsShared.GetCurrentDirectory();
                while (true)
                {
                    int index = WaitHandle.WaitAny(waitHandles);
                    switch (index)
                    {
                        case 0:
                            {
                                NodeEngineShutdownReason shutdownReason = HandleShutdown(out shutdownException);
                                if (_componentHost.BuildParameters.ShutdownInProcNodeOnBuildFinish)
                                {
                                    return shutdownReason;
                                }

                                break;
                            }

                        case 1:

                            while (_receivedPackets.TryDequeue(out INodePacket packet))
                            {
                                if (packet != null)
                                {
                                    HandlePacket(packet);
                                }
                            }

                            break;
                    }
                }
            }
            catch (ThreadAbortException)
            {
                // Do nothing.  This will happen when the thread is forcibly terminated because we are shutting down, for example
                // when the unit test framework terminates.
                throw;
            }
            catch (Exception e)
            {
                // Dump all engine exceptions to a temp file
                // so that we have something to go on in the
                // event of a failure
                ExceptionHandling.DumpExceptionToFile(e);

                // This is fatal: process will terminate: make sure the
                // debugger launches
                ErrorUtilities.ThrowInternalError(e.Message, e);
                throw;
            }

            // UNREACHABLE
        }

        #endregion

        #region INodePacketFactory Members

        /// <summary>
        /// Not necessary for in-proc node - we don't serialize.
        /// </summary>
        public void RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler)
        {
            // The in-proc node doesn't need to do this.
        }

        /// <summary>
        /// Not necessary for in-proc node - we don't serialize.
        /// </summary>
        public void UnregisterPacketHandler(NodePacketType packetType)
        {
            // The in-proc node doesn't need to do this.
        }

        /// <summary>
        /// Not necessary for in-proc node - we don't serialize.
        /// </summary>
        public void DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator)
        {
            // The in-proc endpoint shouldn't be serializing, just routing.
            ErrorUtilities.ThrowInternalError("Unexpected call to DeserializeAndRoutePacket on the in-proc node.");
        }

        /// <summary>
        /// Routes the packet to the appropriate handler.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <param name="packet">The packet.</param>
        public void RoutePacket(int nodeId, INodePacket packet)
        {
            _receivedPackets.Enqueue(packet);
            _packetReceivedEvent.Set();
        }

        #endregion

        /// <summary>
        /// Event handler for the BuildEngine's OnRequestComplete event.
        /// </summary>
        private void OnRequestComplete(BuildRequest request, BuildResult result)
        {
            if (_nodeEndpoint.LinkStatus == LinkStatus.Active)
            {
                _nodeEndpoint.SendData(result);
            }
        }

        /// <summary>
        /// Event handler for the BuildEngine's OnNewRequest event.
        /// </summary>
        private void OnNewRequest(BuildRequestBlocker blocker)
        {
            if (_nodeEndpoint.LinkStatus == LinkStatus.Active)
            {
                _nodeEndpoint.SendData(blocker);
            }
        }

        /// <summary>
        /// Event handler for the BuildEngine's OnNewConfigurationRequest event.
        /// </summary>
        private void OnNewConfigurationRequest(BuildRequestConfiguration config)
        {
            if (_nodeEndpoint.LinkStatus == LinkStatus.Active)
            {
                _nodeEndpoint.SendData(config);
            }
        }

        /// <summary>
        /// Event handler for the LoggingService's OnLoggingThreadException event.
        /// </summary>
        private void OnLoggingThreadException(Exception e)
        {
            OnEngineException(e);
        }

        /// <summary>
        /// Event handler for the BuildEngine's OnEngineException event.
        /// </summary>
        private void OnEngineException(Exception e)
        {
            _shutdownException = e;
            _shutdownReason = NodeEngineShutdownReason.Error;
            _shutdownEvent.Set();
        }

        /// <summary>
        /// Perform necessary actions to shut down the node.
        /// </summary>
        private NodeEngineShutdownReason HandleShutdown(out Exception exception)
        {
            // Console.WriteLine("Node shutting down with reason {0} and exception: {1}", shutdownReason, shutdownException);
            try
            {
                // Clean up the engine
                if (_buildRequestEngine != null && _buildRequestEngine.Status != BuildRequestEngineStatus.Uninitialized)
                {
                    _buildRequestEngine.CleanupForBuild();
                }
            }
            catch (Exception ex)
            {
                if (ExceptionHandling.IsCriticalException(ex))
                {
                    throw;
                }

                // If we had some issue shutting down, don't reuse the node because we may be in some weird state.
                if (_shutdownReason == NodeEngineShutdownReason.BuildCompleteReuse)
                {
                    _shutdownReason = NodeEngineShutdownReason.BuildComplete;
                }
            }

            // Dispose of any build registered objects
            IRegisteredTaskObjectCache objectCache = (IRegisteredTaskObjectCache)(_componentHost.GetComponent(BuildComponentType.RegisteredTaskObjectCache));
            objectCache.DisposeCacheObjects(RegisteredTaskObjectLifetime.Build);

            if (_shutdownReason != NodeEngineShutdownReason.BuildCompleteReuse)
            {
                // Dispose of any node registered objects.
                ((IBuildComponent)objectCache).ShutdownComponent();
            }

            if (_componentHost.BuildParameters.SaveOperatingEnvironment)
            {
                // Restore the original current directory.
                NativeMethodsShared.SetCurrentDirectory(_savedCurrentDirectory);

                // Restore the original environment.
                foreach (KeyValuePair<string, string> entry in CommunicationsUtilities.GetEnvironmentVariables())
                {
                    if (!_savedEnvironment.ContainsKey(entry.Key))
                    {
                        Environment.SetEnvironmentVariable(entry.Key, null);
                    }
                }

                foreach (KeyValuePair<string, string> entry in _savedEnvironment)
                {
                    Environment.SetEnvironmentVariable(entry.Key, entry.Value);
                }
            }

            exception = _shutdownException;

            if (_loggingContext != null)
            {
                _loggingContext.LoggingService.OnLoggingThreadException -= OnLoggingThreadException;
                _loggingContext = null;
            }

            // Notify the BuildManager that we are done.
            if (_nodeEndpoint.LinkStatus == LinkStatus.Active)
            {
                _nodeEndpoint.SendData(new NodeShutdown(_shutdownReason == NodeEngineShutdownReason.Error ? NodeShutdownReason.Error : NodeShutdownReason.Requested, exception));
            }

            _buildRequestEngine.OnEngineException -= _engineExceptionEventHandler;
            _buildRequestEngine.OnNewConfigurationRequest -= _newConfigurationRequestEventHandler;
            _buildRequestEngine.OnRequestBlocked -= _requestBlockedEventHandler;
            _buildRequestEngine.OnRequestComplete -= _requestCompleteEventHandler;

            return _shutdownReason;
        }

        /// <summary>
        /// Dispatches the packet to the correct handler.
        /// </summary>
        private void HandlePacket(INodePacket packet)
        {
            switch (packet.Type)
            {
                case NodePacketType.BuildRequest:
                    HandleBuildRequest(packet as BuildRequest);
                    break;

                case NodePacketType.BuildRequestConfiguration:
                    HandleBuildRequestConfiguration();
                    break;

                case NodePacketType.BuildRequestConfigurationResponse:
                    HandleBuildRequestConfigurationResponse(packet as BuildRequestConfigurationResponse);
                    break;

                case NodePacketType.BuildRequestUnblocker:
                    HandleBuildResult(packet as BuildRequestUnblocker);
                    break;

                case NodePacketType.NodeConfiguration:
                    HandleNodeConfiguration(packet as NodeConfiguration);
                    break;

                case NodePacketType.NodeBuildComplete:
                    HandleNodeBuildComplete(packet as NodeBuildComplete);
                    break;
            }
        }

        /// <summary>
        /// Event handler for the node endpoint's LinkStatusChanged event.
        /// </summary>
        private void OnLinkStatusChanged(INodeEndpoint endpoint, LinkStatus status)
        {
            switch (status)
            {
                case LinkStatus.ConnectionFailed:
                case LinkStatus.Failed:
                    _shutdownReason = NodeEngineShutdownReason.ConnectionFailed;
                    _shutdownEvent.Set();
                    break;

                case LinkStatus.Inactive:
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// Handles the BuildRequest packet.
        /// </summary>
        private void HandleBuildRequest(BuildRequest request)
        {
            _buildRequestEngine.SubmitBuildRequest(request);
        }

        /// <summary>
        /// Handles the BuildRequestConfiguration packet.
        /// </summary>
        private static void HandleBuildRequestConfiguration()
        {
            // Configurations are already in the cache, which we share with the BuildManager.
        }

        /// <summary>
        /// Handles the BuildRequestConfigurationResponse packet.
        /// </summary>
        private void HandleBuildRequestConfigurationResponse(BuildRequestConfigurationResponse response)
        {
            _buildRequestEngine.ReportConfigurationResponse(response);
        }

        /// <summary>
        /// Handles the BuildResult packet.
        /// </summary>
        private void HandleBuildResult(BuildRequestUnblocker unblocker)
        {
            _buildRequestEngine.UnblockBuildRequest(unblocker);
        }

        /// <summary>
        /// Handles the NodeConfiguration packet.
        /// </summary>
        private void HandleNodeConfiguration(NodeConfiguration configuration)
        {
            // Set the culture.
            CultureInfo.CurrentCulture = configuration.BuildParameters.Culture;
            CultureInfo.CurrentUICulture = configuration.BuildParameters.UICulture;

            // Snapshot the initial environment.
            _savedEnvironment = CommunicationsUtilities.GetEnvironmentVariables();

            // Save the current directory.
            _savedCurrentDirectory = NativeMethodsShared.GetCurrentDirectory();

            // Set the node id.
            _componentHost.BuildParameters.NodeId = configuration.NodeId;
            _shutdownException = null;

#if FEATURE_APPDOMAIN
            // And the AppDomainSetup
            _componentHost.BuildParameters.AppDomainSetup = configuration.AppDomainSetup;
#endif

            // Declare in-proc
            _componentHost.BuildParameters.IsOutOfProc = false;

            // Set the logging exception handler
            ILoggingService loggingService = _componentHost.LoggingService;
            loggingService.OnLoggingThreadException += OnLoggingThreadException;

            // Now prep the buildRequestEngine for the build.
            _loggingContext = new NodeLoggingContext(loggingService, configuration.NodeId, true /* inProcNode */);

            _buildRequestEngine.OnEngineException += _engineExceptionEventHandler;
            _buildRequestEngine.OnNewConfigurationRequest += _newConfigurationRequestEventHandler;
            _buildRequestEngine.OnRequestBlocked += _requestBlockedEventHandler;
            _buildRequestEngine.OnRequestComplete += _requestCompleteEventHandler;

            if (_shutdownException != null)
            {
                HandleShutdown(out Exception exception);
                throw exception;
            }

            _buildRequestEngine.InitializeForBuild(_loggingContext);
        }

        /// <summary>
        /// Handles the NodeBuildComplete packet.
        /// </summary>
        private void HandleNodeBuildComplete(NodeBuildComplete buildComplete)
        {
            _shutdownReason = buildComplete.PrepareForReuse ? NodeEngineShutdownReason.BuildCompleteReuse : NodeEngineShutdownReason.BuildComplete;
            _shutdownEvent.Set();
        }
    }
}
