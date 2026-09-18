// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
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

        // Keep the injected failure out of other test processes' crash-log checks.
        TransientTestFolder debugPath = env.CreateFolder();
        var transientDebugPath = env.SetEnvironmentVariable("MSBUILDDEBUGPATH", debugPath.Path);

        try
        {
            FrameworkDebugUtils.SetDebugPath();
            DebugUtils.ResetDebugDumpPathInRunningTests = true;
            _ = DebugUtils.DebugDumpPath;
            endpoint.RunReadLoop(stream, new ConcurrentQueue<INodePacket>(), packetAvailable, terminate);

            string[] failureLogs = Directory.GetFiles(debugPath.Path, "MSBuild_*.failure.txt", SearchOption.AllDirectories);
            if (throwOnRead)
            {
                failureLogs.ShouldHaveSingleItem();
                failureLogs[0].ShouldBe(DebugUtils.DumpFilePath);
                File.ReadAllText(failureLogs[0]).ShouldContain("Injected pipe read failure.");
            }
            else
            {
                failureLogs.ShouldBeEmpty();
            }

            endpoint.LinkStatus.ShouldBe(expectedDisconnect ? LinkStatus.Active : LinkStatus.Failed);
        }
        finally
        {
            transientDebugPath.Revert();
            FrameworkDebugUtils.SetDebugPath();
            DebugUtils.ResetDebugDumpPathInRunningTests = true;
            _ = DebugUtils.DebugDumpPath;
        }
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
