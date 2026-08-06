// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.CommandLine;
using Microsoft.Build.Execution;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class OutOfProcTaskHostNode_Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShutdownRequestTerminatesReusableTaskHost(bool reuseTaskHostNodes)
    {
        OutOfProcTaskHostNode.DetermineShutdownReason(
            nodeReuse: true,
            prepareForReuse: false,
            reuseTaskHostNodes).ShouldBe(NodeEngineShutdownReason.BuildComplete);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ReuseRequestKeepsReusableTaskHostAlive(bool nodeReuse, bool reuseTaskHostNodes)
    {
        OutOfProcTaskHostNode.DetermineShutdownReason(
            nodeReuse,
            prepareForReuse: true,
            reuseTaskHostNodes).ShouldBe(NodeEngineShutdownReason.BuildCompleteReuse);
    }
}
