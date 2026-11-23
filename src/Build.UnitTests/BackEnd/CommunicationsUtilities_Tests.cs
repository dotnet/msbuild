// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for <see cref="CommunicationsUtilities.GetTaskHostHandshakeConfiguration"/> covering the worker
    /// node side of NET task host launches: the worker node must suppress its own architecture
    /// bit on the wire so already-shipped SDK TaskHost nodes (whose <c>IsAllowedBitnessMismatch</c>
    /// tolerates "worker node sent no arch bit") accept the connection regardless of either
    /// process arch.
    /// </summary>
    public sealed class CommunicationsUtilities_Tests
    {
        [Theory]
        [InlineData(XMakeAttributes.MSBuildArchitectureValues.x64)]
        [InlineData(XMakeAttributes.MSBuildArchitectureValues.arm64)]
        [InlineData(XMakeAttributes.MSBuildArchitectureValues.x86)]
        public void NetTaskHostConfiguration_SuppressesParentArchBit(string workerArchitecture)
        {
            var parameters = new TaskHostParameters(
                runtime: XMakeAttributes.MSBuildRuntimeValues.net,
                architecture: workerArchitecture);

            TaskHostHandshakeConfiguration configuration = CommunicationsUtilities.GetTaskHostHandshakeConfiguration(parameters);
            (configuration is NetTaskHostHandshakeConfiguration).ShouldBeTrue();
            HandshakeOptions options = configuration.HandshakeOptions;

            options.HasFlag(HandshakeOptions.NET).ShouldBeTrue();
            options.HasFlag(HandshakeOptions.X64).ShouldBeFalse();
            options.HasFlag(HandshakeOptions.Arm64).ShouldBeFalse();
        }

        [Fact]
        public void FrameworkTaskHostConfiguration_KeepsX64ArchBit()
        {
            var parameters = new TaskHostParameters(
                runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                architecture: XMakeAttributes.MSBuildArchitectureValues.x64);

            TaskHostHandshakeConfiguration configuration = CommunicationsUtilities.GetTaskHostHandshakeConfiguration(parameters);
            (configuration is FrameworkTaskHostHandshakeConfiguration).ShouldBeTrue();
            HandshakeOptions options = configuration.HandshakeOptions;

            options.HasFlag(HandshakeOptions.X64).ShouldBeTrue();
        }

        [Fact]
        public void FrameworkTaskHostConfiguration_KeepsArm64ArchBit()
        {
            var parameters = new TaskHostParameters(
                runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                architecture: XMakeAttributes.MSBuildArchitectureValues.arm64);

            HandshakeOptions options = CommunicationsUtilities.GetTaskHostHandshakeConfiguration(parameters).HandshakeOptions;

            options.HasFlag(HandshakeOptions.Arm64).ShouldBeTrue();
        }

        [Theory]
        [InlineData("x86", (int)TaskHostArchitecture.X86, 0)]
        [InlineData("x64", (int)TaskHostArchitecture.X64, (int)HandshakeOptions.X64)]
        [InlineData("ARM64", (int)TaskHostArchitecture.Arm64, (int)HandshakeOptions.Arm64)]
        [InlineData("unknown", (int)TaskHostArchitecture.Unspecified, 0)]
        public void FrameworkTaskHostConfiguration_EncodesOneArchitecture(string architecture, int expectedArchitecture, int expectedWireBits)
        {
            FrameworkTaskHostHandshakeConfiguration configuration = CommunicationsUtilities.GetFrameworkTaskHostHandshakeConfiguration(architecture);

            configuration.Options.Architecture.ShouldBe((TaskHostArchitecture)expectedArchitecture);
            (configuration.HandshakeOptions & (HandshakeOptions.X64 | HandshakeOptions.Arm64)).ShouldBe((HandshakeOptions)expectedWireBits);
        }

        [Fact]
        public void TaskHostOptions_EncodeIndependentWireFlags()
        {
            TaskHostHandshakeOptions options = new(
                TaskHostArchitecture.Arm64,
                NodeReuse: true,
                LowPriority: true,
                Administrator: true,
                SidecarTaskHost: true);

            options.ToWireOptions().ShouldBe(
                HandshakeOptions.Arm64 | HandshakeOptions.NodeReuse | HandshakeOptions.LowPriority
                | HandshakeOptions.Administrator | HandshakeOptions.SidecarTaskHost);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TaskHostHandshake_KeepsChildArchBit(bool net)
        {
            HandshakeOptions options = net
                ? CommunicationsUtilities.GetCurrentNetTaskHostHandshake().HandshakeOptions
                : CommunicationsUtilities.GetCurrentFrameworkTaskHostHandshake().HandshakeOptions;
            options.HasFlag(HandshakeOptions.TaskHost).ShouldBeTrue();
            options.HasFlag(HandshakeOptions.NET).ShouldBe(net);
            options.HasFlag(HandshakeOptions.CLR2).ShouldBeFalse();

            string currentArch = XMakeAttributes.GetCurrentMSBuildArchitecture();
            if (currentArch.Equals(XMakeAttributes.MSBuildArchitectureValues.x64, System.StringComparison.OrdinalIgnoreCase))
            {
                options.HasFlag(HandshakeOptions.X64).ShouldBeTrue();
            }
            else if (currentArch.Equals(XMakeAttributes.MSBuildArchitectureValues.arm64, System.StringComparison.OrdinalIgnoreCase))
            {
                options.HasFlag(HandshakeOptions.Arm64).ShouldBeTrue();
            }
            else
            {
                // x86 or unknown: no arch bit is expected on the TaskHost node side either.
                options.HasFlag(HandshakeOptions.X64).ShouldBeFalse();
                options.HasFlag(HandshakeOptions.Arm64).ShouldBeFalse();
            }
        }

        [Theory]
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.net, true)]
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.clr4, true)]
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.clr2, false)]
        public void TaskHostConfiguration_PreservesPriorityAndRuntimeReusePolicy(string runtime, bool reuseExpected)
        {
            TaskHostParameters parameters = new(runtime: runtime, architecture: XMakeAttributes.MSBuildArchitectureValues.arm64);
            TaskHostHandshakeConfiguration configuration = CommunicationsUtilities.GetTaskHostHandshakeConfiguration(
                parameters, nodeReuse: true, lowPriority: true);

            configuration.HandshakeOptions.HasFlag(HandshakeOptions.TaskHost).ShouldBeTrue();
            configuration.HandshakeOptions.HasFlag(HandshakeOptions.NodeReuse).ShouldBe(reuseExpected);
            configuration.HandshakeOptions.HasFlag(HandshakeOptions.LowPriority).ShouldBeTrue();
            configuration.HandshakeOptions.HasFlag(HandshakeOptions.NET).ShouldBe(runtime == XMakeAttributes.MSBuildRuntimeValues.net);
            configuration.HandshakeOptions.HasFlag(HandshakeOptions.CLR2).ShouldBe(runtime == XMakeAttributes.MSBuildRuntimeValues.clr2);
            configuration.HandshakeOptions.HasFlag(HandshakeOptions.Arm64).ShouldBe(runtime != XMakeAttributes.MSBuildRuntimeValues.net);
        }

        [Fact]
        public void TaskHostConfigurations_CreateTypedHandshakesWithResolvedInstallationIdentity()
        {
            string toolsDirectory = Path.Combine(Path.GetTempPath(), "resolved-task-host");
            Version fileVersion = new(18, 1, 2, 3);
            NetTaskHostHandshakeConfiguration netConfiguration = CommunicationsUtilities.GetNetTaskHostHandshakeConfiguration(nodeReuse: true);
            FrameworkTaskHostHandshakeConfiguration frameworkConfiguration = CommunicationsUtilities.GetFrameworkTaskHostHandshakeConfiguration(
                XMakeAttributes.MSBuildArchitectureValues.x64, nodeReuse: true);
            Clr2TaskHostHandshakeConfiguration clr2Configuration = CommunicationsUtilities.GetClr2TaskHostHandshakeConfiguration(
                XMakeAttributes.MSBuildArchitectureValues.x64);

            NetTaskHostHandshake net = netConfiguration.CreateHandshake(toolsDirectory);
            FrameworkTaskHostHandshake framework = frameworkConfiguration.CreateHandshake(toolsDirectory, fileVersion);
            Clr2TaskHostHandshake clr2 = clr2Configuration.CreateHandshake(toolsDirectory, fileVersion);

            net.HandshakeOptions.ShouldBe(netConfiguration.HandshakeOptions);
            framework.HandshakeOptions.ShouldBe(frameworkConfiguration.HandshakeOptions);
            clr2.HandshakeOptions.ShouldBe(clr2Configuration.HandshakeOptions);
            net.HandshakeOptions.HasFlag(HandshakeOptions.X64).ShouldBeFalse();
            clr2.HandshakeOptions.HasFlag(HandshakeOptions.NodeReuse).ShouldBeFalse();
            int expectedSalt = CommunicationsUtilities.GetHashCode(
                (Environment.GetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT") ?? string.Empty) + toolsDirectory);
            AssertIdentity(net.RetrieveHandshakeComponents(), new Version(99, 99, 99, 99));
            AssertIdentity(framework.RetrieveHandshakeComponents(), fileVersion);
            AssertIdentity(clr2.RetrieveHandshakeComponents(), fileVersion);

            void AssertIdentity(HandshakeComponents components, Version version)
            {
                components.Salt.ShouldBe(CommunicationsUtilities.AvoidEndOfHandshakeSignal(expectedSalt));
                components.FileVersionMajor.ShouldBe(version.Major);
                components.FileVersionMinor.ShouldBe(version.Minor);
                components.FileVersionBuild.ShouldBe(version.Build);
                components.FileVersionPrivate.ShouldBe(version.Revision);
            }
        }

        [Fact]
        public void NodeFactories_PreserveWorkerServerAndRarOptions()
        {
            WorkerNodeHandshake worker = CommunicationsUtilities.GetWorkerNodeHandshake(nodeReuse: true, lowPriority: true);
            ServerNodeHandshake server = CommunicationsUtilities.GetServerNodeHandshake("instance", nodeReuse: true, lowPriority: true);
            ServerNodeHandshake otherServer = CommunicationsUtilities.GetServerNodeHandshake("other-instance", nodeReuse: true, lowPriority: true);
            ServerNodeHandshake rar = CommunicationsUtilities.GetRarNodeHandshake();

            worker.HandshakeOptions.HasFlag(HandshakeOptions.NodeReuse).ShouldBeTrue();
            worker.HandshakeOptions.HasFlag(HandshakeOptions.LowPriority).ShouldBeTrue();
            (worker.HandshakeOptions & (HandshakeOptions.TaskHost | HandshakeOptions.NET | HandshakeOptions.CLR2)).ShouldBe(HandshakeOptions.None);
            server.HandshakeOptions.ShouldBe(worker.HandshakeOptions);
            server.RetrieveHandshakeComponents().SessionId.ShouldBe(0);
            server.ComputeHash().ShouldNotBe(otherServer.ComputeHash());
            rar.HandshakeOptions.ShouldBe(HandshakeOptions.None);
        }
    }
}
