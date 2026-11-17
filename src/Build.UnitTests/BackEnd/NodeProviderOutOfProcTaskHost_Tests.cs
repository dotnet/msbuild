// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly ITestOutputHelper _output;

        public NodeProviderOutOfProcTaskHost_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

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

        [Theory]
        [InlineData("18.12", false)]
        [InlineData("999.999", true)]
        public void ConnectionPersistenceUsesWave18_12(string disabledWave, bool expectedPersistence)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", disabledWave);
            ChangeWaves.ResetStateForTests();

            NodeProviderOutOfProcTaskHost provider = (NodeProviderOutOfProcTaskHost)NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);
            HandshakeOptions options = CommunicationsUtilities.GetHandshakeOptions(taskHost: true, TaskHostParameters.Empty, nodeReuse: true);

            ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_11).ShouldBeTrue();
            provider.ConnectionPersists(options).ShouldBe(expectedPersistence);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(5)]
        public void LegacyTaskHostsAreNotTreatedAsPersistentConnections(byte version)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
            ChangeWaves.ResetStateForTests();
            NodeProviderOutOfProcTaskHost provider = (NodeProviderOutOfProcTaskHost)NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);
            HandshakeOptions options = CommunicationsUtilities.GetHandshakeOptions(taskHost: true, TaskHostParameters.Empty, nodeReuse: true);

            provider.ConnectionPersists(options, version).ShouldBeFalse();
        }

#if RUNTIME_TYPE_NETCORE
        [WindowsOnlyTheory]
        [InlineData((int)HandshakeOptions.None, "")]
        [InlineData((int)HandshakeOptions.X64, "amd64")]
        [InlineData((int)HandshakeOptions.Arm64, "arm64")]
        public void Clr4TaskHostIsLocatedInVisualStudioFromDeveloperCommandPrompt(int architecture, string architectureDirectory)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            string visualStudioRoot = env.CreateFolder().Path;
            string toolsDirectoryRoot = Path.Combine(visualStudioRoot, "MSBuild", "Current", "Bin");
            string msbuildExePath = Path.Combine(toolsDirectoryRoot, architectureDirectory, "MSBuild.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(msbuildExePath)!);
            File.WriteAllText(msbuildExePath, string.Empty);

            // A Developer Command Prompt sets VSINSTALLDIR with a trailing separator.
            env.SetEnvironmentVariable("VSINSTALLDIR", visualStudioRoot + Path.DirectorySeparatorChar);
            env.SetEnvironmentVariable("VisualStudioVersion", MSBuildConstants.CurrentVisualStudioVersion);

            HandshakeOptions clr4Context = HandshakeOptions.TaskHost | HandshakeOptions.NodeReuse | (HandshakeOptions)architecture;

            NodeProviderOutOfProcTaskHost.TryGetVisualStudioTaskHostPath(clr4Context, out string? foundPath, out string? foundToolsDirectoryRoot).ShouldBeTrue();
            foundPath.ShouldBe(msbuildExePath);
            foundToolsDirectoryRoot.ShouldBe(toolsDirectoryRoot);

            NodeProviderOutOfProcTaskHost.GetMSBuildExecutablePathForNonNETRuntimes(clr4Context).ShouldBe(msbuildExePath);

            // Only the .NET Framework (CLR4) task host comes from Visual Studio.
            NodeProviderOutOfProcTaskHost.TryGetVisualStudioTaskHostPath(clr4Context | HandshakeOptions.NET, out _, out _).ShouldBeFalse();
            NodeProviderOutOfProcTaskHost.TryGetVisualStudioTaskHostPath(clr4Context | HandshakeOptions.CLR2, out _, out _).ShouldBeFalse();

            // Only the Visual Studio version matching this MSBuild is used.
            env.SetEnvironmentVariable("VisualStudioVersion", "17.0");
            NodeProviderOutOfProcTaskHost.TryGetVisualStudioTaskHostPath(clr4Context, out _, out _).ShouldBeFalse();
        }

        [Fact]
        public void Clr4TaskHostIsNotLocatedInVisualStudioOutsideDeveloperCommandPrompt()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("VSINSTALLDIR", null);
            env.SetEnvironmentVariable("VisualStudioVersion", null);

            NodeProviderOutOfProcTaskHost.TryGetVisualStudioTaskHostPath(HandshakeOptions.TaskHost | HandshakeOptions.X64, out string? path, out string? root).ShouldBeFalse();
            path.ShouldBeNull();
            root.ShouldBeNull();
        }

        [Fact]
        public void Clr4TaskHostIsNotLocatedInVisualStudioWithoutMSBuildExe()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("VSINSTALLDIR", env.CreateFolder().Path);
            env.SetEnvironmentVariable("VisualStudioVersion", MSBuildConstants.CurrentVisualStudioVersion);

            NodeProviderOutOfProcTaskHost.TryGetVisualStudioTaskHostPath(HandshakeOptions.TaskHost | HandshakeOptions.X64, out _, out _).ShouldBeFalse();
        }

        [Fact]
        public void Clr4TaskHostUsesTheOnlyVisualStudioInstallationOutsideDeveloperCommandPrompt()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            ClearDeveloperCommandPrompt(env);

            VisualStudioInstance installation = CreateVisualStudioInstallation(env, "Visual Studio Enterprise 2026", "18.0.11111.0", out string msbuildExePath, out string toolsDirectoryRoot);
            VisualStudioInstance olderInstallation = CreateVisualStudioInstallation(env, "Visual Studio Enterprise 2022", "17.14.0.0", out _, out _);
            VisualStudioInstance installationWithoutMSBuild = new("Visual Studio Build Tools 2026", env.CreateFolder().Path, new Version(18, 0));

            NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(
                HandshakeOptions.TaskHost | HandshakeOptions.X64,
                () => [olderInstallation, installationWithoutMSBuild, installation],
                out string? foundPath,
                out string? foundToolsDirectoryRoot,
                out IReadOnlyList<VisualStudioInstance> installations).ShouldBe(NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.Found);

            foundPath.ShouldBe(msbuildExePath);
            foundToolsDirectoryRoot.ShouldBe(toolsDirectoryRoot);
            installations.ShouldBe([installation]);
        }

        [Fact]
        public void Clr4TaskHostReportsMissingVisualStudio()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            ClearDeveloperCommandPrompt(env);

            VisualStudioInstance olderInstallation = CreateVisualStudioInstallation(env, "Visual Studio Enterprise 2022", "17.14.0.0", out _, out _);

            NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(HandshakeOptions.TaskHost | HandshakeOptions.X64, () => [], out string? path, out string? root, out IReadOnlyList<VisualStudioInstance> installations)
                .ShouldBe(NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.NotFound);
            path.ShouldBeNull();
            root.ShouldBeNull();
            installations.ShouldBeEmpty();

            NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(HandshakeOptions.TaskHost | HandshakeOptions.X64, () => [olderInstallation], out _, out _, out _)
                .ShouldBe(NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.NotFound);

            // A Developer Command Prompt for an installation without MSBuild.exe falls back to the installations that have it.
            env.SetEnvironmentVariable("VSINSTALLDIR", env.CreateFolder().Path);
            env.SetEnvironmentVariable("VisualStudioVersion", MSBuildConstants.CurrentVisualStudioVersion);
            NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(HandshakeOptions.TaskHost | HandshakeOptions.X64, () => [], out _, out _, out _)
                .ShouldBe(NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.NotFound);

            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
                out string? code,
                out _,
                "TaskHostVisualStudioNotFound",
                "LegacyTask",
                "CLR4",
                "x64",
                MSBuildConstants.CurrentVisualStudioVersion);
            code.ShouldBe("MSB4291");
            message.ShouldContain("LegacyTask");
            message.ShouldContain(MSBuildConstants.CurrentVisualStudioVersion);
        }

        [Fact]
        public void Clr4TaskHostReportsMultipleVisualStudioInstallations()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            ClearDeveloperCommandPrompt(env);

            VisualStudioInstance enterprise = CreateVisualStudioInstallation(env, "Visual Studio Enterprise 2026", "18.0.11111.0", out _, out _);
            VisualStudioInstance buildTools = CreateVisualStudioInstallation(env, "Visual Studio Build Tools 2026", "18.1.22222.0", out _, out _);

            NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(
                HandshakeOptions.TaskHost | HandshakeOptions.X64,
                () => [enterprise, buildTools],
                out string? path,
                out string? root,
                out IReadOnlyList<VisualStudioInstance> installations).ShouldBe(NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.MultipleFound);

            path.ShouldBeNull();
            root.ShouldBeNull();
            installations.ShouldBe([enterprise, buildTools]);

            string list = NodeProviderOutOfProcTaskHost.FormatVisualStudioInstallations(installations);
            list.ShouldContain($"Visual Studio Enterprise 2026 (18.0.11111.0): {enterprise.Path}");
            list.ShouldContain($"Visual Studio Build Tools 2026 (18.1.22222.0): {buildTools.Path}");

            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
                out string? code,
                out _,
                "TaskHostMultipleVisualStudioFound",
                "LegacyTask",
                "CLR4",
                "x64",
                MSBuildConstants.CurrentVisualStudioVersion,
                list);
            code.ShouldBe("MSB4292");
            message.ShouldContain(enterprise.Path);
            message.ShouldContain(buildTools.Path);
            message.ShouldContain("VSINSTALLDIR");
        }

        [Fact]
        public void Clr4TaskHostPrefersDeveloperCommandPromptOverDiscoveredInstallations()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            VisualStudioInstance enterprise = CreateVisualStudioInstallation(env, "Visual Studio Enterprise 2026", "18.0.11111.0", out string msbuildExePath, out string toolsDirectoryRoot);
            VisualStudioInstance buildTools = CreateVisualStudioInstallation(env, "Visual Studio Build Tools 2026", "18.1.22222.0", out _, out _);
            env.SetEnvironmentVariable("VSINSTALLDIR", enterprise.Path + Path.DirectorySeparatorChar);
            env.SetEnvironmentVariable("VisualStudioVersion", MSBuildConstants.CurrentVisualStudioVersion);

            NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(
                HandshakeOptions.TaskHost | HandshakeOptions.X64,
                () => throw new InvalidOperationException("Visual Studio installations should not be queried."),
                out string? foundPath,
                out string? foundToolsDirectoryRoot,
                out _).ShouldBe(NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.Found);

            foundPath.ShouldBe(msbuildExePath);
            foundToolsDirectoryRoot.ShouldBe(toolsDirectoryRoot);
        }

        [Theory]
        [InlineData((int)(HandshakeOptions.TaskHost | HandshakeOptions.NET | HandshakeOptions.X64))]
        [InlineData((int)(HandshakeOptions.TaskHost | HandshakeOptions.CLR2))]
        [InlineData((int)(HandshakeOptions.NodeReuse | HandshakeOptions.X64))]
        public void VisualStudioIsNotUsedForOtherHosts(int hostContext)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            ClearDeveloperCommandPrompt(env);

            NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(
                (HandshakeOptions)hostContext,
                () => throw new InvalidOperationException("Visual Studio installations should not be queried."),
                out string? path,
                out _,
                out IReadOnlyList<VisualStudioInstance> installations).ShouldBe(NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.NotApplicable);

            path.ShouldBeNull();
            installations.ShouldBeEmpty();
        }

        private static void ClearDeveloperCommandPrompt(TestEnvironment env)
        {
            env.SetEnvironmentVariable("VSINSTALLDIR", null);
            env.SetEnvironmentVariable("VisualStudioVersion", null);
        }

        private static VisualStudioInstance CreateVisualStudioInstallation(TestEnvironment env, string name, string version, out string msbuildExePath, out string toolsDirectoryRoot)
        {
            string installationPath = env.CreateFolder().Path;
            toolsDirectoryRoot = Path.Combine(installationPath, "MSBuild", "Current", "Bin");
            msbuildExePath = Path.Combine(toolsDirectoryRoot, "amd64", "MSBuild.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(msbuildExePath)!);
            File.WriteAllText(msbuildExePath, string.Empty);

            return new VisualStudioInstance(name, installationPath, Version.Parse(version));
        }
#endif
    }
}
