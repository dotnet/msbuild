// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;
using Shouldly;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for <see cref="NodeEndpointOutOfProcBase.IsAllowedBitnessMismatch"/>, the .NET task
    /// host (TaskHost node) side tolerance that lets a worker node which did not emit an
    /// architecture bit (e.g. a .NET Framework MSBuild) connect to an SDK TaskHost node running
    /// on x64 or arm64.
    /// </summary>
    [TestClass]
    public sealed class NodeEndpointOutOfProcBase_Tests
    {
        private const HandshakeOptions BaseNet = HandshakeOptions.TaskHost | HandshakeOptions.NET;

        [MSBuildTestMethod]
        public void NoArchBitWorkerNode_X64TaskHost_IsTolerated()
        {
            NodeEndpointOutOfProcBase.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.X64),
                receivedOptions: (int)BaseNet).ShouldBeTrue();
        }

        [MSBuildTestMethod]
        public void NoArchBitWorkerNode_Arm64TaskHost_IsTolerated()
        {
            NodeEndpointOutOfProcBase.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.Arm64),
                receivedOptions: (int)BaseNet).ShouldBeTrue();
        }

        [MSBuildTestMethod]
        public void X64WorkerNode_X64TaskHost_NotConsideredMismatch()
        {
            // Equal handshakes never hit IsAllowedBitnessMismatch in production; verify it
            // still returns false so the tolerance is scoped to the no-arch-bit worker node only.
            NodeEndpointOutOfProcBase.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.X64),
                receivedOptions: (int)(BaseNet | HandshakeOptions.X64)).ShouldBeFalse();
        }

        [MSBuildTestMethod]
        public void X64WorkerNode_Arm64TaskHost_NotTolerated()
        {
            // True architecture mismatch (worker node sent X64, TaskHost node expects Arm64) is rejected.
            NodeEndpointOutOfProcBase.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.Arm64),
                receivedOptions: (int)(BaseNet | HandshakeOptions.X64)).ShouldBeFalse();
        }

        [MSBuildTestMethod]
        public void Arm64WorkerNode_X64TaskHost_NotTolerated()
        {
            NodeEndpointOutOfProcBase.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.X64),
                receivedOptions: (int)(BaseNet | HandshakeOptions.Arm64)).ShouldBeFalse();
        }

        [MSBuildTestMethod]
        public void NoArchBitWorkerNode_NoArchBitTaskHost_NotTolerated()
        {
            // The tolerance is scoped to x64/arm64 TaskHost nodes; an x86-equivalent (no arch bit)
            // TaskHost node must not silently accept any handshake.
            NodeEndpointOutOfProcBase.IsAllowedBitnessMismatch(
                expectedOptions: (int)BaseNet,
                receivedOptions: (int)BaseNet).ShouldBeFalse();
        }
    }
}

#endif
