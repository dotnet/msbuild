// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TaskHostLaunchRecipes_KeepHandshakeAndArgumentsTogether(bool nodeReuse)
        {
            TaskHostHandshakeOptions options = new(NodeReuse: nodeReuse, LowPriority: true);
            string dotnetRoot = Path.Combine(Path.GetTempPath(), "task-host-dotnet");
            string dotnetPath = Path.Combine(dotnetRoot, "dotnet");
            NetTaskHostRequest netRequest = new(options, new(DotnetHostPath: dotnetPath));
            FrameworkTaskHostRequest frameworkRequest = new(options);
            Clr2TaskHostRequest clr2Request = new(TaskHostHandshakeOptions.None);
            string dllPath = Path.Combine("sdk path", "MSBuild.dll");

            NodeLaunchData<TaskHostHandshake> appHost = NodeLaunchData.ForNetTaskHostAppHost(
                netRequest, "apphost", toolsDirectory: null);
            NodeLaunchData<TaskHostHandshake> dll = NodeLaunchData.ForNetTaskHostDll(
                netRequest, dllPath, toolsDirectory: null);
            NodeLaunchData<TaskHostHandshake> framework = NodeLaunchData.ForFrameworkTaskHost(
                frameworkRequest, "msbuild");
            NodeLaunchData<TaskHostHandshake> clr2 = NodeLaunchData.ForClr2TaskHost(
                clr2Request, "legacy-task-host", "legacy-directory");

            (appHost.Handshake is NetTaskHostHandshake).ShouldBeTrue();
            (dll.Handshake is NetTaskHostHandshake).ShouldBeTrue();
            (framework.Handshake is FrameworkTaskHostHandshake).ShouldBeTrue();
            (clr2.Handshake is Clr2TaskHostHandshake).ShouldBeTrue();

            NodeLaunchData<TaskHostHandshake>[] modernLaunches = [appHost, dll, framework];
            foreach (NodeLaunchData<TaskHostHandshake> launch in modernLaunches)
            {
                launch.ExpectedNodeMode.ShouldBe(NodeMode.OutOfProcTaskHostNode);
                NodeModeHelper.ExtractFromCommandLine(launch.CommandLineArgs).ShouldBe(launch.ExpectedNodeMode);
                launch.Handshake.HandshakeOptions.HasFlag(HandshakeOptions.NodeReuse).ShouldBe(nodeReuse);
                launch.Handshake.LowPriority.ShouldBeTrue();
                launch.CommandLineArgs.ShouldContain($"/nodereuse:{nodeReuse} /low:True");
            }

            string taskHostArguments = $"/nologo /nodemode:2 /nodereuse:{nodeReuse} /low:True /parentpacketversion:{NodePacketTypeExtensions.PacketVersion} ";
            appHost.CommandLineArgs.ShouldBe(taskHostArguments);
            framework.CommandLineArgs.ShouldBe(taskHostArguments);
            appHost.EnvironmentOverrides!["DOTNET_ROOT"].ShouldBe(dotnetRoot);
            dll.MSBuildLocation.ShouldBe(dotnetPath);
            dll.CommandLineArgs.ShouldBe($"\"{dllPath}\" {taskHostArguments}");
            clr2.ExpectedNodeMode.ShouldBeNull();
            clr2.CommandLineArgs.ShouldBeEmpty();
            clr2.Handshake.HandshakeOptions.HasFlag(HandshakeOptions.NodeReuse).ShouldBeFalse();
        }

        [Fact]
        public void ExternalFrameworkLaunch_UsesResolvedInstallationForHandshakeAndEnvironment()
        {
            FrameworkTaskHostRequest request = new(TaskHostHandshakeOptions.None);
            Version fileVersion = new(18, 1, 2, 3);
            NodeLaunchData<TaskHostHandshake> launch = NodeLaunchData.ForExternalFrameworkTaskHost(
                request, "external-msbuild", "external-tools", fileVersion);

            launch.EnvironmentOverrides!["MSBUILD_EXE_PATH"].ShouldBe("external-msbuild");
            int expectedSalt = CommunicationsUtilities.GetHashCode(
                (Environment.GetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT") ?? string.Empty) + "external-tools");
            launch.Handshake.RetrieveHandshakeComponents().Salt.ShouldBe(CommunicationsUtilities.AvoidEndOfHandshakeSignal(expectedSalt));
            launch.Handshake.RetrieveHandshakeComponents().FileVersionMajor.ShouldBe(18);
            launch.Handshake.RetrieveHandshakeComponents().FileVersionPrivate.ShouldBe(3);
            launch.ExpectedNodeMode.ShouldBe(NodeMode.OutOfProcTaskHostNode);
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

            TaskHostArchitecture sameArchitecture = CommunicationsUtilities
                .GetFrameworkTaskHostRequest(XMakeAttributes.GetCurrentMSBuildArchitecture())
                .Options.Architecture;

            provider.ConnectionPersists(new FrameworkTaskHostHandshake(new(sameArchitecture, NodeReuse: true))).ShouldBeTrue(
                "a task host that only exists to keep a task out of this process is owned by it");

            provider.ConnectionPersists(new FrameworkTaskHostHandshake(new(sameArchitecture))).ShouldBeFalse(
                "a task host not launched with node reuse exits at the end of the build");
        }

        [Fact]
        public void Clr2TaskHostsStayPooled()
        {
            // A task host this process cannot run the task without is useful to every other process
            // that needs the same runtime, so owning it would cost one idle task host per worker
            // node for a task that only ever runs in one of them at a time.
            NodeProviderOutOfProcTaskHost provider = (NodeProviderOutOfProcTaskHost)NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);

            provider.ConnectionPersists(new Clr2TaskHostHandshake(
                new(NodeReuse: true),
                BuildEnvironmentHelper.Instance.MSBuildToolsDirectoryRoot)).ShouldBeFalse();
        }

        [WindowsFullFrameworkOnlyFact]
        public void NetTaskHostsStayPooledOnFramework()
        {
            // .NET Framework MSBuild cannot run a Runtime="NET" task itself, so the task host it
            // launches for one is shared, not owned.
            NodeProviderOutOfProcTaskHost provider = (NodeProviderOutOfProcTaskHost)NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);

            provider.ConnectionPersists(new NetTaskHostHandshake(new(NodeReuse: true))).ShouldBeFalse();
        }

        [Fact]
        public void CrossArchitectureTaskHostsStayPooled()
        {
            NodeProviderOutOfProcTaskHost provider = (NodeProviderOutOfProcTaskHost)NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);
            TaskHostArchitecture currentArchitecture = CommunicationsUtilities
                .GetFrameworkTaskHostRequest(XMakeAttributes.GetCurrentMSBuildArchitecture())
                .Options.Architecture;
            TaskHostArchitecture differentArchitecture = currentArchitecture == TaskHostArchitecture.X64
                ? TaskHostArchitecture.X86
                : TaskHostArchitecture.X64;

            provider.ConnectionPersists(new FrameworkTaskHostHandshake(new(differentArchitecture, NodeReuse: true))).ShouldBeFalse(
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
            TaskHostHandshake handshake = CreateHandshake(CommunicationsUtilities.GetTaskHostRequest(TaskHostParameters.Empty, nodeReuse: true));

            ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_11).ShouldBeTrue();
            provider.ConnectionPersists(handshake).ShouldBe(expectedPersistence);
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
            TaskHostHandshake handshake = CreateHandshake(CommunicationsUtilities.GetTaskHostRequest(TaskHostParameters.Empty, nodeReuse: true));

            provider.ConnectionPersists(handshake, version).ShouldBeFalse();
        }

        private static TaskHostHandshake CreateHandshake(TaskHostRequest request) => request switch
        {
            NetTaskHostRequest net => net.CreateHandshake(),
            FrameworkTaskHostRequest framework => framework.CreateHandshake(),
            Clr2TaskHostRequest clr2 => clr2.CreateHandshake(BuildEnvironmentHelper.Instance.MSBuildToolsDirectoryRoot),
        };

#if RUNTIME_TYPE_NETCORE
        [WindowsOnlyTheory]
        [InlineData((int)TaskHostArchitecture.Unspecified, "")]
        [InlineData((int)TaskHostArchitecture.X64, "amd64")]
        [InlineData((int)TaskHostArchitecture.Arm64, "arm64")]
        public void Clr4TaskHostIsLocatedInVisualStudioFromDeveloperCommandPrompt(int architectureValue, string architectureDirectory)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            TaskHostArchitecture architecture = (TaskHostArchitecture)architectureValue;
            string visualStudioRoot = env.CreateFolder().Path;
            string toolsDirectoryRoot = Path.Combine(visualStudioRoot, "MSBuild", "Current", "Bin");
            string msbuildExePath = Path.Combine(toolsDirectoryRoot, architectureDirectory, "MSBuild.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(msbuildExePath)!);
            File.WriteAllText(msbuildExePath, string.Empty);

            // A Developer Command Prompt sets VSINSTALLDIR with a trailing separator.
            env.SetEnvironmentVariable("VSINSTALLDIR", visualStudioRoot + Path.DirectorySeparatorChar);
            env.SetEnvironmentVariable("VisualStudioVersion", MSBuildConstants.CurrentVisualStudioVersion);

            FrameworkTaskHostRequest clr4Request = CreateFrameworkRequest(architecture, nodeReuse: true);

            NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(clr4Request, () => throw new InvalidOperationException())
                .ShouldBe(new NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.Found(msbuildExePath, toolsDirectoryRoot));

            NodeProviderOutOfProcTaskHost.GetMSBuildExecutablePathForNonNETRuntimes(clr4Request).ShouldBe(msbuildExePath);

            // Only the Visual Studio version matching this MSBuild is used.
            env.SetEnvironmentVariable("VisualStudioVersion", "17.0");
            (NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(clr4Request, () => [])
                is NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.NotFound).ShouldBeTrue();
        }

        [Theory]
        [InlineData((int)TaskHostArchitecture.Unspecified, "")]
        [InlineData((int)TaskHostArchitecture.X64, "amd64")]
        public void Clr2TaskHostUsesItsOwnArchitectureDirectory(int architectureValue, string architectureDirectory)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            TaskHostArchitecture architecture = (TaskHostArchitecture)architectureValue;
            string installation = env.CreateFolder().Path;
            string hostDirectory = Path.Combine(installation, "MSBuild", "Current", "Bin", architectureDirectory);
            Directory.CreateDirectory(hostDirectory);
            string hostPath = Path.Combine(hostDirectory, "MSBuildTaskHost.exe");
            File.WriteAllText(hostPath, string.Empty);
            env.SetEnvironmentVariable("VSINSTALLDIR", installation);
            env.SetEnvironmentVariable("VisualStudioVersion", MSBuildConstants.CurrentVisualStudioVersion);

            Clr2TaskHostRequest request = CreateClr2Request(architecture);
            NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(request, () => throw new InvalidOperationException())
                .ShouldBe(new NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.Found(hostPath, hostDirectory));
        }

        [Fact]
        public void Clr2DiscoveryRequiresTheRequestedHostAndDoesNotSupportArm64()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            ClearDeveloperCommandPrompt(env);
            VisualStudioInstance clr4Only = CreateVisualStudioInstallation(env, "CLR4 only", "18.0.11111.0", out _, out _);
            string hostDirectory = Path.Combine(clr4Only.Path, "MSBuild", "Current", "Bin", "amd64");
            Clr2TaskHostRequest context = CreateClr2Request(TaskHostArchitecture.X64);

            (NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(context, () => [clr4Only])
                is NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.NotFound).ShouldBeTrue();

            File.WriteAllText(Path.Combine(hostDirectory, "MSBuildTaskHost.exe"), string.Empty);
            (NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(context, () => [clr4Only])
                is NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.Found).ShouldBeTrue();

            VisualStudioInstance second = CreateVisualStudioInstallation(env, "Second", "18.1.11111.0", out _, out _);
            string secondHostDirectory = Path.Combine(second.Path, "MSBuild", "Current", "Bin", "amd64");
            File.WriteAllText(Path.Combine(secondHostDirectory, "MSBuildTaskHost.exe"), string.Empty);
            var multipleFound = NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(context, () => [clr4Only, second]) switch
            {
                NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.MultipleFound found => found,
                _ => throw new InvalidOperationException("Expected multiple Visual Studio installations."),
            };
            multipleFound.Installations.ShouldBe([clr4Only, second]);

            env.SetEnvironmentVariable("VSINSTALLDIR", env.CreateFolder().Path);
            env.SetEnvironmentVariable("VisualStudioVersion", MSBuildConstants.CurrentVisualStudioVersion);
            (NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(context, () => [clr4Only])
                is NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.Found).ShouldBeTrue();

            (NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(
                CreateClr2Request(TaskHostArchitecture.Arm64), () => [clr4Only])
                is NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.NotFound).ShouldBeTrue();
        }

        [Fact]
        public void Clr4TaskHostIsNotLocatedInVisualStudioOutsideDeveloperCommandPrompt()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("VSINSTALLDIR", null);
            env.SetEnvironmentVariable("VisualStudioVersion", null);

            (NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(CreateFrameworkRequest(TaskHostArchitecture.X64), () => [])
                is NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.NotFound).ShouldBeTrue();
        }

        [Fact]
        public void Clr4TaskHostIsNotLocatedInVisualStudioWithoutMSBuildExe()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("VSINSTALLDIR", env.CreateFolder().Path);
            env.SetEnvironmentVariable("VisualStudioVersion", MSBuildConstants.CurrentVisualStudioVersion);

            (NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(CreateFrameworkRequest(TaskHostArchitecture.X64), () => [])
                is NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.NotFound).ShouldBeTrue();
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
                CreateFrameworkRequest(TaskHostArchitecture.X64),
                () => [olderInstallation, installationWithoutMSBuild, installation])
                .ShouldBe(new NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.Found(msbuildExePath, toolsDirectoryRoot));
        }

        [Fact]
        public void Clr4TaskHostReportsMissingVisualStudio()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            ClearDeveloperCommandPrompt(env);

            VisualStudioInstance olderInstallation = CreateVisualStudioInstallation(env, "Visual Studio Enterprise 2022", "17.14.0.0", out _, out _);

            (NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(CreateFrameworkRequest(TaskHostArchitecture.X64), () => [])
                is NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.NotFound).ShouldBeTrue();

            (NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(CreateFrameworkRequest(TaskHostArchitecture.X64), () => [olderInstallation])
                is NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.NotFound).ShouldBeTrue();

            // A Developer Command Prompt for an installation without MSBuild.exe falls back to the installations that have it.
            env.SetEnvironmentVariable("VSINSTALLDIR", env.CreateFolder().Path);
            env.SetEnvironmentVariable("VisualStudioVersion", MSBuildConstants.CurrentVisualStudioVersion);
            (NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(CreateFrameworkRequest(TaskHostArchitecture.X64), () => [])
                is NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.NotFound).ShouldBeTrue();

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

            var multipleFound = NodeProviderOutOfProcTaskHost.ResolveVisualStudioTaskHost(CreateFrameworkRequest(TaskHostArchitecture.X64), () => [enterprise, buildTools]) switch
            {
                NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.MultipleFound found => found,
                _ => throw new InvalidOperationException("Expected multiple Visual Studio installations."),
            };
            multipleFound.Installations.ShouldBe([enterprise, buildTools]);

            string list = NodeProviderOutOfProcTaskHost.FormatVisualStudioInstallations(multipleFound.Installations);
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
                CreateFrameworkRequest(TaskHostArchitecture.X64),
                () => throw new InvalidOperationException("Visual Studio installations should not be queried."))
                .ShouldBe(new NodeProviderOutOfProcTaskHost.VisualStudioTaskHostResolution.Found(msbuildExePath, toolsDirectoryRoot));
        }

        private static FrameworkTaskHostRequest CreateFrameworkRequest(TaskHostArchitecture architecture = TaskHostArchitecture.X86, bool nodeReuse = false)
            => new(new(architecture, NodeReuse: nodeReuse));

        private static Clr2TaskHostRequest CreateClr2Request(TaskHostArchitecture architecture = TaskHostArchitecture.X86)
            => new(new(architecture));

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
