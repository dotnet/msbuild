// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// This class represents an implementation of INode for out-of-proc server nodes aka MSBuild server 
    /// </summary>
    public sealed class OutOfProcServerNode : INode, INodePacketFactory, INodePacketHandler
    {
        private readonly Func<string, (int exitCode, string exitType)> _buildFunction;

        /// <summary>
        /// The endpoint used to talk to the host.
        /// </summary>
        private INodeEndpoint _nodeEndpoint = default!;

        /// <summary>
        /// The packet factory.
        /// </summary>
        private readonly NodePacketFactory _packetFactory;

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
        private readonly ManualResetEvent _shutdownEvent;

        /// <summary>
        /// The reason we are shutting down.
        /// </summary>
        private NodeEngineShutdownReason _shutdownReason;

        /// <summary>
        /// The exception, if any, which caused shutdown.
        /// </summary>
        private Exception? _shutdownException = null;

        /// <summary>
        /// Flag indicating if we should debug communications or not.
        /// </summary>
        private readonly bool _debugCommunications;

        private string _serverBusyMutexName = default!;

        public OutOfProcServerNode(Func<string, (int exitCode, string exitType)> buildFunction)
        {
            _buildFunction = buildFunction;
            new Dictionary<string, string>();
            _debugCommunications = (Environment.GetEnvironmentVariable("MSBUILDDEBUGCOMM") == "1");

            _receivedPackets = new ConcurrentQueue<INodePacket>();
            _packetReceivedEvent = new AutoResetEvent(false);
            _shutdownEvent = new ManualResetEvent(false);
            _packetFactory = new NodePacketFactory();

            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.ServerNodeBuildCommand, ServerNodeBuildCommand.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.NodeBuildComplete, NodeBuildComplete.FactoryForDeserialization, this);
        }

        #region INode Members

        /// <summary>
        /// Starts up the node and processes messages until the node is requested to shut down.
        /// Assumes no node reuse.
        /// Assumes low priority is disabled.
        /// </summary>
        /// <param name="shutdownException">The exception which caused shutdown, if any.</param>
        /// <returns>The reason for shutting down.</returns>
        public NodeEngineShutdownReason Run(out Exception? shutdownException)
        {
            return Run(false, false, out shutdownException);
        }

        /// <summary>
        /// Starts up the node and processes messages until the node is requested to shut down.
        /// Assumes low priority is disabled.
        /// </summary>
        /// <param name="enableReuse">Whether this node is eligible for reuse later.</param>
        /// <param name="shutdownException">The exception which caused shutdown, if any.</param>
        /// <returns>The reason for shutting down.</returns>
        public NodeEngineShutdownReason Run(bool enableReuse, out Exception? shutdownException)
        {
            return Run(enableReuse, false, out shutdownException);
        }

        /// <summary>
        /// Starts up the node and processes messages until the node is requested to shut down.
        /// </summary>
        /// <param name="enableReuse">Whether this node is eligible for reuse later.</param>
        /// <param name="lowPriority">Whether this node should be running with low priority.</param>
        /// <param name="shutdownException">The exception which caused shutdown, if any.</param>
        /// <returns>The reason for shutting down.</returns>
        public NodeEngineShutdownReason Run(bool enableReuse, bool lowPriority, out Exception? shutdownException)
        {
            string msBuildLocation = BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;
            var handshake = new ServerNodeHandshake(
                CommunicationsUtilities.GetHandshakeOptions(taskHost: false, lowPriority: lowPriority, is64Bit: EnvironmentUtilities.Is64BitProcess),
                msBuildLocation);

            string pipeName = NamedPipeUtil.GetPipeNameOrPath("MSBuildServer-" + handshake.ComputeHash());

            string serverRunningMutexName = $@"Global\server-running-{pipeName}";
            _serverBusyMutexName = $@"Global\server-busy-{pipeName}";

            // TODO: shall we address possible race condition. It is harmless as it, with acceptable probability, just cause unnecessary process spawning
            // and of two processes will become victim and fails, build will not be affected
            using var serverRunningMutex = ServerNamedMutex.OpenOrCreateMutex(serverRunningMutexName, out bool mutexCreatedNew);
            if (!mutexCreatedNew)
            {
                shutdownException = new InvalidOperationException("MSBuild server is already running!");
                return NodeEngineShutdownReason.Error;
            }

            _nodeEndpoint = new ServerNodeEndpointOutOfProc(pipeName, handshake);
            _nodeEndpoint.OnLinkStatusChanged += OnLinkStatusChanged;
            _nodeEndpoint.Listen(this);

            var waitHandles = new WaitHandle[] { _shutdownEvent, _packetReceivedEvent };

            // Get the current directory before doing any work. We need this so we can restore the directory when the node shutsdown.
            while (true)
            {
                int index = WaitHandle.WaitAny(waitHandles);
                switch (index)
                {
                    case 0:
                        NodeEngineShutdownReason shutdownReason = HandleShutdown(out shutdownException);
                        return shutdownReason;

                    case 1:

                        while (_receivedPackets.TryDequeue(out INodePacket? packet))
                        {
                            if (packet != null)
                            {
                                HandlePacket(packet);
                            }
                        }

                        break;
                }
            }

            // UNREACHABLE
        }

        #endregion

        #region INodePacketFactory Members

        /// <summary>
        /// Registers a packet handler.
        /// </summary>
        /// <param name="packetType">The packet type for which the handler should be registered.</param>
        /// <param name="factory">The factory used to create packets.</param>
        /// <param name="handler">The handler for the packets.</param>
        void INodePacketFactory.RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler)
        {
            _packetFactory.RegisterPacketHandler(packetType, factory, handler);
        }

        /// <summary>
        /// Unregisters a packet handler.
        /// </summary>
        /// <param name="packetType">The type of packet for which the handler should be unregistered.</param>
        void INodePacketFactory.UnregisterPacketHandler(NodePacketType packetType)
        {
            _packetFactory.UnregisterPacketHandler(packetType);
        }

        /// <summary>
        /// Deserializes and routes a packer to the appropriate handler.
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packetType">The packet type.</param>
        /// <param name="translator">The translator to use as a source for packet data.</param>
        void INodePacketFactory.DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator)
        {
            _packetFactory.DeserializeAndRoutePacket(nodeId, packetType, translator);
        }

        /// <summary>
        /// Routes a packet to the appropriate handler.
        /// </summary>
        /// <param name="nodeId">The node id from which the packet was received.</param>
        /// <param name="packet">The packet to route.</param>
        void INodePacketFactory.RoutePacket(int nodeId, INodePacket packet)
        {
            _packetFactory.RoutePacket(nodeId, packet);
        }

        #endregion

        #region INodePacketHandler Members

        /// <summary>
        /// Called when a packet has been received.
        /// </summary>
        /// <param name="node">The node from which the packet was received.</param>
        /// <param name="packet">The packet.</param>
        void INodePacketHandler.PacketReceived(int node, INodePacket packet)
        {
            _receivedPackets.Enqueue(packet);
            _packetReceivedEvent.Set();
        }

        #endregion

        /// <summary>
        /// Perform necessary actions to shut down the node.
        /// </summary>
        // TODO: it is too complicated, for simple role of server node it needs to be simplified
        private NodeEngineShutdownReason HandleShutdown(out Exception? exception)
        {
            CommunicationsUtilities.Trace("Shutting down with reason: {0}, and exception: {1}.", _shutdownReason, _shutdownException);

            exception = _shutdownException;

            if (_nodeEndpoint.LinkStatus == LinkStatus.Active)
            {
                _nodeEndpoint.OnLinkStatusChanged -= OnLinkStatusChanged;
            }

            _nodeEndpoint.Disconnect();

            CommunicationsUtilities.Trace("Shut down complete.");

            return _shutdownReason;
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

                case LinkStatus.Active:
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// Callback for logging packets to be sent.
        /// </summary>
        private void SendPacket(INodePacket packet)
        {
            if (_nodeEndpoint.LinkStatus == LinkStatus.Active)
            {
                _nodeEndpoint.SendData(packet);
            }
        }

        /// <summary>
        /// Dispatches the packet to the correct handler.
        /// </summary>
        private void HandlePacket(INodePacket packet)
        {
            switch (packet.Type)
            {
                case NodePacketType.ServerNodeBuildCommand:
                    HandleServerNodeBuildCommand((ServerNodeBuildCommand)packet);
                    break;
                case NodePacketType.NodeBuildComplete:
                    HandleNodeBuildComplete((NodeBuildComplete)packet);
                    break;
            }
        }

        private void HandleServerNodeBuildCommand(ServerNodeBuildCommand command)
        {
            using var serverBusyMutex = ServerNamedMutex.OpenOrCreateMutex(name: _serverBusyMutexName, createdNew: out var holdsMutex);
            if (!holdsMutex)
            {
                // Client must have send request message to server even though serer is busy.
                // It is not a race condition, as client exclusivity is also guaranteed by name pipe which allows only one client to connect.
                _shutdownException = new InvalidOperationException("Client requested build while server is busy processing previous client build request.");
                _shutdownReason = NodeEngineShutdownReason.Error;
                _shutdownEvent.Set();
            }

            // set build process context
            Directory.SetCurrentDirectory(command.StartupDirectory);
            CommunicationsUtilities.SetEnvironment(command.BuildProcessEnvironment);
            Thread.CurrentThread.CurrentCulture = command.Culture;
            Thread.CurrentThread.CurrentUICulture = command.UICulture;

            // configure console output redirection
            var oldOut = Console.Out;
            var oldErr = Console.Error;
            (int exitCode, string exitType) buildResult;

            // Dispose must be called before the server sends response packet
            using (var outWriter = RedirectConsoleWriter.Create(text => SendPacket(new ServerNodeConsoleWrite(text, 1))))
            using (var errWriter = RedirectConsoleWriter.Create(text => SendPacket(new ServerNodeConsoleWrite(text, 2))))
            {
                Console.SetOut(outWriter);
                Console.SetError(errWriter);

                buildResult = _buildFunction(command.CommandLine);

                Console.SetOut(oldOut);
                Console.SetError(oldErr);
            }
          
            // On Windows, a process holds a handle to the current directory,
            // so reset it away from a user-requested folder that may get deleted.
            NativeMethodsShared.SetCurrentDirectory(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory);

            var response = new ServerNodeBuildResult(buildResult.exitCode, buildResult.exitType);
            SendPacket(response);

            _shutdownReason = NodeEngineShutdownReason.BuildCompleteReuse;
            _shutdownEvent.Set();
        }

        internal sealed class RedirectConsoleWriter : StringWriter
        {
            private readonly Action<string> _writeCallback;
            private readonly Timer _timer;
            private readonly TextWriter _syncWriter;

            private RedirectConsoleWriter(Action<string> writeCallback)
            {
                _writeCallback = writeCallback;
                _syncWriter = Synchronized(this);
                _timer = new Timer(TimerCallback, null, 0, 200);
            }

            public static TextWriter Create(Action<string> writeCallback)
            {
                RedirectConsoleWriter writer = new(writeCallback);
                return writer._syncWriter;
            }

            private void TimerCallback(object? state)
            {
                if (GetStringBuilder().Length > 0)
                {
                    _syncWriter.Flush();
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _timer.Dispose();
                    Flush();
                }

                base.Dispose(disposing);
            }

            public override void Flush()
            {
                var sb = GetStringBuilder();
                var captured = sb.ToString();
                sb.Clear();
                _writeCallback(captured);

                base.Flush();
            }
        }

        /// <summary>
        /// Handles the NodeBuildComplete packet.
        /// </summary>
        private void HandleNodeBuildComplete(NodeBuildComplete buildComplete)
        {
            _shutdownReason = buildComplete.PrepareForReuse ? NodeEngineShutdownReason.BuildCompleteReuse : NodeEngineShutdownReason.BuildComplete;
            _shutdownEvent.Set();
        }

        internal sealed class ServerNamedMutex : IDisposable
        {
            public readonly Mutex _serverMutex;

            public bool IsDisposed { get; private set; }

            public bool IsLocked { get; private set; }

            public ServerNamedMutex(string mutexName, out bool createdNew)
            {
                _serverMutex = new Mutex(
                    initiallyOwned: true,
                    name: mutexName,
                    createdNew: out createdNew);

                if (createdNew)
                {
                    IsLocked = true;
                }
            }

            internal static ServerNamedMutex OpenOrCreateMutex(string name, out bool createdNew)
            {
                // TODO: verify it is not needed anymore
                // if (PlatformInformation.IsRunningOnMono)
                // {
                //     return new ServerFileMutexPair(name, initiallyOwned: true, out createdNew);
                // }
                // else

                return new ServerNamedMutex(name, out createdNew);
            }

            public static bool WasOpen(string mutexName)
            {
                bool result = Mutex.TryOpenExisting(mutexName, out Mutex? mutex);
                mutex?.Dispose();

                return result;
            }

            public bool TryLock(int timeoutMs)
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException(nameof(ServerNamedMutex));
                }

                if (IsLocked)
                {
                    throw new InvalidOperationException("Lock already held");
                }

                return IsLocked = _serverMutex.WaitOne(timeoutMs);
            }

            public void Dispose()
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;

                try
                {
                    if (IsLocked)
                    {
                        _serverMutex.ReleaseMutex();
                    }
                }
                finally
                {
                    _serverMutex.Dispose();
                    IsLocked = false;
                }
            }
        }
    }
}
