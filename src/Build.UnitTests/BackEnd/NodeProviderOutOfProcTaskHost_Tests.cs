// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Build.BackEnd;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for the lifetime of <see cref="NodeProviderOutOfProcTaskHost"/>.
    /// </summary>
    /// <remarks>
    /// A task host launched with node reuse stays connected to the process that launched it, so its
    /// connection outlives the build. A worker node builds a fresh component collection for every
    /// build it serves, so the object holding those connections has to be scoped to the process
    /// rather than the build; otherwise each build forgets the task hosts the previous one started,
    /// and those task hosts are left running, unreachable by their launcher and unclaimable by
    /// anyone else because a task host pipe accepts a single connection.
    /// </remarks>
    public class NodeProviderOutOfProcTaskHost_Tests
    {
        [Fact]
        public void CreateProcessWideComponent_ReturnsTheSameInstanceEveryTime()
        {
            IBuildComponent first = NodeProviderOutOfProcTaskHost.CreateProcessWideComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);
            IBuildComponent second = NodeProviderOutOfProcTaskHost.CreateProcessWideComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);

            first.ShouldNotBeNull();
            second.ShouldBeSameAs(first, "every build a worker node serves must resolve the same provider, or it forgets the task hosts the previous build left connected");
        }

        [Fact]
        public void CreateComponent_StillReturnsAFreshInstanceEachTime()
        {
            // BuildManager resolves the provider through the ordinary factory and is unaffected by
            // the process-wide one: its own lifetime already spans the builds it runs.
            IBuildComponent first = NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);
            IBuildComponent second = NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);

            second.ShouldNotBeSameAs(first);
            first.ShouldNotBeSameAs(NodeProviderOutOfProcTaskHost.CreateProcessWideComponent(BuildComponentType.OutOfProcTaskHostNodeProvider));
        }

        [Fact]
        public void InitializeComponent_WhenAlreadyInitialized_KeepsConnectedNodesAndRefreshesTheHost()
        {
            var provider = (NodeProviderOutOfProcTaskHost)NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);

            MockHost firstBuildHost = new();
            provider.InitializeComponent(firstBuildHost);

            object? nodeContextsAfterFirstBuild = GetNodeContexts(provider);
            nodeContextsAfterFirstBuild.ShouldNotBeNull();

            // The next build the worker node serves brings a new component host with it.
            MockHost secondBuildHost = new();
            provider.InitializeComponent(secondBuildHost);

            GetNodeContexts(provider).ShouldBeSameAs(
                nodeContextsAfterFirstBuild,
                "re-initializing would drop the connections to task hosts that are still running, leaving them unreachable and unable to exit");

            GetComponentHost(provider).ShouldBeSameAs(secondBuildHost, "the provider must still act on behalf of the build currently running");
        }

        private static object? GetNodeContexts(NodeProviderOutOfProcTaskHost provider)
            => typeof(NodeProviderOutOfProcTaskHost)
                .GetField("_nodeContexts", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(provider);

        private static object? GetComponentHost(NodeProviderOutOfProcTaskHost provider)
            => typeof(NodeProviderOutOfProcBase)
                .GetProperty("ComponentHost", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
                .GetValue(provider);
    }
}
