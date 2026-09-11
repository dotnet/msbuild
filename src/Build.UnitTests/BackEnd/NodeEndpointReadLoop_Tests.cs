// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared.Debugging;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class NodeEndpointReadLoop_Tests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void ReadLoop_DistinguishesFailureFromExpectedDisconnect(bool throwOnRead, bool expectedDisconnect)
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        using MemoryStream stream = throwOnRead ? new FailingReadStream() : new MemoryStream();
        using AutoResetEvent packetAvailable = new(false);
        using AutoResetEvent terminate = new(false);
        TestEndpoint endpoint = new();

        if (expectedDisconnect)
        {
            endpoint.ClientWillDisconnect();
        }

        DebugUtils.ResetDebugDumpPathInRunningTests = true;
        _ = DebugUtils.DebugDumpPath;
        endpoint.RunReadLoop(stream, new ConcurrentQueue<INodePacket>(), packetAvailable, terminate);

        if (throwOnRead)
        {
            env.WithTransientTestState(new TransientTestFile(DebugUtils.DebugDumpPath, Path.GetFileName(DebugUtils.DumpFilePath)));
            DebugUtils.ResetDebugDumpPathInRunningTests = true;
        }

        endpoint.LinkStatus.ShouldBe(expectedDisconnect ? LinkStatus.Active : LinkStatus.Failed);
    }

    private sealed class TestEndpoint : NodeEndpointOutOfProcBase
    {
        public TestEndpoint() => ChangeLinkStatus(LinkStatus.Active);

        protected override Handshake GetHandshake() => throw new NotSupportedException();
    }

    private sealed class FailingReadStream : MemoryStream
    {
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromException<int>(new IOException("Injected pipe read failure."));
    }
}
