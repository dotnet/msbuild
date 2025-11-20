// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    /// <summary>
    /// Implements a persistent node for the ResolveAssemblyReferences task.
    /// This manages the lifecycle of the multi-instance pipe server which executes RAR requests
    /// and does not invoke the task itself.
    /// </summary>
    public sealed class OutOfProcRarNode
    {
        private readonly OutOfProcRarNodeEndpoint.SharedConfig _config;

        public OutOfProcRarNode()
            : this(Environment.ProcessorCount)
        {
        }

        public OutOfProcRarNode(int maxNumberOfConcurrentTasks)
            => _config = OutOfProcRarNodeEndpoint.CreateConfig(maxNumberOfConcurrentTasks);

        /// <summary>
        /// Starts the node and begins processing RAR execution requests until cancelled.
        /// </summary>
        /// <param name="shutdownException">The exception which caused shutdown, if any.</param>
        /// <param name="cancellationToken">A cancellation token to observe while running the node loop.</param>
        /// <returns>The reason for the node shutdown.</returns>
        public RarNodeShutdownReason Run(out Exception? shutdownException, CancellationToken cancellationToken = default)
        {
            RarNodeShutdownReason shutdownReason;
            shutdownException = null;

            try
            {
                shutdownReason = RunNodeAsync(cancellationToken).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // Consider cancellation as an intentional shutdown of the node.
                shutdownReason = RarNodeShutdownReason.Complete;
            }
            catch (UnauthorizedAccessException ex)
            {
                // Access to the path is denied if the named pipe already exists or is owned by a different user.
                shutdownException = new InvalidOperationException("RAR node is already running.", ex);
                shutdownReason = RarNodeShutdownReason.AlreadyRunning;
            }
            catch (Exception ex)
            {
                shutdownException = ex;
                shutdownReason = RarNodeShutdownReason.Error;
            }

            if (shutdownException == null)
            {
                CommunicationsUtilities.Trace("Shutting down with reason: {0}");
            }
            else
            {
                CommunicationsUtilities.Trace("Shutting down with reason: {0}, and exception: {1}", shutdownReason, shutdownException);
            }

            return shutdownReason;
        }

        private async Task<RarNodeShutdownReason> RunNodeAsync(CancellationToken cancellationToken)
        {
            // The RAR node uses two sets of pipe servers:
            // 1. A single instance pipe to manage the lifecycle of the node.
            // 2. A multi-instance pipe to execute concurrent RAR requests.
            // Because multi-instance pipes can live across multiple processes, we can't rely on the instance cap to preven
            // multiple nodes from running in the event of a race condition.
            // This also simplifies tearing down all active pipe servers when shutdown is requested.
            using NodePipeServer pipeServer = new(NamedPipeUtil.GetRarNodePipeName(_config.Handshake), _config.Handshake);

            NodePacketFactory packetFactory = new();
            packetFactory.RegisterPacketHandler(NodePacketType.NodeBuildComplete, NodeBuildComplete.FactoryForDeserialization, null);
            pipeServer.RegisterPacketFactory(packetFactory);

            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Task nodeEndpointTasks = Task.Run(() => RunNodeEndpointsAsync(linkedCts.Token), linkedCts.Token);

            // Run any static initializers which will add latency to the first task run.
            RarTaskParameters.Init();

            while (!cancellationToken.IsCancellationRequested)
            {
                LinkStatus linkStatus = await pipeServer.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                if (linkStatus == LinkStatus.Active)
                {
                    NodeBuildComplete buildComplete = (NodeBuildComplete)pipeServer.ReadPacket();

                    if (!buildComplete.PrepareForReuse)
                    {
                        break;
                    }
                }

                pipeServer.Disconnect();
            }

            // Gracefully shutdown the node endpoints.
            linkedCts.Cancel();

            try
            {
                await nodeEndpointTasks;
            }
            catch (OperationCanceledException)
            {
                // Ignore since cancellation is expected.
            }

            return RarNodeShutdownReason.Complete;
        }

        private async Task RunNodeEndpointsAsync(CancellationToken cancellationToken)
        {
            OutOfProcRarNodeEndpoint[] endpoints = new OutOfProcRarNodeEndpoint[_config.MaxNumberOfServerInstances];

            // Validate all endpoint pipe handles successfully initialize before running any read loops.
            // This allows us to bail out in the event where we can't control every pipe instance.
            for (int i = 0; i < endpoints.Length; i++)
            {
                endpoints[i] = new OutOfProcRarNodeEndpoint(endpointId: i + 1, _config);
            }

            Task[] endpointTasks = new Task[endpoints.Length];

            for (int i = 0; i < endpoints.Length; i++)
            {
                // Avoid capturing the indexer in the closure.
                OutOfProcRarNodeEndpoint endpoint = endpoints[i];
                endpointTasks[i] = Task.Run(() => endpoint.RunAsync(cancellationToken), cancellationToken);
            }

            CommunicationsUtilities.Trace("{0} RAR endpoints started.", endpoints.Length);

            await Task.WhenAll(endpointTasks);

            foreach (OutOfProcRarNodeEndpoint endpoint in endpoints)
            {
                endpoint.Dispose();
            }

            CommunicationsUtilities.Trace("All endpoints successfully stopped. Exiting.");
        }
    }
}
