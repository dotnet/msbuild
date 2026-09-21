// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Execution;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class NodeManager_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task CacheRepliesRemainRoutableDuringNodeMapGrowthAndRemoval()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using RoutingHarness test = new();
        int node = test.CreateNodes(128)[127].NodeId;
        TaskCachePacket reply = new(NodePacketType.TaskCacheResponse);
        using CancellationTokenSource stop = new();
        using CountdownEvent ready = new(2);
        using ManualResetEventSlim start = new();
        Task[] readers = new Task[2];
        for (int i = 0; i < readers.Length; i++)
        {
            readers[i] = Task.Factory.StartNew(() =>
            {
                ready.Signal();
                start.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                while (!stop.IsCancellationRequested)
                {
                    test.Manager.SendData(node, reply);
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        try
        {
            ready.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            start.Set();
            SpinWait.SpinUntil(() => test.Provider.SentCount > 0, TimeSpan.FromSeconds(10)).ShouldBeTrue();
            for (int batch = 0; batch < 1024; batch++)
            {
                IList<NodeInfo> added = test.CreateNodes(32);
                for (int i = 0; i < 8; i++)
                {
                    test.Manager.RoutePacket(added[i].NodeId, new NodeShutdown(NodeShutdownReason.Requested));
                }
            }
        }
        finally
        {
            stop.Cancel();
            start.Set();
            await Task.WhenAll(readers);
        }
        test.Provider.SentCount.ShouldBeGreaterThan(0);
        test.Manager.SendData(node, reply);
    }

    [Fact]
    public async Task ProviderSendDoesNotBlockConcurrentClearAndRouteRecreation()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using RoutingHarness test = new();
        int node = test.CreateNodes(1)[0].NodeId;
        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();
        test.Provider.OnSend = (_, _) =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        };
        Task send = Task.Run(() => test.Manager.SendData(node, new TaskCachePacket(NodePacketType.TaskCacheResponse)));
        Task? mutation = null;
        try
        {
            entered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            mutation = Task.Run(() =>
            {
                test.Manager.ClearPerBuildState();
                test.CreateNodes(1)[0].NodeId.ShouldBe(node);
            });
            mutation.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
        }
        finally
        {
            release.Set();
            await send;
            if (mutation is not null)
            {
                await mutation;
            }
        }
        test.Manager.SendData(node, new TaskCachePacket(NodePacketType.TaskCacheResponse));
        test.Provider.SentCount.ShouldBe(2);
    }

    [Fact]
    public async Task ProviderCreationCanWaitForAnIndependentReply()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using RoutingHarness test = new();
        int node = test.CreateNodes(1)[0].NodeId;
        Task? reply = null;
        test.Provider.OnCreate = () =>
        {
            reply = Task.Run(() => test.Manager.SendData(node, new TaskCachePacket(NodePacketType.TaskCacheResponse)));
            reply.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
        };
        try
        {
            test.CreateNodes(1).Count.ShouldBe(1);
        }
        finally
        {
            if (reply is not null)
            {
                await reply;
            }
        }
        test.Provider.SentCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShutdownRemovesRouteBeforeCallingReentrantHandler(bool deserialize)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using RoutingHarness test = new();
        int node = test.CreateNodes(1)[0].NodeId;
        Task<IList<NodeInfo>>? creation = null;
        test.Manager.RegisterPacketHandler(NodePacketType.NodeShutdown, NodeShutdown.FactoryForDeserialization,
            new PacketHandler((_, _) =>
            {
                creation = Task.Run(() => test.CreateNodes(1));
                creation.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            }));
        NodeShutdown shutdown = new(NodeShutdownReason.Requested);
        try
        {
            if (deserialize)
            {
                using MemoryStream writeStream = new();
                using (ITranslator writer = BinaryTranslator.GetWriteTranslator(writeStream))
                {
                    shutdown.Translate(writer);
                }
                byte[] bytes = writeStream.ToArray();
                using MemoryStream readStream = new(bytes, 0, bytes.Length, writable: false, publiclyVisible: true);
                using ITranslator reader = BinaryTranslator.GetReadTranslator(readStream, InterningBinaryReader.PoolingBuffer);
                test.Manager.DeserializeAndRoutePacket(node, NodePacketType.NodeShutdown, reader);
            }
            else
            {
                test.Manager.RoutePacket(node, shutdown);
            }
        }
        finally
        {
            if (creation is not null)
            {
                (await creation)[0].NodeId.ShouldBe(node);
            }
        }
        _ = creation.ShouldNotBeNull();
        test.Manager.SendData(node, new TaskCachePacket(NodePacketType.TaskCacheResponse));
        test.Provider.SentCount.ShouldBe(1);
    }

    [Fact]
    public void DuplicateNodeIdsStillFailWithoutReplacingTheRoute()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        using RoutingHarness test = new();
        int node = test.CreateNodes(1)[0].NodeId;
        test.Provider.ForcedNodeId = node;
        Should.Throw<ArgumentException>(() => test.CreateNodes(1));
        test.Manager.SendData(node, new TaskCachePacket(NodePacketType.TaskCacheResponse));
        test.Provider.SentCount.ShouldBe(1);
    }

    private sealed class RoutingHarness : IBuildComponentHost, IDisposable
    {
        internal NodeManager Manager { get; }
        internal FakeProvider Provider { get; } = new();
        public BuildParameters BuildParameters { get; } = new() { DisableInProcNode = true };
        public string Name => nameof(NodeManager_Tests);
        public LegacyThreadingData LegacyThreadingData => throw new NotSupportedException();
        public ILoggingService LoggingService => throw new NotSupportedException();

        internal RoutingHarness()
        {
            Manager = (NodeManager)NodeManager.CreateComponent(BuildComponentType.NodeManager);
            Manager.InitializeComponent(this);
            Manager.RegisterPacketHandler(NodePacketType.NodeShutdown, NodeShutdown.FactoryForDeserialization, new PacketHandler((_, _) => { }));
        }

        internal IList<NodeInfo> CreateNodes(int count)
        {
            NodeConfiguration configuration = new(0, BuildParameters, [],
#if FEATURE_APPDOMAIN
                null,
#endif
                default);
            return Manager.CreateNodes(configuration, NodeAffinity.OutOfProc, count);
        }

        public IBuildComponent GetComponent(BuildComponentType type) => type switch
        {
            BuildComponentType.InProcNodeProvider or BuildComponentType.OutOfProcNodeProvider => Provider,
            _ => throw new NotSupportedException(),
        };
        public TComponent GetComponent<TComponent>(BuildComponentType type) where TComponent : IBuildComponent => (TComponent)GetComponent(type);
        public void RegisterFactory(BuildComponentType factoryType, BuildComponentFactoryDelegate factory) => throw new NotSupportedException();
        public void Dispose() => Manager.ShutdownComponent();
    }

    private sealed class FakeProvider : INodeProvider
    {
        private int _sent;
        internal int SentCount => Volatile.Read(ref _sent);
        internal Action<int, INodePacket>? OnSend { get; set; }
        internal Action? OnCreate { get; set; }
        internal int? ForcedNodeId { get; set; }
        public NodeProviderType ProviderType => NodeProviderType.OutOfProc;
        public int AvailableNodes => int.MaxValue;

        public IList<NodeInfo> CreateNodes(int nextNodeId, INodePacketFactory packetFactory,
            Func<NodeInfo, NodeConfiguration> configurationFactory, int numberOfNodesToCreate)
        {
            OnCreate?.Invoke();
            List<NodeInfo> nodes = new(numberOfNodesToCreate);
            for (int i = 0; i < numberOfNodesToCreate; i++)
            {
                NodeInfo node = new(ForcedNodeId ?? nextNodeId + i, ProviderType);
                configurationFactory(node).NodeId.ShouldBe(node.NodeId);
                nodes.Add(node);
            }
            return nodes;
        }
        public void SendData(int node, INodePacket packet)
        {
            Interlocked.Increment(ref _sent);
            OnSend?.Invoke(node, packet);
        }
        public void InitializeComponent(IBuildComponentHost host) { }
        public void ShutdownComponent() { }
        public void ShutdownConnectedNodes(bool enableReuse) { }
        public void ShutdownAllNodes() { }
        public IEnumerable<Process> GetProcesses() => [];
    }

    private sealed class PacketHandler(Action<int, INodePacket> received) : INodePacketHandler
    {
        public void PacketReceived(int node, INodePacket packet) => received(node, packet);
    }
}
