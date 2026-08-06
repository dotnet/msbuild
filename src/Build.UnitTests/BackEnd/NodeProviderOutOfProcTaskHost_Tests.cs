// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
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
    /// and those task hosts are left running -- unreachable by their launcher and unclaimable by
    /// anyone else, because a task host pipe accepts a single connection.
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
            NodeProviderOutOfProcTaskHost provider = (NodeProviderOutOfProcTaskHost)NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);

            MockHost firstBuildHost = new();
            provider.InitializeComponent(firstBuildHost);

            object connectedNodesAfterFirstBuild = provider.ConnectedNodes;
            connectedNodesAfterFirstBuild.ShouldNotBeNull();

            // The next build this worker node serves brings a new component host with it.
            MockHost secondBuildHost = new();
            provider.InitializeComponent(secondBuildHost);

            provider.ConnectedNodes.ShouldBeSameAs(
                connectedNodesAfterFirstBuild,
                "re-initializing drops the connections to task hosts that are still running, leaving them unreachable and unable to exit");

            provider.CurrentComponentHost.ShouldBeSameAs(secondBuildHost, "the provider must act on behalf of the build currently running");
        }

        [Fact]
        public void ConnectionPersistsAcrossBuilds_OnlyForATaskHostThisProcessCouldHaveRunItself()
        {
            NodeProviderOutOfProcTaskHost provider = (NodeProviderOutOfProcTaskHost)NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);

            HandshakeOptions sameArchitecture = XMakeAttributes.GetCurrentMSBuildArchitecture() switch
            {
                XMakeAttributes.MSBuildArchitectureValues.arm64 => HandshakeOptions.Arm64,
                XMakeAttributes.MSBuildArchitectureValues.x64 => HandshakeOptions.X64,
                _ => HandshakeOptions.None,
            };

            provider.ConnectionPersists(HandshakeOptions.TaskHost | HandshakeOptions.NodeReuse | sameArchitecture).ShouldBeTrue(
                "a task host that only exists to keep a task out of this process is owned by it");

            provider.ConnectionPersists(HandshakeOptions.TaskHost | sameArchitecture).ShouldBeFalse(
                "a task host not launched with node reuse exits at the end of the build");
        }

        [Fact]
        public void Clr2TaskHostsStayPooled()
        {
            // A task host this process cannot run the task without is useful to every other process
            // that needs the same runtime, so owning it would cost one idle task host per worker
            // node for a task that only ever runs in one of them at a time.
            NodeProviderOutOfProcTaskHost provider = (NodeProviderOutOfProcTaskHost)NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);

            provider.ConnectionPersists(HandshakeOptions.TaskHost | HandshakeOptions.NodeReuse | HandshakeOptions.CLR2).ShouldBeFalse();
        }

        [WindowsFullFrameworkOnlyFact]
        public void NetTaskHostsStayPooledOnFramework()
        {
            // .NET Framework MSBuild cannot run a Runtime="NET" task itself, so the task host it
            // launches for one is shared, not owned.
            NodeProviderOutOfProcTaskHost provider = (NodeProviderOutOfProcTaskHost)NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);

            provider.ConnectionPersists(HandshakeOptions.TaskHost | HandshakeOptions.NodeReuse | HandshakeOptions.NET).ShouldBeFalse();
        }

        [Fact]
        public void CrossArchitectureTaskHostsStayPooled()
        {
            NodeProviderOutOfProcTaskHost provider = (NodeProviderOutOfProcTaskHost)NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);

            // No architecture bit means x86, which differs from this process unless it is itself x86.
            HandshakeOptions differentArchitecture = XMakeAttributes.GetCurrentMSBuildArchitecture() == XMakeAttributes.MSBuildArchitectureValues.x64
                ? HandshakeOptions.None
                : HandshakeOptions.X64;

            provider.ConnectionPersists(HandshakeOptions.TaskHost | HandshakeOptions.NodeReuse | differentArchitecture).ShouldBeFalse(
                "a task host of another architecture is useful to every process that needs that architecture");
        }

        [Fact]
        public void ConnectionDoesNotPersistWhenTheChangeWaveIsDisabled()
        {
            using TestEnvironment env = TestEnvironment.Create();
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_11.ToString());
            ChangeWaves.ResetStateForTests();

            try
            {
                NodeProviderOutOfProcTaskHost provider = (NodeProviderOutOfProcTaskHost)NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);

                provider.ConnectionPersists(HandshakeOptions.TaskHost | HandshakeOptions.NodeReuse | (XMakeAttributes.GetCurrentMSBuildArchitecture() == XMakeAttributes.MSBuildArchitectureValues.x64 ? HandshakeOptions.X64 : HandshakeOptions.None)).ShouldBeFalse(
                    "opting out of the wave must return the task host to the machine-wide pool it used to join");
            }
            finally
            {
                ChangeWaves.ResetStateForTests();
            }
        }
    }
}
