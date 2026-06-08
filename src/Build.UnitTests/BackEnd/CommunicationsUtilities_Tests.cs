// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for <see cref="CommunicationsUtilities.GetHandshakeOptions"/> covering the worker
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
        public void GetHandshakeOptions_NetTaskHostWorkerNode_SuppressesArchBit(string workerArchitecture)
        {
            var parameters = new TaskHostParameters(
                runtime: XMakeAttributes.MSBuildRuntimeValues.net,
                architecture: workerArchitecture);

            HandshakeOptions options = CommunicationsUtilities.GetHandshakeOptions(
                taskHost: true,
                taskHostParameters: parameters);

            options.HasFlag(HandshakeOptions.NET).ShouldBeTrue();
            options.HasFlag(HandshakeOptions.X64).ShouldBeFalse();
            options.HasFlag(HandshakeOptions.Arm64).ShouldBeFalse();
        }

        [Fact]
        public void GetHandshakeOptions_NonNetTaskHostWorkerNode_KeepsX64ArchBit()
        {
            var parameters = new TaskHostParameters(
                runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                architecture: XMakeAttributes.MSBuildArchitectureValues.x64);

            HandshakeOptions options = CommunicationsUtilities.GetHandshakeOptions(
                taskHost: true,
                taskHostParameters: parameters);

            options.HasFlag(HandshakeOptions.X64).ShouldBeTrue();
        }

        [Fact]
        public void GetHandshakeOptions_NonNetTaskHostWorkerNode_KeepsArm64ArchBit()
        {
            var parameters = new TaskHostParameters(
                runtime: XMakeAttributes.MSBuildRuntimeValues.clr4,
                architecture: XMakeAttributes.MSBuildArchitectureValues.arm64);

            HandshakeOptions options = CommunicationsUtilities.GetHandshakeOptions(
                taskHost: true,
                taskHostParameters: parameters);

            options.HasFlag(HandshakeOptions.Arm64).ShouldBeTrue();
        }

        [Fact]
        public void GetHandshakeOptions_NetTaskHostNode_KeepsArchBit()
        {
            // The TaskHost node invokes GetHandshakeOptions with TaskHostParameters.Empty; the
            // helper then derives architectureFlagToSet from GetCurrentMSBuildArchitecture(). The
            // suppression must not apply: the TaskHost node needs to keep its own arch bit so
            // already-deployed worker nodes that still emit one continue to match.
            HandshakeOptions options = CommunicationsUtilities.GetHandshakeOptions(
                taskHost: true,
                taskHostParameters: TaskHostParameters.Empty);

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
    }
}
