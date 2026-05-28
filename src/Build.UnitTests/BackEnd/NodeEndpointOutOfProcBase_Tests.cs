// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for <see cref="NodeEndpointOutOfProcBase.IsAllowedBitnessMismatch"/>, the .NET task
    /// host child-side tolerance that lets a parent which did not emit an architecture bit
    /// (e.g. a .NET Framework MSBuild) connect to an SDK child running on x64 or arm64.
    /// </summary>
    public sealed class NodeEndpointOutOfProcBase_Tests
    {
        private const HandshakeOptions BaseNet = HandshakeOptions.TaskHost | HandshakeOptions.NET;

        [Fact]
        public void NoArchBitParent_X64Child_IsTolerated()
        {
            NodeEndpointOutOfProcBase.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.X64),
                receivedOptions: (int)BaseNet).ShouldBeTrue();
        }

        [Fact]
        public void NoArchBitParent_Arm64Child_IsTolerated()
        {
            NodeEndpointOutOfProcBase.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.Arm64),
                receivedOptions: (int)BaseNet).ShouldBeTrue();
        }

        [Fact]
        public void X64Parent_X64Child_NotConsideredMismatch()
        {
            // Equal handshakes never hit IsAllowedBitnessMismatch in production; verify it
            // still returns false so the tolerance is scoped to the no-arch-bit parent only.
            NodeEndpointOutOfProcBase.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.X64),
                receivedOptions: (int)(BaseNet | HandshakeOptions.X64)).ShouldBeFalse();
        }

        [Fact]
        public void X64Parent_Arm64Child_NotTolerated()
        {
            // True architecture mismatch (parent sent X64, child expects Arm64) is rejected.
            NodeEndpointOutOfProcBase.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.Arm64),
                receivedOptions: (int)(BaseNet | HandshakeOptions.X64)).ShouldBeFalse();
        }

        [Fact]
        public void Arm64Parent_X64Child_NotTolerated()
        {
            NodeEndpointOutOfProcBase.IsAllowedBitnessMismatch(
                expectedOptions: (int)(BaseNet | HandshakeOptions.X64),
                receivedOptions: (int)(BaseNet | HandshakeOptions.Arm64)).ShouldBeFalse();
        }
    }
}

#endif
