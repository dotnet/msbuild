// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    /// <summary>
    /// Implements a persistent node for the ResolveAssemblyReferences task.
    /// </summary>
    public class OutOfProcRarNode
    {
        private static readonly ServerNodeHandshake _handshake = new(HandshakeOptions.None);

        private readonly string _pipeName;

        public OutOfProcRarNode()
        {
            // SYNC: src\Build\BackEnd\Components\Communications\RarNodeLauncher.cs
            _pipeName = NamedPipeUtil.GetPlatformSpecificPipeName($"MSBuildRarNode-{_handshake.ComputeHash()}");
        }

        /// <summary>
        /// Starts the node and begins processing RAR execution requests until cancelled.
        /// </summary>
        /// <param name="shutdownException">The exception which caused shutdown, if any.</param>
        /// <param name="cancellationToken">A cancellation token to observe while running the node loop.</param>
        /// <returns></returns>
        public RarNodeShutdownReason Run(out Exception? shutdownException, CancellationToken cancellationToken = default)
        {
            RarNodeShutdownReason shutdownReason;
            shutdownException = null;

            try
            {
                shutdownReason = RunInternal(cancellationToken);
            }
            catch (UnauthorizedAccessException)
            {
                // Access to the path is denied if named pipe already exists.
                shutdownException = new InvalidOperationException("RAR node is already running.");
                shutdownReason = RarNodeShutdownReason.AlreadyRunning;
            }
            catch (OperationCanceledException)
            {
                // Cancellation is considered an intentional shutdown of the node.
                shutdownReason = RarNodeShutdownReason.Complete;
            }
            catch (Exception ex)
            {
                shutdownException = ex;
                shutdownReason = RarNodeShutdownReason.Error;
            }

            CommunicationsUtilities.Trace("Shutting down with reason: {0}, and exception: {1}.", shutdownReason, shutdownException);

            return shutdownReason;
        }

        private RarNodeShutdownReason RunInternal(CancellationToken cancellationToken)
        {
            CommunicationsUtilities.Trace("Starting new server node with handshake {0}", _handshake);

            using NamedPipeServerStream pipeServer = CommunicationsUtilities.CreateSecurePipeServer(_pipeName);

            LinkStatus linkStatus = LinkStatus.Inactive;

            // Run until no builds have been requested in the timeout period.
            while (linkStatus != LinkStatus.ConnectionFailed)
            {
                linkStatus = WaitForConnection();

                if (linkStatus == LinkStatus.Active)
                {
#if NETCOREAPP
                    if (OperatingSystem.IsWindows())
#endif
                    {
                        pipeServer.WaitForPipeDrain();
                    }
                }

                // TODO: Requests will be processed and executed at this stage.
                pipeServer.Disconnect();
            }

            return RarNodeShutdownReason.ConnectionTimedOut;

            // Unblock the parent thread if cancellation is requested. Cancellation is only expected when MSBuild is
            // gracefully shutting down or running in unit tests.
            LinkStatus WaitForConnection()
            {
                Task<LinkStatus> linkStatusTask = Task.Run(
                    () => CommunicationsUtilities.WaitForConnection(pipeServer, _handshake));
                linkStatusTask.Wait(cancellationToken);

                return linkStatusTask.GetAwaiter().GetResult();
            }
        }
    }
}
