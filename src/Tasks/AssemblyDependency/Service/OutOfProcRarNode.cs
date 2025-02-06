// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO.Pipes;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;

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
            _pipeName = $"MSBuildRarNode-{_handshake.ComputeHash()}";
        }

        /// <summary>
        /// Starts the node and begins processing RAR execution requests until cancelled.
        /// </summary>
        /// <param name="shutdownException"></param>
        /// <returns></returns>
        public RarNodeShutdownReason Run(out Exception? shutdownException)
        {
            RarNodeShutdownReason shutdownReason;
            shutdownException = null;

            try
            {
                shutdownReason = RunInternal();
            }
            catch (UnauthorizedAccessException)
            {
                // Access to the path is denied if named pipe already exists.
                shutdownException = new InvalidOperationException("RAR node is already running.");
                shutdownReason = RarNodeShutdownReason.AlreadyRunning;
            }
            catch (Exception ex)
            {
                shutdownException = ex;
                shutdownReason = RarNodeShutdownReason.Error;
            }

            CommunicationsUtilities.Trace("Shutting down with reason: {0}, and exception: {1}.", shutdownReason, shutdownException);

            return shutdownReason;
        }

        private RarNodeShutdownReason RunInternal()
        {
            CommunicationsUtilities.Trace("Starting new server node with handshake {0}", _handshake);

            using NamedPipeServerStream pipeServer = CommunicationsUtilities.CreateSecurePipeServer(_pipeName);

            LinkStatus linkStatus = LinkStatus.Inactive;

            // Run until no builds have been requested in the timeout period.
            while (linkStatus != LinkStatus.ConnectionFailed)
            {
                linkStatus = CommunicationsUtilities.WaitForConnection(pipeServer, _handshake);

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
        }
    }
}
